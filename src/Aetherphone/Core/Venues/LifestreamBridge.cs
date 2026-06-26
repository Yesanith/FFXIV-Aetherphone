namespace Aetherphone.Core.Venues;

internal static class LifestreamBridge
{
    private const string InternalName = "Lifestream";

    public static bool IsAvailable()
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, InternalName, StringComparison.Ordinal) && plugin.IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    public static void Travel(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        Plugin.CommandManager.ProcessCommand($"/li {code}");
    }
}
