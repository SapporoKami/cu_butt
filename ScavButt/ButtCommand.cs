using ScavLib.command;
using ScavLib.util;
using System.Collections.Generic;

namespace ScavButt;

public class ButtCommand : BaseCommand
{
    public override string Name => "butt";
    public override string Description => "ScavButt — buttplug.io integration. Usage: butt <connect|disconnect|scan|stopscan|devices|stop|test|status>";

    public override void Execute(string[] args)
    {
        string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "status";

        switch (sub)
        {
            case "status":     ExecuteStatus();     break;
            case "connect":    ExecuteConnect();    break;
            case "disconnect": ExecuteDisconnect(); break;
            case "scan":       ExecuteScan();       break;
            case "stopscan":   ExecuteStopScan();   break;
            case "devices":    ExecuteDevices();    break;
            case "stop":       ExecuteStop();       break;
            case "test":       ExecuteTest();       break;
            default:
                GameUtil.Log($"[ScavButt] Unknown subcommand '{args[1]}'. Commands: connect, disconnect, scan, stopscan, devices, stop, status");
                break;
        }
    }

    private static void ExecuteStatus()
    {
        GameUtil.Log("[ScavButt] === ScavButt Status ===");
        GameUtil.Log($"[ScavButt] Version  : {Plugin.PluginVersion}");
        GameUtil.Log($"[ScavButt] Intiface : {(ButtplugManager.IsConnected ? "connected" : "disconnected")}");
        GameUtil.Log($"[ScavButt] Devices  : {ButtplugManager.Devices.Length}");
    }

    private static void ExecuteConnect()
    {
        GameUtil.Log("[ScavButt] (Re)connecting to Intiface Central...");
        ButtplugManager.Connect();
    }

    private static void ExecuteDisconnect()
    {
        GameUtil.Log("[ScavButt] Disconnecting from Intiface Central.");
        ButtplugManager.Disconnect();
    }

    private static void ExecuteScan()
    {
        if (!ButtplugManager.IsConnected) { GameUtil.Log("[ScavButt] Not connected."); return; }
        GameUtil.Log("[ScavButt] Scanning for devices...");
        ButtplugManager.StartScanning();
    }

    private static void ExecuteStopScan()
    {
        if (!ButtplugManager.IsConnected) { GameUtil.Log("[ScavButt] Not connected."); return; }
        GameUtil.Log("[ScavButt] Stopping scan.");
        ButtplugManager.StopScanning();
    }

    private static void ExecuteDevices()
    {
        var devices = ButtplugManager.Devices;
        if (devices.Length == 0) { GameUtil.Log("[ScavButt] No devices connected."); return; }

        GameUtil.Log($"[ScavButt] {devices.Length} device(s):");
        foreach (var device in devices)
        {
            var outputs = new List<string>();
            if (device.VibrateAttributes.Count > 0)   outputs.Add("Vibrate");
            if (device.RotateAttributes.Count > 0)    outputs.Add("Rotate");
            if (device.OscillateAttributes.Count > 0) outputs.Add("Oscillate");
            if (device.LinearAttributes.Count > 0)    outputs.Add("Linear");

            var inputs = new List<string>();
            if (device.HasBattery) inputs.Add("Battery");

            GameUtil.Log($"[ScavButt]   {device.Name}");
            if (outputs.Count > 0) GameUtil.Log($"[ScavButt]     Outputs: {string.Join(", ", outputs)}");
            if (inputs.Count  > 0) GameUtil.Log($"[ScavButt]     Inputs:  {string.Join(", ", inputs)}");
        }
    }

    private static void ExecuteTest()
    {
        if (!ButtplugManager.IsConnected) { GameUtil.Log("[ScavButt] Not connected."); return; }
        GameUtil.Log("[ScavButt] Test vibration.");
        ButtplugManager.Vibrate(0.01, durationMs: 300);
    }

    private static void ExecuteStop()
    {
        if (!ButtplugManager.IsConnected) { GameUtil.Log("[ScavButt] Not connected."); return; }
        GameUtil.Log("[ScavButt] Stopping all devices.");
        ButtplugManager.StopAll();
    }
}
