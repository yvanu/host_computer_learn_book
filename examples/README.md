# 配套可运行示例

推荐按以下顺序进行。第一步在 Windows 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download)，然后从仓库根目录执行。

```bash
dotnet run --project examples/01-csharp-basics
```

另开一个终端启动 Python 模拟设备：

```bash
python examples/02-device-simulator/simulator.py
```

接着在第二个终端启动 C# 客户端：

```bash
dotnet run --project examples/03-tcp-client
```

Windows 上还可以运行 WPF 监控台：

```powershell
dotnet run --project examples/04-wpf-monitor
```

前四项实验使用文本协议模拟器 `127.0.0.1:9000`。

## 进入 Modbus 工业通信实战

```powershell
# 开一个终端：启动本机模拟 PLC（无需 pip）
python examples/05-modbus-tcp-simulator/simulator.py

# 另一个终端：任选 Python 或 C# 客户端
python examples/05-modbus-tcp-simulator/inspect_client.py
dotnet run --project examples/06-modbus-tcp-client

# 只有本机模拟器才允许用下面的写入练习
dotnet run --project examples/06-modbus-tcp-client -- write-rpm 1500

# 单独学习 RTU CRC，无需串口
python examples/07-modbus-rtu-crc/crc16.py
```

`05/06` 使用 `127.0.0.1:1502` 的标准 Modbus TCP 帧；`07` 不建立网络连接。

## WPF 监控台（Windows 桌面程序）

```powershell
# 第一个终端：温度每秒动态变化，适合观察曲线和 32℃ 报警
python examples/05-modbus-tcp-simulator/simulator.py --dynamic

# 第二个终端（Windows + .NET 10 SDK）：运行完整 WPF 监控实验
dotnet restore examples/08-wpf-modbus-monitor/IndustrialMonitor.csproj
dotnet run --project examples/08-wpf-modbus-monitor
```

操作“连接设备”，观察实时温度、报警、历史 SQLite 存储并可导出 CSV。**WPF 工程须在 Windows 验证编译与 GUI，Linux 服务器没有 SDK，当前未做编译通过宣称。**

所有实验代码仅用于模拟学习，不可直接用于真实工业控制系统。

> 本仓库的 Linux 构建环境未安装 .NET SDK，因而 C# / WPF 运行需要在装有 .NET SDK 的 Windows 开发机完成；Python 模拟器可以独立测试。
