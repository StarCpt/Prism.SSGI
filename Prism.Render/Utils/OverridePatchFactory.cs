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
    const string BASE_RESULT_PARAM_NAME = "__baseResult";

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

            if (baseMethod.ReturnType.IsByRef || (baseMethod.ReturnType != typeof(void) && baseMethod.ReturnType.IsValueType))
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

    private static DynamicMethod Factory(MethodBase baseMethod) // baseMethod is MethodInfo
    {
        MethodInfo overrideMethod = _baseToOverride[baseMethod];
        OverrideOrder order = overrideMethod.GetCustomAttribute<OverrideAttribute>().Order;

        if (!ValidateOverrideMethodParameters((MethodInfo)baseMethod, overrideMethod, order))
        {
            throw new Exception("Invalid override method parameters.");
        }

        (Type Type, string Name)[] glueParams = GetGlueMethodParameters((MethodInfo)baseMethod);

        string glueMethodName = $"{typeof(OverridePatchFactory).FullName}_Override{order}_{baseMethod.DeclaringType.FullName}::{baseMethod.Name}_{overrideMethod.DeclaringType.FullName}::{overrideMethod.Name}";
        Type glueReturnType = order is OverrideOrder.Replace ? typeof(bool) : typeof(void);
        var glueMethod = new DynamicMethod(glueMethodName, glueReturnType, [.. glueParams.Select(i => i.Type)], overrideMethod.Module, true);

        // set parameter names
        foreach ((ParameterInfo param, int i) in glueMethod.GetParameters().Select((p, i) => (p, i)))
        {
            glueMethod.DefineParameter(i + 1, param.Attributes, glueParams[i].Name); // index starts at 1
        }

        ILGenerator il = glueMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // load __instance
        il.Emit(OpCodes.Isinst, overrideMethod.DeclaringType); // check if __instance is overrideType

        // result of isinst is either an instance of overrideType or null evaluated to true and false respectively
        // therefore we can skip casting in the "true" branch since we know __instance's type is overrideType

        Label brJump = il.DefineLabel();
        il.Emit(OpCodes.Brfalse_S, brJump); // branch jump to label if false

        // if true
        bool overrideHasReturn = overrideMethod.ReturnType != typeof(void);
        bool overrideHasBaseResultParam = overrideHasReturn && order is OverrideOrder.After;

        if (overrideHasReturn)
        {
            il.Emit(OpCodes.Ldarg_1); // load T* __result to store overrideMethod result in later
        }

        // load args for overrideMethod
        for (int i = 0; i < glueParams.Length; i++)
        {
            if (overrideHasBaseResultParam && i == 1)
            {
                il.Emit(OpCodes.Ldarg_1);   // load T* result
                il.Emit(OpCodes.Ldind_Ref); // deref T* result
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

        if (overrideHasReturn)
        {
            il.Emit(OpCodes.Stind_Ref); // store return value at addr on stack (T* __result)
        }

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

    private static (Type Type, string Name)[] GetGlueMethodParameters(MethodInfo baseMethod)
    {
        List<(Type Type, string Name)> glueParams = [];
        glueParams.Add((baseMethod.DeclaringType, "__instance"));

        if (baseMethod.ReturnType != typeof(void))
        {
            glueParams.Add((baseMethod.ReturnType.MakeByRefType(), "__result"));
        }

        foreach (ParameterInfo param in baseMethod.GetParameters())
        {
            glueParams.Add((param.ParameterType, param.Name));
        }

        return glueParams.ToArray();
    }

    private static bool ValidateOverrideMethodParameters(MethodInfo baseMethod, MethodInfo overrideMethod, OverrideOrder order)
    {
        if (order is OverrideOrder.Replace)
        {
            return overrideMethod.ReturnType == baseMethod.ReturnType && EnsureParamsAreSame(baseMethod, overrideMethod, false);
        }
        else if (order is OverrideOrder.Before)
        {
            return overrideMethod.ReturnType == typeof(void) && EnsureParamsAreSame(baseMethod, overrideMethod, false);
        }
        else if (order is OverrideOrder.After)
        {
            if (baseMethod.ReturnType != overrideMethod.ReturnType)
                return false;

            bool hasReturnValue = baseMethod.ReturnType != typeof(void);
            return EnsureParamsAreSame(baseMethod, overrideMethod, hasReturnValue);
        }
        else
        {
            throw new ArgumentException(nameof(order));
        }

        static bool EnsureParamsAreSame(MethodInfo baseMethod, MethodInfo overrideMethod, bool overrideMethodHasBaseResultParam)
        {
            ParameterInfo[] baseParams = baseMethod.GetParameters();
            ParameterInfo[] overrideParams = overrideMethod.GetParameters();

            if (overrideMethodHasBaseResultParam)
            {
                // check the [T __baseResult] param
                ParameterInfo baseResultParam = overrideParams.FirstOrDefault();
                if (baseResultParam is null ||
                    baseResultParam.ParameterType != baseMethod.ReturnType ||
                    baseResultParam.Name != BASE_RESULT_PARAM_NAME)
                {
                    return false;
                }

                overrideParams = [.. overrideParams.Skip(1)];
            }

            if (baseParams.Length != overrideParams.Length)
            {
                return false;
            }

            for (int i = 0; i < baseParams.Length; i++)
            {
                ParameterInfo baseParam = baseParams[i];
                ParameterInfo overrideParam = overrideParams[i];
                if (baseParam.ParameterType != overrideParam.ParameterType ||
                    baseParam.Name != overrideParam.Name)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
