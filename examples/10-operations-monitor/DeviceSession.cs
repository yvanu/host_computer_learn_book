using System.IO;
using System.Net.Sockets;

namespace HostComputer.IndustrialMonitor;

/// <summary>
/// One device, one sequential FC03 polling loop. Max 3 reconnect attempts
/// at 1/2/4 seconds. Never automatically retry writes or protocol-invalid frames.
/// Run from the WPF UI synchronization context so callbacks can update Binding.
/// </summary>
public sealed class DeviceSession
{
    private readonly DevicePanel _panel;
    private readonly SqliteHistoryStore _store;
    private readonly Action<DevicePanel, DeviceReading> _onReading;
    private readonly Action<DevicePanel, string> _onLog;
    private readonly Action _onStopped;
    private CancellationTokenSource? _stop;
    private Task? _active;
    private ModbusDeviceClient? _client;

    public bool IsRunning => _active is { IsCompleted: false };
    public DeviceSession(DevicePanel panel, SqliteHistoryStore store,
        Action<DevicePanel, DeviceReading> onReading, Action<DevicePanel, string> onLog,
        Action onStopped)
    {
        _panel = panel;
        _store = store;
        _onReading = onReading;
        _onLog = onLog;
        _onStopped = onStopped;
    }

    public void Start()
    {
        if (IsRunning) return;
        _stop = new CancellationTokenSource();
        _active = PollAsync(_stop.Token);
    }

    private async Task PollAsync(CancellationToken token)
    {
        int failures = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var client = new ModbusDeviceClient(_panel.Port);
                _client = client;
                try
                {
                    _panel.SetStatus("连接中…");
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    await client.ConnectAsync(timeout.Token);
                    _panel.SetStatus("在线 · 实时采集");
                    await _store.WriteAuditAsync(_panel.Id, "connected", "FC03 polling connected");
                    _onLog(_panel, "连接成功，开始 FC03 周期采集");

                    while (true)
                    {
                        DeviceReading raw = await client.ReadAsync(token);
                        // The alarm threshold belongs to the specific device.
                        DeviceReading actual = _panel.Receive(raw, s => _onLog(_panel, s));
                        await _store.AddAsync(actual, token);
                        if (_panel.LastAlarmTransition is bool alarmEntered)
                            await _store.RecordTransitionAsync(actual, _panel.Threshold, alarmEntered);
                        _onReading(_panel, actual);
                        failures = 0; // a successful sample proves the connection recovered
                        await Task.Delay(1000, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (InvalidDataException ex) // do not retry malformed protocol frames
                {
                    _panel.SetStatus("协议错误 · 请检查报文");
                    _onLog(_panel, "已停止自动重连：" + ex.Message);
                    break;
                }
                catch (Exception ex) when (ex is SocketException or IOException or
                    OperationCanceledException or ObjectDisposedException)
                {
                    if (token.IsCancellationRequested) break;
                    if (++failures > 3)
                    {
                        _panel.SetStatus("离线 · 重试已耗尽");
                        _onLog(_panel, "三次自动重连失败，可点击连接再次尝试：" + ex.Message);
                        break;
                    }
                    client.Dispose(); // dispose broken socket before backoff
                    int seconds = 1 << (failures - 1);
                    _panel.SetStatus($"掉线，{seconds} 秒后重连（{failures}/3）");
                    _onLog(_panel, $"通信异常，准备第 {failures} 次重连：" + ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(seconds), token);
                }
                finally
                {
                    _client = null;
                    _panel.MarkOffline();
                    await _store.CloseOnDisconnectAsync(_panel.Id);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _panel.SetStatus("采集停止 · 内部错误");
            _onLog(_panel, ex.Message);
        }
        finally
        {
            if (token.IsCancellationRequested) _panel.SetStatus("已手动断开");
            _stop?.Dispose();
            _stop = null;
            _active = null;
            _client = null;
            _onStopped();
        }
    }

    public void Stop()
    {
        _stop?.Cancel();
        _client?.Dispose(); // releases blocked TCP read
    }

    public async Task StopAsync()
    {
        var task = _active;
        Stop();
        if (task is not null) await task;
    }
}
