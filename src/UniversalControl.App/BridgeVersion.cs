using System.Reflection;

namespace UniversalControl.App;

internal static class BridgeVersion
{
    public static string Display => typeof(BridgeVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "DisplayVersion").Value!;
}
