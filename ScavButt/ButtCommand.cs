using ScavLib.command;
using ScavLib.util;
using System.Collections.Generic;

namespace ScavButt;

public class ButtCommand : BaseCommand
{
    public override string Name => "butt";
    public override string Description => "ScavButt — buttplug.io integration";

    private readonly Dictionary<string, BaseCommand> _subs = new Dictionary<string, BaseCommand>
    {
        { "connect",    new ConnectSub() },
        { "disconnect", new DisconnectSub() },
        { "scan",       new ScanSub() },
        { "stopscan",   new StopScanSub() },
        { "devices",    new DevicesSub() },
        { "stop",       new StopSub() },
        { "test",       new TestSub() },
        { "status",     new StatusSub() },
    };

    public override Dictionary<string, BaseCommand> SubCommands => _subs;

    public override void Execute(string[] args) => ExecuteSubCommand(args, subArgIndex: 1);

    // --- subcommands ---

    private class StatusSub : BaseCommand
    {
        public override string Name => "status";
        public override string Description => "Show connection state and device count";
        public override void Execute(string[] args)
        {
            LogLine("[ScavButt] === ScavButt Status ===");
            LogLine($"[ScavButt] Version  : {Plugin.PluginVersion}");
            LogLine($"[ScavButt] Intiface : {(ButtplugManager.IsConnected ? "connected" : "disconnected")}");
            LogLine($"[ScavButt] Devices  : {ButtplugManager.Devices.Length}");
        }
    }

    private class ConnectSub : BaseCommand
    {
        public override string Name => "connect";
        public override string Description => "(Re)connect to Intiface Central";
        public override void Execute(string[] args)
        {
            LogLine("[ScavButt] (Re)connecting to Intiface Central...");
            ButtplugManager.Connect();
        }
    }

    private class DisconnectSub : BaseCommand
    {
        public override string Name => "disconnect";
        public override string Description => "Disconnect from Intiface Central";
        public override void Execute(string[] args)
        {
            LogLine("[ScavButt] Disconnecting from Intiface Central.");
            ButtplugManager.Disconnect();
        }
    }

    private class ScanSub : BaseCommand
    {
        public override string Name => "scan";
        public override string Description => "Start scanning for devices";
        public override void Execute(string[] args)
        {
            if (!ButtplugManager.IsConnected) { LogLine("[ScavButt] Not connected."); return; }
            LogLine("[ScavButt] Scanning for devices...");
            ButtplugManager.StartScanning();
        }
    }

    private class StopScanSub : BaseCommand
    {
        public override string Name => "stopscan";
        public override string Description => "Stop scanning for devices";
        public override void Execute(string[] args)
        {
            if (!ButtplugManager.IsConnected) { LogLine("[ScavButt] Not connected."); return; }
            LogLine("[ScavButt] Stopping scan.");
            ButtplugManager.StopScanning();
        }
    }

    private class DevicesSub : BaseCommand
    {
        public override string Name => "devices";
        public override string Description => "List connected devices and their capabilities";
        public override void Execute(string[] args)
        {
            var devices = ButtplugManager.Devices;
            if (devices.Length == 0) { LogLine("[ScavButt] No devices connected."); return; }

            LogLine($"[ScavButt] {devices.Length} device(s):");
            foreach (var device in devices)
            {
                var outputs = new List<string>();
                if (device.VibrateAttributes.Count > 0)   outputs.Add("Vibrate");
                if (device.RotateAttributes.Count > 0)    outputs.Add("Rotate");
                if (device.OscillateAttributes.Count > 0) outputs.Add("Oscillate");
                if (device.LinearAttributes.Count > 0)    outputs.Add("Linear");

                var inputs = new List<string>();
                if (device.HasBattery) inputs.Add("Battery");

                LogLine($"[ScavButt]   {device.Name}");
                if (outputs.Count > 0) LogLine($"[ScavButt]     Outputs: {string.Join(", ", outputs)}");
                if (inputs.Count  > 0) LogLine($"[ScavButt]     Inputs:  {string.Join(", ", inputs)}");
            }
        }
    }

    private class StopSub : BaseCommand
    {
        public override string Name => "stop";
        public override string Description => "Stop all devices immediately";
        public override void Execute(string[] args)
        {
            if (!ButtplugManager.IsConnected) { LogLine("[ScavButt] Not connected."); return; }
            LogLine("[ScavButt] Stopping all devices.");
            ButtplugManager.StopAll();
        }
    }

    private class TestSub : BaseCommand
    {
        public override string Name => "test";
        public override string Description => "Brief test vibration (0.01 intensity, 300 ms)";
        public override void Execute(string[] args)
        {
            if (!ButtplugManager.IsConnected) { LogLine("[ScavButt] Not connected."); return; }
            LogLine("[ScavButt] Test vibration.");
            ButtplugManager.Vibrate(0.01, durationMs: 300);
        }
    }
}
