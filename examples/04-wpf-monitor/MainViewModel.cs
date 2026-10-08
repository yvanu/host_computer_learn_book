using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace HostComputer.WpfMonitor;

// 入门演示版：把通信放在 ViewModel，后续课程再抽离独立 DeviceSession 服务。
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private TcpClient? _client;
    private CancellationTokenSource? _stop;

    private string _status = "未连接";
    private string _temperature = "—";
    private string _pressure = "—";
    private string _rpm = "—";
    private string _lastUpdated = "尚未收到设备数据";

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public string Pressure { get => _pressure; private set => Set(ref _pressure, value); }
    public string Rpm { get => _rpm; private set => Set(ref _rpm, value); }
    public string LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }

    public MainViewModel()
    {
        ConnectCommand = new RelayCommand(() => _ = ConnectAndReadAsync(), () => _client is null);
        DisconnectCommand = new RelayCommand(Disconnect, () => _client is not null);
    }

    private async Task ConnectAndReadAsync()
    {
        if (_client is not null) return;
        var client = new TcpClient();
        using var stop = new CancellationTokenSource();
        _client = client;
        _stop = stop;
        Status = "连接中…";
        RefreshCommands();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync("127.0.0.1", 9000, timeout.Token);
            Status = "已连接";
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync(stop.Token)) is not null)
                UpdateReading(line);
            Status = "设备已断开";
        }
        catch (OperationCanceledException)
        {
            Status = "已断开";
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException)
        {
            Status = $"通信失败：{ex.Message}";
        }
        finally
        {
            client.Close();
            if (ReferenceEquals(_client, client))
            {
                _client = null;
                _stop = null;
                RefreshCommands();
            }
        }
    }

    private void UpdateReading(string frame)
    {
        // 本地教学文本协议：每行 TEMP=...;PRESSURE=...;RPM=...
        foreach (var entry in frame.Split(';'))
        {
            var pair = entry.Split('=', 2);
            if (pair.Length != 2) continue;
            switch (pair[0])
            {
                case "TEMP" when double.TryParse(pair[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var temp) && double.IsFinite(temp):
                    Temperature = temp.ToString("F1", CultureInfo.InvariantCulture);
                    break;
                case "PRESSURE" when double.TryParse(pair[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var pressure) && double.IsFinite(pressure):
                    Pressure = pressure.ToString("F1", CultureInfo.InvariantCulture);
                    break;
                case "RPM" when int.TryParse(pair[1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var rpm) && rpm >= 0:
                    Rpm = rpm.ToString(CultureInfo.InvariantCulture);
                    break;
            }
        }
        LastUpdated = $"最近收到数据：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    private void Disconnect()
    {
        _stop?.Cancel();
        _client?.Close();
        Status = "已断开";
    }

    private void RefreshCommands()
    {
        ConnectCommand.NotifyChanged();
        DisconnectCommand.NotifyChanged();
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void Dispose() => Disconnect();
}

public sealed class RelayCommand(Action execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute();
    public void Execute(object? parameter) => execute();
    public void NotifyChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
