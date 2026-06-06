using System.Reflection;

namespace AerationSterilize.Persistence;
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
