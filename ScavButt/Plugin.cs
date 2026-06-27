using BepInEx;
using BepInEx.Logging;
using ScavLib.command;
using ScavLib.event_bus;
using ScavLib.event_bus.events;

namespace ScavButt;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.kanisuko.scavlib", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid    = "com.scavbutt.scavbutt";
    public const string PluginName    = "ScavButt";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log = null!;

    private VibrationSystem? _vibrationSystem;

    private void Awake()
    {
        Log = Logger;

        if (!CommandRegistry.TryRegister(new ButtCommand(), PluginName, out var error))
            Log.LogError($"[ScavButt] Failed to register 'butt' command: {error}");

        ButtplugManager.StartConnectionLoop();
        EventBus.Register(this);

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    [Subscribe]
    private void OnWorldLoaded(WorldLoadedEvent e)
    {
        _vibrationSystem = gameObject.AddComponent<VibrationSystem>();
        _vibrationSystem.StartPolling();
    }

    [Subscribe]
    private void OnWorldUnloading(WorldUnloadingEvent e)
    {
        ButtplugManager.StopAll();
        if (_vibrationSystem != null)
        {
            Destroy(_vibrationSystem);
            _vibrationSystem = null;
        }
    }

    private void OnDestroy()
    {
        EventBus.Unregister(this);
        ButtplugManager.StopConnectionLoop();
    }
}
