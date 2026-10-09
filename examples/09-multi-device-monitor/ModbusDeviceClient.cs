using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;

namespace HostComputer.IndustrialMonitor;

/// <summary>
/// Read-only Modbus TCP for two local teaching PLCs, ports 1502/1503.
/// Each session owns its own TcpClient and transaction counter.
/// </summary>
public sealed class ModbusDeviceClient : IDisposable
{
    private readonly int _port;
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private ushort _nextTransaction;

    public ModbusDeviceClient(int port)
    {
        if (port is not (1502 or 1503))
            throw new ArgumentOutOfRangeException(nameof(port), "仅允许本机模拟器端口 1502/1503");
        _port = port;
    }

    public async Task ConnectAsync(CancellationToken token)
    {
        await _client.ConnectAsync("127.0.0.1", _port, token);
        _stream = _client.GetStream();
    }

    public async Task<DeviceReading> ReadAsync(CancellationToken token)
    {
        if (_stream is null) throw new InvalidOperationException("尚未连接");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        byte[] request = new byte[12];
        ushort tx = unchecked(++_nextTransaction);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), tx);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6);
        request[6] = 1; // Unit ID
        request[7] = 3; // FC03 - READ ONLY
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), 3);
        await _stream.WriteAsync(request, timeout.Token);

        byte[] header = new byte[7];
        await _stream.ReadExactlyAsync(header, timeout.Token);
        ushort responseTx = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
        ushort protocol = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        if (tx != responseTx || protocol != 0 || header[6] != 1 || length < 2 || length > 254)
            throw new InvalidDataException("MBAP 事务号、协议号、设备号或长度异常");

        byte[] pdu = new byte[length - 1];
        await _stream.ReadExactlyAsync(pdu, timeout.Token);
        if (pdu.Length == 2 && pdu[0] == 0x83)
            throw new InvalidDataException($"Modbus 异常码 0x{pdu[1]:X2}");
        if (pdu.Length != 8 || pdu[0] != 3 || pdu[1] != 6)
            throw new InvalidDataException("FC03 应答长度或功能码异常");

        return new DeviceReading(
            "", DateTimeOffset.UtcNow,
            BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(2, 2)) / 10.0,
            BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(4, 2)) / 10.0,
            BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(6, 2)),
            false);
    }

    public void Dispose() => _client.Dispose();
}
