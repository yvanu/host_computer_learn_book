using System.Net.Sockets;
using System.Text;

// 第二课：用 C# 长连接消费模拟设备的换行帧。
using var client = new TcpClient();
using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
try
{
    await client.ConnectAsync("127.0.0.1", 9000, connectTimeout.Token);
}
catch (Exception ex) when (ex is SocketException or OperationCanceledException)
{
    Console.Error.WriteLine($"无法连接模拟设备：{ex.Message}");
    return;
}

Console.WriteLine("已连接 127.0.0.1:9000；输入 SET RPM=1500 控制模拟转速，输入 quit 退出。");
using var stream = client.GetStream();
using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

var receiving = Task.Run(async () =>
{
    try
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
            Console.WriteLine($"[device] {line}");
        Console.WriteLine("[device] 设备主动关闭连接");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"[device] 网络读取失败：{ex.Message}");
    }
});

try
{
    while (client.Connected && !receiving.IsCompleted)
    {
        var input = Console.ReadLine();
        if (input is null || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;
        if (input.Length > 0)
            await writer.WriteLineAsync(input);
    }
}
catch (IOException ex)
{
    Console.WriteLine($"[client] 写入失败：{ex.Message}");
}
finally
{
    client.Close();
    await receiving;
}
