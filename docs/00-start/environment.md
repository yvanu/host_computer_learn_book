# 00-2 · 环境安装与验证

## 最推荐的本机环境

**Windows 10/11 + .NET 10 SDK + Visual Studio（支持 WPF 的工作负载）+ VS Code（可选）**。WPF 只面向 Windows；不要在 Linux 服务器上强行运行 WPF 窗口。

- 下载 .NET SDK：<https://dotnet.microsoft.com/download>
- 下载 Visual Studio：<https://visualstudio.microsoft.com/>
- Visual Studio 安装时勾选“.NET 桌面开发（.NET desktop development）”工作负载。
- 可选：Python 3.10+，用于先运行本书的设备模拟器。
- 可选：Wireshark / 串口调试软件，用来观察真实报文。

## 安装检查

在 PowerShell 执行：

```powershell
dotnet --info
dotnet --list-sdks
dotnet new console -n FirstDeviceApp
cd FirstDeviceApp
dotnet run
```

预期终端输出 `Hello, World!`。如果 `dotnet` 找不到，重新打开 PowerShell，检查 PATH 和 SDK 是否真的安装。

## WPF 的验证

Visual Studio 新建 **WPF 应用程序（C# / .NET）**，点击运行，确认出现空白窗口。也可以在 Windows PowerShell：

```powershell
dotnet new wpf -n FirstWpfApp -f net10.0
cd FirstWpfApp
dotnet run
```

`dotnet new wpf` 不可用时，检查是否安装 Windows Desktop SDK 或 Visual Studio 工作负载。不要把“控制台程序能运行”误当成“WPF 环境已准备好”。

## 本书示例顺序

```powershell
# 1. 跑熟悉的 Python TCP 模拟设备（另一个终端）
python examples/02-device-simulator/simulator.py

# 2. 跑 C# TCP 读取程序（第二个终端）
dotnet run --project examples/03-tcp-client

# 3. Windows 环境下打开 WPF 项目
dotnet run --project examples/04-wpf-monitor
```

无需硬件；模拟器只监听本机 `127.0.0.1:9000`。

## 本课练习

尝试在没有安装 Python 的情况下只运行 C# Hello World；解释为什么 SDK 和 Runtime 不一样：SDK 用于**编译/创建**应用，Runtime 主要用于**运行已编译**应用。

**验收：** 能从命令行新建和运行 .NET 程序，并成功打开 WPF 窗口。
