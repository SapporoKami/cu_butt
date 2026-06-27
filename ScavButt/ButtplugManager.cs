using Buttplug.Client;
using ScavLib.util;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ScavButt;

internal static class ButtplugManager
{
    private const string DefaultAddress = "ws://127.0.0.1:12345";
    private static readonly ButtplugClient _client = new ButtplugClient("ScavButt");
    private static CancellationTokenSource _cts = new CancellationTokenSource();

    internal static bool IsConnected => _client.Connected;
    internal static ButtplugClientDevice[] Devices => _client.Devices;

    internal static void StartConnectionLoop()
    {
        _client.DeviceAdded += (_, args) =>
        {
            Plugin.Log.LogInfo($"[ScavButt] Device added: {args.Device.Name}");
            GameUtil.Log($"[ScavButt] Device added: {args.Device.Name}");
        };
        _client.DeviceRemoved += (_, args) =>
        {
            Plugin.Log.LogInfo($"[ScavButt] Device removed: {args.Device.Name}");
            GameUtil.Log($"[ScavButt] Device removed: {args.Device.Name}");
        };
        _client.PingTimeout += (_, _) =>
        {
            Plugin.Log.LogWarning("[ScavButt] Ping timeout — reconnecting.");
            GameUtil.Log("[ScavButt] Ping timeout — reconnecting.");
        };
        _client.ServerDisconnect += (_, _) =>
        {
            Plugin.Log.LogInfo("[ScavButt] Server disconnected.");
            GameUtil.Log("[ScavButt] Server disconnected.");
        };
        _client.ScanningFinished += (_, __) =>
        {
            var msg = $"[ScavButt] Scan finished. {_client.Devices.Length} device(s) found.";
            Plugin.Log.LogInfo(msg);
            GameUtil.Log(msg);
        };

        RunLoop(_cts.Token);
    }

    private static void RunLoop(CancellationToken token)
    {
        Task.Run(async () =>
        {
            Plugin.Log.LogInfo($"[ScavButt] Connection loop started. Target: {DefaultAddress}");
            while (!token.IsCancellationRequested)
            {
                if (!_client.Connected)
                {
                    Plugin.Log.LogInfo($"[ScavButt] Connecting to {DefaultAddress}...");
                    try
                    {
                        await _client.ConnectAsync(new ButtplugWebsocketConnector(new Uri(DefaultAddress)), token);
                        var msg = $"[ScavButt] Connected to Intiface. {_client.Devices.Length} device(s) available.";
                        Plugin.Log.LogInfo(msg);
                        GameUtil.Log(msg);
                        try
                        {
                            await _client.StartScanningAsync(token);
                            Plugin.Log.LogInfo("[ScavButt] Scanning started.");
                            GameUtil.Log("[ScavButt] Scanning for devices...");
                        }
                        catch (Exception ex)
                        {
                            var err = $"[ScavButt] StartScanning failed: {ex.Message}";
                            Plugin.Log.LogWarning(err);
                            GameUtil.Log(err);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        var err = $"[ScavButt] Connect failed: {ex.Message} — retrying in 2s.";
                        Plugin.Log.LogWarning(err);
                        GameUtil.Log(err);
                        try { await Task.Delay(2000, token); } catch { break; }
                    }
                }
                else
                {
                    try { await Task.Delay(500, token); } catch { break; }
                }
            }
            Plugin.Log.LogInfo("[ScavButt] Connection loop stopped.");
        });
    }

    private static void RestartLoop()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        if (_client.Connected)
            Task.Run(async () =>
            {
                try { await _client.DisconnectAsync(); }
                catch (Exception ex) { Plugin.Log.LogWarning($"[ScavButt] Disconnect error: {ex.Message}"); }
            });
        RunLoop(_cts.Token);
    }

    internal static void Connect() => RestartLoop();

    internal static void Disconnect()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        if (_client.Connected)
            Task.Run(async () =>
            {
                try { await _client.DisconnectAsync(); }
                catch (Exception ex) { Plugin.Log.LogWarning($"[ScavButt] Disconnect error: {ex.Message}"); }
            });
    }

    internal static void StopConnectionLoop() => Disconnect();

    internal static void StartScanning()
    {
        if (!_client.Connected) { Plugin.Log.LogWarning("[ScavButt] StartScanning: not connected."); return; }
        Task.Run(async () =>
        {
            try { await _client.StartScanningAsync(); Plugin.Log.LogInfo("[ScavButt] Scanning started."); }
            catch (Exception ex) { Plugin.Log.LogWarning($"[ScavButt] StartScanning failed: {ex.Message}"); }
        });
    }

    internal static void StopScanning()
    {
        if (!_client.Connected) return;
        Task.Run(async () =>
        {
            try { await _client.StopScanningAsync(); Plugin.Log.LogInfo("[ScavButt] Scanning stopped."); }
            catch (Exception ex) { Plugin.Log.LogWarning($"[ScavButt] StopScanning failed: {ex.Message}"); }
        });
    }

    internal static void StopAll()
    {
        if (!_client.Connected) return;
        Task.Run(async () =>
        {
            try { await _client.StopAllDevicesAsync(); }
            catch (Exception ex) { Plugin.Log.LogWarning($"[ScavButt] StopAll failed: {ex.Message}"); }
        });
    }

    internal static void Vibrate(double intensity, int durationMs = 200)
    {
        if (!_client.Connected) return;
        intensity = Math.Max(0.0, Math.Min(1.0, intensity));

        Task.Run(async () =>
        {
            foreach (var device in _client.Devices)
            {
                if (device.VibrateAttributes.Count == 0) continue;
                try
                {
                    await device.VibrateAsync(intensity);
                    await Task.Delay(durationMs);
                    await device.Stop();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[ScavButt] Vibrate failed on {device.Name}: {ex.Message}");
                }
            }
        });
    }
}
