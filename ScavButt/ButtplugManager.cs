using Buttplug.Client;
using Buttplug.Core.Messages;
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
            Plugin.Log.LogInfo($"[ScavButt] Device added: {args.Device.Name}");
        _client.DeviceRemoved += (_, args) =>
            Plugin.Log.LogInfo($"[ScavButt] Device removed: {args.Device.Name}");
        _client.PingTimeout += (_, _) =>
            Plugin.Log.LogWarning("[ScavButt] Ping timeout.");

        RestartLoop();
    }

    private static void RestartLoop()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (!_client.Connected)
                {
                    try
                    {
                        await _client.ConnectAsync(DefaultAddress);
                        Plugin.Log.LogInfo("[ScavButt] Connected to Intiface Central.");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        try { await Task.Delay(2000, token); } catch { break; }
                    }
                }
                else
                {
                    var disconnected = new TaskCompletionSource<bool>();
                    _client.ServerDisconnect += (_, _) => disconnected.TrySetResult(true);
                    await disconnected.Task.ConfigureAwait(false);
                    Plugin.Log.LogInfo("[ScavButt] Disconnected from Intiface Central.");
                }
            }
        });
    }

    // Force an immediate reconnect attempt (restart the loop).
    internal static void Connect() => RestartLoop();

    // Disconnect and stop the loop. Auto-connect will not resume until Connect() is called.
    internal static void Disconnect()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        if (_client.Connected)
            Task.Run(async () => await _client.DisconnectAsync());
    }

    internal static void StopConnectionLoop() => Disconnect();

    internal static void StartScanning()
    {
        if (!_client.Connected) return;
        Task.Run(async () => await _client.StartScanningAsync());
    }

    internal static void StopScanning()
    {
        if (!_client.Connected) return;
        Task.Run(async () => await _client.StopScanningAsync());
    }

    internal static void StopAll()
    {
        if (!_client.Connected) return;
        Task.Run(async () => await _client.StopAllDevicesAsync());
    }

    // Fire-and-forget vibration; safe to call from Unity's main thread.
    internal static void Vibrate(double intensity, int durationMs = 200)
    {
        if (!_client.Connected) return;
        intensity = Math.Max(0.0, Math.Min(1.0, intensity));

        Task.Run(async () =>
        {
            foreach (var device in _client.Devices)
            {
                if (device.HasOutput(OutputType.Vibrate))
                {
                    await device.RunOutputAsync(DeviceOutput.Vibrate.Percent(intensity));
                    await Task.Delay(durationMs);
                    await device.StopAsync();
                }
            }
        });
    }
}
