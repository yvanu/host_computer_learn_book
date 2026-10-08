# 实验 06：C# Modbus TCP 客户端（.NET 10）

1. 先启动 Python Modbus TCP 模拟器：
   ```powershell
   python examples/05-modbus-tcp-simulator/simulator.py
   ```
2. 再运行 **只读** 客户端：
   ```powershell
   dotnet run --project examples/06-modbus-tcp-client
   ```
3. 可选：只向**本地模拟器**写入模拟转速，然后读取校验：
   ```powershell
   dotnet run --project examples/06-modbus-tcp-client -- write-rpm 1500
   ```

预期输出类似 `温度: 25.3 °C`、`压力: 101.2 kPa`、`转速: 1200 rpm`。

**与早期文本 TCP 示例有什么不同？** 这里根据 MBAP 头的长度读取精确字节，使用事务号校验响应，处理设备返回的 Modbus 异常响应。不使用 `ReadLineAsync` 或 RTU CRC。

本示例地址固定为 `127.0.0.1:1502`，默认只读。服务器尚无 .NET SDK，需在你的 Windows 开发机编译运行；这份源码没有被声称通过 .NET 编译测试。
