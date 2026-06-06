using System.Reflection;

namespace AerationSterilize.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
