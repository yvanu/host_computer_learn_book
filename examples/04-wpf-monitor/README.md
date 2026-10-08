# 第 4 个实验：WPF 监控面板（Windows）

**Windows + .NET 10 SDK** 下运行：

```powershell
python examples/02-device-simulator/simulator.py
# 另开一个 PowerShell
dotnet run --project examples/04-wpf-monitor
```

点击“连接设备”后，可观察每秒刷新的温度、压力、转速与时间。断开连接或停止模拟器应显示连接状态变化，窗口继续响应。练习：增加“设备地址”输入框，或增加最近采样时间状态。

结构：
- `MainWindow.xaml`：页面布局和 Binding。
- `MainWindow.xaml.cs`：只负责构建 ViewModel 与关闭清理。
- `MainViewModel.cs`：连接、更新属性、PropertyChanged 与 Command。

注意：这是**单设备 + 教学简化文本协议**，不是完整工业监控软件；生产环境必须抽出独立通信服务、加入消息长度限制、受控重连、性能监测和安全控制。

Linux CI / 本次工作环境没有 .NET SDK，因此需要你在 Windows 图形环境完成 WPF 运行验证。
