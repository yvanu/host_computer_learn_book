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

模拟器只监听 `127.0.0.1:9000`。同时只能启动一个服务器实例，否则端口被占用。代码仅用于模拟学习，不应用于连接未经验证的工业设备。

> 本仓库的 Linux 构建环境未安装 .NET SDK，因而 C# / WPF 运行需要在装有 .NET SDK 的 Windows 开发机完成；Python 模拟器可以独立测试。
