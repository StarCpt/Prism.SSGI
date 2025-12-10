using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Prism.Render.Utils;

/// <summary>
/// Helper class to simplify overriding non-virtual or nonaccessible instance methods.
/// </summary>
static class OverridePatchFactory
{
    private static readonly Dictionary<MethodBase, MethodInfo> _baseToOverride = [];

    public static void Init(Harmony harmony)
    {
        Type[] assemblyTypes = AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly());
        IEnumerable<MethodInfo> overrideMethods = assemblyTypes.SelectMany(t => AccessTools.GetDeclaredMethods(t).Where(m => m.HasAttribute<OverrideAttribute>()));
        foreach (MethodInfo overrideMethod in overrideMethods)
        {
            if (overrideMethod.IsStatic)
            {
                throw new Exception("Override method can't be static.");
            }

            OverrideOrder order = overrideMethod.GetCustomAttribute<OverrideAttribute>().Order;
            if (order is not OverrideOrder.Replace and not OverrideOrder.Before and not OverrideOrder.After)
            {
                throw new Exception("Invalid override order.");
            }

            Type baseType = overrideMethod.DeclaringType.BaseType;
            if (baseType == null || baseType == typeof(object) || baseType.IsValueType)
            {
                throw new Exception("Base class not found.");
            }

            MethodInfo baseMethod = AccessTools.Method(baseType, overrideMethod.Name);
            if (baseMethod is null)
            {
                throw new Exception("Base method not found.");
            }

            if (baseMethod.IsStatic)
            {
                throw new Exception("Base method can't be static.");
            }

            // PLACEHOLDER
            if (baseMethod.ReturnType != null && baseMethod.ReturnType != typeof(void))
            {
                throw new NotImplementedException();
            }

            _baseToOverride.Add(baseMethod, overrideMethod);

            if (order is OverrideOrder.Replace or OverrideOrder.Before)
            {
                harmony.Patch(baseMethod, prefix: new HarmonyMethod(Factory));
            }
            else if (order is OverrideOrder.After)
            {
                harmony.Patch(baseMethod, postfix: new HarmonyMethod(Factory));
            }
        }
    }

    private static DynamicMethod Factory(MethodBase baseMethod)
    {
        MethodInfo overrideMethod = _baseToOverride[baseMethod];
        OverrideOrder order = overrideMethod.GetCustomAttribute<OverrideAttribute>().Order;

        Type baseType = baseMethod.DeclaringType;
        Type overrideType = overrideMethod.DeclaringType;
        string glueMethodName = $"{typeof(OverridePatchFactory).FullName}_Override{order}_{baseMethod.DeclaringType.FullName}::{baseMethod.Name}_{overrideMethod.DeclaringType.FullName}::{overrideMethod.Name}";

        List<(Type Type, string Name)> glueParams = [];
        glueParams.Add((baseType, "__instance"));
        foreach (ParameterInfo param in baseMethod.GetParameters())
        {
            glueParams.Add((param.ParameterType, param.Name));
        }

        Type? returnType = order is OverrideOrder.Replace ? typeof(bool) : null;

        var glueMethod = new DynamicMethod(glueMethodName, returnType, [.. glueParams.Select(i => i.Type)], overrideType.Module, true);

        // set parameter names
        foreach (var (param, i) in glueMethod.GetParameters().Select((p, i) => (p, i)))
        {
            glueMethod.DefineParameter(i + 1, param.Attributes, glueParams[i].Name); // index starts at 1
        }

        var il = glueMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // load __instance
        il.Emit(OpCodes.Isinst, overrideType); // check if __instance is overrideType

        // result of isinst is either an instance of overrideType or null evaluated to true and false respectively
        // therefore we can skip casting in the "true" branch since we know __instance's type is overrideType

        Label brJump = il.DefineLabel();
        il.Emit(OpCodes.Brfalse_S, brJump);    // branch jump to label if false

        // if true
        // load args for overrideMethod
        for (int i = 0; i < glueParams.Count; i++)
        {
            if (glueParams[i].Type.IsByRef)
            {
                if (i <= byte.MaxValue)
                {
                    il.Emit(OpCodes.Ldarga_S, (byte)i);
                }
                else
                {
                    il.Emit(OpCodes.Ldarga, (ushort)i);
                }
            }
            else
            {
                switch (i)
                {
                    case 0: il.Emit(OpCodes.Ldarg_0); break;
                    case 1: il.Emit(OpCodes.Ldarg_1); break;
                    case 2: il.Emit(OpCodes.Ldarg_2); break;
                    case 3: il.Emit(OpCodes.Ldarg_3); break;
                    default:
                        if (i <= byte.MaxValue)
                        {
                            il.Emit(OpCodes.Ldarg_S, (byte)i);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldarg, (ushort)i);
                        }
                        break;
                }
            }
        }
        il.Emit(OpCodes.Call, overrideMethod); // call override method on __instance

        if (order is OverrideOrder.Replace)
        {
            il.Emit(OpCodes.Ldc_I4_0); // load bool false (int32 0x0)
            il.Emit(OpCodes.Ret);

            // if false
            il.MarkLabel(brJump);
            il.Emit(OpCodes.Ldc_I4_1); // load bool true (int32 0x1)
            il.Emit(OpCodes.Ret);
        }
        else if (order is OverrideOrder.Before)
        {
            // if false
            il.MarkLabel(brJump);
            il.Emit(OpCodes.Ret);
        }
        else if (order is OverrideOrder.After)
        {
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
