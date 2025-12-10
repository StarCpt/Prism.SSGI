using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prism.Render;

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

public class OverrideReplaceAttribute() : OverrideAttribute(OverrideOrder.Replace) { }
public class OverrideBeforeAttribute() : OverrideAttribute(OverrideOrder.Before) { }
public class OverrideAfterAttribute() : OverrideAttribute(OverrideOrder.After) { }
