// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill required by the compiler to emit <c>init</c> accessors on .NET Framework, which does
    /// not ship the type. Recognised by convention; never called at runtime.
    /// </summary>
    /// <remarks>
    /// Used by the immutable request/result structs in <c>JDFixer.Core</c>, which rely on
    /// <c>init</c> plus <c>with</c> so the setpoint resolver can build up a result without mutation.
    /// </remarks>
    internal static class IsExternalInit
    {
    }
}
