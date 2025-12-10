using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prism.Render.Utils;

public enum OverrideOrder
{
    Replace = 0,
    Before = 1,
    After = 2,
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public abstract class OverrideAttribute : Attribute
{
    public OverrideOrder Order { get; }

    public OverrideAttribute(OverrideOrder baseMethodInvoke)
    {
        Order = baseMethodInvoke;
    }
}

/// <summary>
/// Calls this method instead of the base method.
/// </summary>
public class OverrideReplaceAttribute() : OverrideAttribute(OverrideOrder.Replace) { }

/// <summary>
/// Calls this method before the base method.
/// </summary>
public class OverrideBeforeAttribute() : OverrideAttribute(OverrideOrder.Before) { }

/// <summary>
/// Calls this method after the base method.
/// </summary>
public class OverrideAfterAttribute() : OverrideAttribute(OverrideOrder.After) { }
