using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;

namespace HostComputer.IndustrialMonitor;

/// <summary>
/// Beginner Modbus TCP client restricted to the local teaching simulator.
/// Not a generic PLC controller. One sequential request at a time.
/// </summary>
public sealed class ModbusDeviceClient : IDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private ushort _nextTransaction;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _client.ConnectAsync("127.0.0.1", 1502, cancellationToken);
        _stream = _client.GetStream();
    }

    public async Task<DeviceReading> ReadAsync(CancellationToken cancellationToken)
    {
        if (_stream is null) throw new InvalidOperationException("设备尚未连接。");

        // Only this locally simulated FC03 request is supported; no writes to a PLC.
        byte[] request = new byte[12];
        ushort transaction = unchecked(++_nextTransaction);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transaction);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6);
        request[6] = 1; // Unit ID
        request[7] = 3; // FC03: holding registers
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), 0); // offset 0
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), 3); // count 3

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await _stream.WriteAsync(request, timeout.Token);

        // TCP is a byte stream: do not confuse one Read() with one Modbus frame.
        byte[] header = new byte[7];
        await _stream.ReadExactlyAsync(header, timeout.Token);
        ushort responseTransaction = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
        ushort protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        if (responseTransaction != transaction || protocolId != 0 ||
            header[6] != 1 || length < 2 || length > 254)
            throw new InvalidDataException("MBAP 响应头不合法。");

        byte[] pdu = new byte[length - 1]; // Unit ID is already in the 7-byte header
        await _stream.ReadExactlyAsync(pdu, timeout.Token);
        if (pdu.Length == 2 && pdu[0] == 0x83)
            throw new InvalidDataException($"设备返回 FC03 异常码 0x{pdu[1]:X2}。");
        if (pdu.Length != 8 || pdu[0] != 3 || pdu[1] != 6)
            throw new InvalidDataException("保持寄存器响应的数据长度或功能码不符。");

        ushort rawTemp = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(2, 2));
        ushort rawPressure = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(4, 2));
        ushort rawRpm = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(6, 2));
        return new DeviceReading(DateTimeOffset.UtcNow, rawTemp / 10.0, rawPressure / 10.0, rawRpm);
    }

    public void Dispose() => _client.Dispose(); // forced close also unblocks pending reads
}
