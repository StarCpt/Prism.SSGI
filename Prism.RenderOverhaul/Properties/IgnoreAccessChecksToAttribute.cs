[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("VRage")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("VRage.Render")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("VRage.Render11")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        internal IgnoresAccessChecksToAttribute(string assemblyName)
        {
        }
    }
}
