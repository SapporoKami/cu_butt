using BepInEx;
using BepInEx.Logging;
using ScavLib.command;
using ScavLib.event_bus;
using ScavLib.event_bus.events;
using ScavLib.util;

namespace ScavButt;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.kanisuko.scavlib", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.scavbutt.scavbutt";
    public const string PluginName = "ScavButt";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log = null!;

    private bool _worldLoaded = false;
    private float _lastShock = 0f;

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
        _lastShock = PlayerUtil.GetShock();
        _worldLoaded = true;
    }

    [Subscribe]
    private void OnWorldUnloading(WorldUnloadingEvent e)
    {
        _worldLoaded = false;
        ButtplugManager.StopAll();
    }

    private void Update()
    {
        if (!_worldLoaded) return;

        float shock = PlayerUtil.GetShock();
        float delta = shock - _lastShock;
        _lastShock = shock;

        if (delta > 1f)
            ButtplugManager.Vibrate(0.01, durationMs: 300);
    }

    private void OnDestroy()
    {
        EventBus.Unregister(this);
        ButtplugManager.StopConnectionLoop();
    }
}
