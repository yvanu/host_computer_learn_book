using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;

// Training client: hardcoded localhost only. Real devices require safety review.
if (args.Length != 0 &&
    !(args.Length == 2 && args[0] == "write-rpm" &&
      ushort.TryParse(args[1], out var speed) && speed <= 3000))
{
    Console.Error.WriteLine("用法: dotnet run --project examples/06-modbus-tcp-client [-- write-rpm 1500]");
    return;
}

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    using var client = new TcpClient();
    await client.ConnectAsync("127.0.0.1", 1502, timeout.Token);
    using var stream = client.GetStream();

    // The simulator exposes read-only temperature/pressure and a writable RPM register.
    var readings = await ExchangeAsync(stream, 1, 0x03, 0, 3, timeout.Token);
    PrintReadings(readings);

    if (args.Length == 2)
    {
        ushort rpm = ushort.Parse(args[1]);
        var result = await ExchangeAsync(stream, 2, 0x06, 2, rpm, timeout.Token);
        if (result.Length != 5 || result[0] != 0x06 ||
            BinaryPrimitives.ReadUInt16BigEndian(result.AsSpan(1, 2)) != 2 ||
            BinaryPrimitives.ReadUInt16BigEndian(result.AsSpan(3, 2)) != rpm)
            throw new InvalidDataException("FC06 回复未原样确认写入地址和值。");

        Console.WriteLine($"FC06 写入确认: RPM={rpm}（仅本地模拟设备）");
        var verified = await ExchangeAsync(stream, 3, 0x03, 2, 1, timeout.Token);
        ushort actual = ParseRegisters(verified, 1)[0];
        Console.WriteLine($"FC03 回读确认: RPM={actual}");
        if (actual != rpm)
            throw new InvalidDataException("回读值与写入值不一致。");
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("连接或读写超时（5秒）；请确认先启动模拟器。");
    Environment.ExitCode = 1;
}
catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException)
{
    Console.Error.WriteLine($"通信/协议错误：{ex.Message}");
    Environment.ExitCode = 1;
}

static async Task<byte[]> ExchangeAsync(
    NetworkStream stream, ushort transaction, byte function,
    ushort address, ushort value, CancellationToken ct)
{
    byte[] request = new byte[12];
    BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transaction);
    // Protocol ID at [2..4] stays zero.
    BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6); // Unit(1) + PDU(5)
    request[6] = 1; // Unit ID
    request[7] = function;
    BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), address);
    BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), value);

    Console.WriteLine($"> {Convert.ToHexString(request)}");
    await stream.WriteAsync(request, ct);

    byte[] mbap = new byte[7];
    await stream.ReadExactlyAsync(mbap, ct); // exactly 7 bytes, not "one Read == one packet"
    ushort replyTransaction = BinaryPrimitives.ReadUInt16BigEndian(mbap.AsSpan(0, 2));
    ushort protocol = BinaryPrimitives.ReadUInt16BigEndian(mbap.AsSpan(2, 2));
    ushort length = BinaryPrimitives.ReadUInt16BigEndian(mbap.AsSpan(4, 2));
    if (replyTransaction != transaction || protocol != 0 ||
        mbap[6] != 1 || length < 2 || length > 254)
        throw new InvalidDataException("MBAP 事务号、协议号、设备号或长度不合法。");

    byte[] pdu = new byte[length - 1]; // Length includes the Unit ID already read.
    await stream.ReadExactlyAsync(pdu, ct);
    Console.WriteLine($"< {Convert.ToHexString(mbap)}{Convert.ToHexString(pdu)}");

    if (pdu.Length == 2 && pdu[0] == (function | 0x80))
        throw new InvalidDataException($"设备返回 Modbus 异常码 0x{pdu[1]:X2}。");
    if (pdu[0] != function)
        throw new InvalidDataException("响应功能码与请求不一致。");
    return pdu;
}

static ushort[] ParseRegisters(byte[] response, int expectedCount)
{
    int expectedBytes = expectedCount * 2;
    if (response.Length != 2 + expectedBytes || response[0] != 3 ||
        response[1] != expectedBytes)
        throw new InvalidDataException("FC03 字节数/寄存器数量不一致。");

    ushort[] values = new ushort[expectedCount];
    for (int i = 0; i < expectedCount; i++)
        values[i] = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2 + 2 * i, 2));
    return values;
}

static void PrintReadings(byte[] pdu)
{
    var registers = ParseRegisters(pdu, 3);
    Console.WriteLine($"温度: {registers[0] / 10.0:F1} °C");
    Console.WriteLine($"压力: {registers[1] / 10.0:F1} kPa");
    Console.WriteLine($"转速: {registers[2]} rpm");
}
