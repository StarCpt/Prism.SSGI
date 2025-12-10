using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Prism.Render;

static class OverridePatchFactory
{
    private static readonly Dictionary<MethodBase, MethodInfo> _baseToOverride = [];

    public static void Init(Harmony harmony)
    {
        Type[] assemblyTypes = AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly());
        IEnumerable<MethodInfo> overrideMethods = assemblyTypes.SelectMany(t => AccessTools.GetDeclaredMethods(t).Where(m => m.HasAttribute<OverrideAttribute>()));
        foreach (MethodInfo overrideMethod in overrideMethods)
        {
            Type baseType = overrideMethod.DeclaringType.BaseType;
            if (baseType == null || baseType == typeof(object) || overrideMethod.DeclaringType.IsValueType)
            {
                throw new Exception("No base class.");
            }

            OverrideAttribute attribute = overrideMethod.GetCustomAttribute<OverrideAttribute>();
            if (attribute.Order is not OverrideOrder.Replace and not OverrideOrder.Before and not OverrideOrder.After)
            {
                throw new Exception("Invalid override order.");
            }

            MethodInfo baseMethod = AccessTools.Method(baseType, overrideMethod.Name);
            if (baseMethod.IsStatic || overrideMethod.IsStatic)
            {
                throw new Exception("Methods can't be static.");
            }

            _baseToOverride.Add(baseMethod, overrideMethod);

            if (attribute.Order is OverrideOrder.Replace or OverrideOrder.Before)
            {
                harmony.Patch(baseMethod, prefix: new HarmonyMethod(Factory));
            }
            else if (attribute.Order is OverrideOrder.After)
            {
                harmony.Patch(baseMethod, postfix: new HarmonyMethod(Factory));
            }
        }
    }

    static DynamicMethod Factory(MethodBase baseMethod)
    {
        MethodInfo overrideMethod = _baseToOverride[baseMethod];
        OverrideOrder order = overrideMethod.GetCustomAttribute<OverrideAttribute>().Order;

        Type baseType = baseMethod.DeclaringType;
        Type overrideType = overrideMethod.DeclaringType;
        string glueMethodName = $"{typeof(OverridePatchFactory).FullName}_{order}_{baseMethod.DeclaringType.FullName}:{baseMethod.Name}_{overrideMethod.DeclaringType.FullName}:{overrideMethod.Name}";

        bool hasReturnValueBool = order is OverrideOrder.Replace;

        var glueMethod = new DynamicMethod(glueMethodName, hasReturnValueBool ? typeof(bool) : null, [baseType], overrideType.Module, true);
        glueMethod.DefineParameter(1, ParameterAttributes.None, "__instance"); // index starts at 1

        var il = glueMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // load __instance
        il.Emit(OpCodes.Isinst, overrideType); // check if __instance is overrideType

        // result of isinst is either an instance of overrideType or null evaluated to true and false respectively
        // therefore we can skip casting in the "true" branch since we know __instance's type is overrideType

        Label brJump = il.DefineLabel();
        il.Emit(OpCodes.Brfalse_S, brJump); // branch jump to label if false

        if (order is OverrideOrder.Replace)
        {
            // if true
            il.Emit(OpCodes.Ldarg_0);               // load __instance
            il.Emit(OpCodes.Call, overrideMethod);  // call override method on __instance
            il.Emit(OpCodes.Ldc_I4_0);              // load bool false (int32 0x0)
            il.Emit(OpCodes.Ret);

            // if false
            il.MarkLabel(brJump);
            il.Emit(OpCodes.Ldc_I4_1);              // load bool true (int32 0x1)
            il.Emit(OpCodes.Ret);
        }
        else if (order is OverrideOrder.Before)
        {
            // if true
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, overrideMethod);

            // if false
            il.MarkLabel(brJump);
            il.Emit(OpCodes.Ret);
        }
        else if (order is OverrideOrder.After)
        {
            // if true
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, overrideMethod);

            // if false
            il.MarkLabel(brJump);
            il.Emit(OpCodes.Ret);
        }
        else
        {
            throw new ArgumentException(nameof(order));
        }

        return glueMethod;
    }
}
