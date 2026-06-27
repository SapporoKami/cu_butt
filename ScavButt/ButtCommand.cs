using ScavLib.command;
using ScavLib.util;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ScavButt;

public class ButtCommand : BaseCommand
{
    public override string Name => "butt";
    public override string Description => "ScavButt — buttplug.io integration";

    // ── Settings registry ────────────────────────────────────────────────────
    // To expose a new knob: add one entry here. Both 'butt set' and 'butt settings'
    // are driven entirely from this array — no other changes needed.

    private sealed class Setting
    {
        public readonly string Group;
        public readonly string Description;
        public readonly Func<string>   Get;
        public readonly Action<string> Set;
        public Setting(string group, string desc, Func<string> get, Action<string> set)
        { Group = group; Description = desc; Get = get; Set = set; }
    }

    // Ordered — 'butt settings' prints in declaration order, grouped by Group name.
    private static readonly (string Key, Setting S)[] _defs =
    {
        // ── global ───────────────────────────────────────────────────────────
        ("enable",         new Setting("global",     "Master on/off (true/false)",
                               () => VibrationSettings.EnableVibration.ToString().ToLower(),
                               v  => VibrationSettings.EnableVibration = ParseBool(v))),
        ("intensity",      new Setting("global",     "Hardware intensity cap 0–100 (%); bypasses Lovense app limits",
                               () => VibrationSettings.MaxDeviceIntensity.ToString("F1"),
                               v  => VibrationSettings.MaxDeviceIntensity = ParseFlt(v, 0, 100))),
        ("scale",          new Setting("global",     "Global waveform output scale 0.0–1.0 (pre-hardware)",
                               () => VibrationSettings.GlobalScale.ToString("F2"),
                               v  => VibrationSettings.GlobalScale = ParseFlt(v, 0, 1))),
        ("poll-hz",        new Setting("global",     "Device command rate 1–60 Hz (real-time flush window; game-time sampling stays at 20 Hz)",
                               () => VibrationSettings.PollHz.ToString("F1"),
                               v  => VibrationSettings.PollHz = ParseFlt(v, 1, 60))),
        ("ff-threshold",   new Setting("global",     "Time.timeScale above which fast-forward behaviour activates (default 1.5)",
                               () => VibrationSettings.FastForwardThreshold.ToString("F2"),
                               v  => VibrationSettings.FastForwardThreshold = ParseFlt(v, 1, 20))),
        ("silence-ff",     new Setting("global",     "Silence vibrations entirely during fast-forward (true/false); false = dampened output instead",
                               () => VibrationSettings.SilenceOnFastForward.ToString().ToLower(),
                               v  => VibrationSettings.SilenceOnFastForward = ParseBool(v))),

        // ── effects ──────────────────────────────────────────────────────────
        ("heartbeat",      new Setting("effects",    "Heartbeat component on/off",
                               () => VibrationSettings.EnableHeartbeat.ToString().ToLower(),
                               v  => VibrationSettings.EnableHeartbeat = ParseBool(v))),
        ("bleed",          new Setting("effects",    "Bleed component on/off",
                               () => VibrationSettings.EnableBleed.ToString().ToLower(),
                               v  => VibrationSettings.EnableBleed = ParseBool(v))),
        ("pain",           new Setting("effects",    "Pain/shock component on/off",
                               () => VibrationSettings.EnablePainShock.ToString().ToLower(),
                               v  => VibrationSettings.EnablePainShock = ParseBool(v))),
        ("horror",         new Setting("effects",    "Horror component on/off",
                               () => VibrationSettings.EnableHorror.ToString().ToLower(),
                               v  => VibrationSettings.EnableHorror = ParseBool(v))),
        ("fibril",         new Setting("effects",    "Fibrillation component on/off",
                               () => VibrationSettings.EnableFibrillation.ToString().ToLower(),
                               v  => VibrationSettings.EnableFibrillation = ParseBool(v))),
        ("impact",         new Setting("effects",    "Impact transients on/off",
                               () => VibrationSettings.EnableImpact.ToString().ToLower(),
                               v  => VibrationSettings.EnableImpact = ParseBool(v))),
        ("trauma",         new Setting("effects",    "Trauma component on/off",
                               () => VibrationSettings.EnableTrauma.ToString().ToLower(),
                               v  => VibrationSettings.EnableTrauma = ParseBool(v))),

        // ── heartbeat ────────────────────────────────────────────────────────
        ("hr-low",         new Setting("heartbeat",  "HR below this triggers bradycardia vibration (BPM)",
                               () => VibrationSettings.HrFeelLow.ToString("F1"),
                               v  => VibrationSettings.HrFeelLow = ParseFlt(v, 0, 300))),
        ("hr-high",        new Setting("heartbeat",  "HR above this triggers tachycardia vibration (BPM)",
                               () => VibrationSettings.HrFeelHigh.ToString("F1"),
                               v  => VibrationSettings.HrFeelHigh = ParseFlt(v, 0, 300))),
        ("amp-brady",      new Setting("heartbeat",  "Bradycardia peak amplitude 0–1",
                               () => VibrationSettings.AmpHeartbeatBrady.ToString("F2"),
                               v  => VibrationSettings.AmpHeartbeatBrady = ParseFlt(v, 0, 1))),
        ("amp-tachy",      new Setting("heartbeat",  "Tachycardia peak amplitude 0–1",
                               () => VibrationSettings.AmpHeartbeatTachy.ToString("F2"),
                               v  => VibrationSettings.AmpHeartbeatTachy = ParseFlt(v, 0, 1))),
        ("amp-arrest",     new Setting("heartbeat",  "Cardiac arrest amplitude 0–1",
                               () => VibrationSettings.AmpHeartbeatArrest.ToString("F2"),
                               v  => VibrationSettings.AmpHeartbeatArrest = ParseFlt(v, 0, 1))),

        // ── bleed ────────────────────────────────────────────────────────────
        ("bleed-onset",    new Setting("bleed",      "Bleed speed where vibration starts",
                               () => VibrationSettings.BleedOnset.ToString("F3"),
                               v  => VibrationSettings.BleedOnset = ParseFlt(v, 0, 9999))),
        ("bleed-ceil",     new Setting("bleed",      "Bleed speed where amplitude reaches max",
                               () => VibrationSettings.BleedCeiling.ToString("F3"),
                               v  => VibrationSettings.BleedCeiling = ParseFlt(v, 0, 9999))),
        ("bleed-max-amp",  new Setting("bleed",      "Max bleed amplitude 0–1 (cap so bleed never saturates alone)",
                               () => VibrationSettings.BleedMaxAmp.ToString("F2"),
                               v  => VibrationSettings.BleedMaxAmp = ParseFlt(v, 0, 1))),

        // ── amplitudes ───────────────────────────────────────────────────────
        ("pain-onset",     new Setting("amplitudes", "Raw pain value where vibration starts (0–80; PAIN_MILD=10, PAIN_MODERATE=30)",
                               () => VibrationSettings.PainOnset.ToString("F1"),
                               v  => VibrationSettings.PainOnset = ParseFlt(v, 0, 80))),
        ("amp-pain",       new Setting("amplitudes", "Pain/shock peak amplitude 0–1",
                               () => VibrationSettings.AmpPainShock.ToString("F2"),
                               v  => VibrationSettings.AmpPainShock = ParseFlt(v, 0, 1))),
        ("amp-horror",     new Setting("amplitudes", "Horror peak amplitude 0–1",
                               () => VibrationSettings.AmpHorror.ToString("F2"),
                               v  => VibrationSettings.AmpHorror = ParseFlt(v, 0, 1))),
        ("amp-fibril",     new Setting("amplitudes", "Fibrillation peak amplitude 0–1",
                               () => VibrationSettings.AmpFibrillation.ToString("F2"),
                               v  => VibrationSettings.AmpFibrillation = ParseFlt(v, 0, 1))),
        ("amp-trauma",     new Setting("amplitudes", "Trauma peak amplitude 0–1",
                               () => VibrationSettings.AmpTrauma.ToString("F2"),
                               v  => VibrationSettings.AmpTrauma = ParseFlt(v, 0, 1))),
    };

    // ── Parse helpers ────────────────────────────────────────────────────────
    private static bool ParseBool(string v)
    {
        v = v.ToLower();
        if (v is "true"  or "on"  or "1" or "yes") return true;
        if (v is "false" or "off" or "0" or "no")  return false;
        throw new FormatException($"'{v}' is not a valid bool — use true/false");
    }

    private static float ParseFlt(string v, float min, float max)
    {
        if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            throw new FormatException($"'{v}' is not a valid number");
        if (f < min || f > max)
            throw new ArgumentOutOfRangeException(nameof(v), $"Value must be {min}–{max}");
        return f;
    }

    // ── Subcommand routing ───────────────────────────────────────────────────
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
        { "viz",        new VizSub() },
        { "set",        new SetSub() },
        { "settings",   new SettingsSub() },
    };

    public override Dictionary<string, BaseCommand> SubCommands => _subs;
    public override void Execute(string[] args) => ExecuteSubCommand(args, subArgIndex: 1);

    // ── Subcommand implementations ───────────────────────────────────────────

    private class StatusSub : BaseCommand
    {
        public override string Name => "status";
        public override string Description => "Show connection state and device count";
        public override void Execute(string[] args)
        {
            LogLine("[ScavButt] === ScavButt Status ===");
            LogLine($"[ScavButt] Version   : {Plugin.PluginVersion}");
            LogLine($"[ScavButt] Intiface  : {(ButtplugManager.IsConnected ? "connected" : "disconnected")}");
            LogLine($"[ScavButt] Devices   : {ButtplugManager.Devices.Length}");
            LogLine($"[ScavButt] Intensity : {VibrationSettings.MaxDeviceIntensity:F1}%  (butt set intensity <0-100>)");
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
        public override string Description => "Trigger a test pulse through the wave system (visible in viz, vibrates if connected)";
        public override void Execute(string[] args)
        {
            VibrationSystem.TriggerTest();
            LogLine($"[ScavButt] Test pulse triggered. (intensity cap: {VibrationSettings.MaxDeviceIntensity:F1}%)");
        }
    }

    private class VizSub : BaseCommand
    {
        public override string Name => "viz";
        public override string Description => "Toggle the wave visualizer overlay  |  butt viz poll — toggle poll-window markers on the graph";
        public override void Execute(string[] args)
        {
            if (args.Length >= 3 && args[2].ToLower() is "--help" or "help" or "?")
            {
                LogLine("[ScavButt] butt viz — wave visualizer overlay");
                LogLine("[ScavButt]   butt viz           Toggle the oscilloscope overlay on/off");
                LogLine("[ScavButt]   butt viz poll      Toggle green poll-window markers on the graph");
                LogLine("[ScavButt]                        Each green line marks where a flush was sent to the device.");
                LogLine("[ScavButt]                        Dense lines = normal speed; sparse lines = fast-forward batching.");
                LogLine("[ScavButt]   butt viz --help    Show this help");
                return;
            }
            if (args.Length >= 3 && args[2].ToLower() == "poll")
            {
                VibrationSystem.ShowPollMarkers = !VibrationSystem.ShowPollMarkers;
                if (VibrationSystem.ShowPollMarkers && !VibrationSystem.ShowDebug)
                {
                    VibrationSystem.ShowDebug = true;
                    LogLine("[ScavButt] Visualizer ON");
                }
                LogLine($"[ScavButt] Poll-window markers {(VibrationSystem.ShowPollMarkers ? "ON" : "OFF")}");
                return;
            }
            VibrationSystem.ShowDebug = !VibrationSystem.ShowDebug;
            LogLine($"[ScavButt] Visualizer {(VibrationSystem.ShowDebug ? "ON" : "OFF")}");
        }
    }

    // ── butt set <key> [value] ───────────────────────────────────────────────
    private class SetSub : BaseCommand
    {
        public override string Name => "set";
        public override string Description => "Set a vibration setting  (butt set <key> <value>)";

        public override void Execute(string[] args)
        {
            // args layout: ["butt", "set", <key>, <value>]
            if (args.Length < 3) { PrintHelp(); return; }

            string key = args[2].ToLower();
            if (key is "--help" or "help" or "?") { PrintHelp(); return; }

            if (args.Length < 4)
            {
                // query only — print current value
                Setting? found = Lookup(key);
                if (found == null) { LogLine($"[ScavButt] Unknown setting '{key}'. Run 'butt set' for the full list."); return; }
                LogLine($"[ScavButt] {key} = {found.Get()}   ({found.Description})");
                return;
            }

            Setting? s = Lookup(key);
            if (s == null) { LogLine($"[ScavButt] Unknown setting '{key}'. Run 'butt set' for the full list."); return; }

            try
            {
                s.Set(args[3]);
                LogLine($"[ScavButt] {key} = {s.Get()}");
            }
            catch (Exception ex)
            {
                LogLine($"[ScavButt] Error: {ex.Message}");
            }
        }

        private void PrintHelp()
        {
            LogLine("[ScavButt] Usage: butt set <key> <value>");
            string group = "";
            foreach (var (key, s) in ButtCommand._defs)
            {
                if (s.Group != group) { group = s.Group; LogLine($"[ScavButt]  [{group}]"); }
                LogLine($"[ScavButt]    {key,-22}  {s.Description}");
            }
        }

        private static Setting? Lookup(string key)
        {
            foreach (var (k, s) in ButtCommand._defs)
                if (k == key) return s;
            return null;
        }
    }

    // ── butt settings ────────────────────────────────────────────────────────
    private class SettingsSub : BaseCommand
    {
        public override string Name => "settings";
        public override string Description => "List all vibration settings and their current values";

        public override void Execute(string[] args)
        {
            LogLine("[ScavButt] === Vibration Settings ===");
            string group = "";
            foreach (var (key, s) in ButtCommand._defs)
            {
                if (s.Group != group) { group = s.Group; LogLine($"[ScavButt]  [{group}]"); }
                LogLine($"[ScavButt]    {key,-22} = {s.Get(),-8}  {s.Description}");
            }
        }
    }
}
