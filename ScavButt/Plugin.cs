using BepInEx;
using BepInEx.Logging;
using ScavLib.command;

namespace ScavButt;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.kanisuko.scavlib", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.scavbutt.scavbutt";
    public const string PluginName = "ScavButt";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log = null!;

    private void Awake()
    {
        Log = Logger;

        if (!CommandRegistry.TryRegister(new ButtCommand(), PluginName, out var error))
            Log.LogError($"[ScavButt] Failed to register 'butt' command: {error}");

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        // TODO: start Buttplug connection loop
        // TODO: register WorldLoadedEvent listener for damage watcher
    }
}
