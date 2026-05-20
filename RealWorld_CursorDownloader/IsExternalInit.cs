// Polyfill required for C# 9+ record types targeting .NET Standard 2.x
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
