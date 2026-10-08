# 实验 08：WPF 工业设备监控台（v0.3）

**这不是网页。** 它是面向 Windows 10/11 的 C# WPF 桌面程序，连接前一课的真实 Modbus TCP 训练模拟器。

## 可观察的功能

- **实时采集**：每秒通过 FC03 读取 3 个 Holding Register（温度、压力、RPM），每次请求都检查事务号、Unit ID、协议号、长度和功能码。
- **轻量可视化**：温度、压力和转速三张实时指标卡；温度折线图最多保留 60 次采样，不需要第三方图表控件。
- **报警事件**：温度达到 32.0 ℃ 触发高温报警；恢复到阈值下方后记录恢复事件；事件面板只保存最近 30 条在内存中。
- **SQLite 持久化**：专用后台写入任务、最多排队 120 条采样，参数化 INSERT，避免在 UI 线程执行磁盘操作。每条记录包含 UTC 时间、三个指标和报警标记。
- **CSV 导出**：将数据库的历史记录导出成 UTF-8 BOM CSV，可在 Excel 查看。
- **设备连接管理**：按钮连接、断开；每次读取有 3 秒超时，报错会显示在界面；关闭窗口时取消采集并等待数据写入结束。

本版**仅连接本机 127.0.0.1:1502，且上位机只读**。模拟器的写转速练习在上一章的独立客户端完成，不把控制指令掺进监控台。

## Windows 一步步运行

1. 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download) 和 Visual Studio 的 **.NET 桌面开发**工作负载。
2. 在仓库根目录启动模拟器的动态演示模式（PowerShell 窗口 1）：

   ```powershell
   python examples/05-modbus-tcp-simulator/simulator.py --dynamic
   ```

3. 另一 PowerShell（窗口 2）运行桌面程序：

   ```powershell
   dotnet restore examples/08-wpf-modbus-monitor/IndustrialMonitor.csproj
   dotnet run --project examples/08-wpf-modbus-monitor
   ```

4. 在界面点击“连接设备”。开始会显示约 25.3 ℃，温度随模拟器每秒变化。运行约十几秒到达 32 ℃ 后，会产生高温报警；随后模拟器降温并记录恢复事件。
5. 点击“导出 CSV”，选择文件路径，检查列 `time_utc,temperature_c,pressure_kpa,rpm,is_alarm`。
6. 关闭模拟器，观察上位机是否显示通信异常而不是卡死；重新启动模拟器后手动连接。
7. 关闭桌面窗口，确认采集任务已经取消。

> 第一次 `dotnet restore` 需要下载 NuGet 依赖 Microsoft.Data.Sqlite 10.0.0；这是 **Windows 开发机** 的操作，不应在小内存部署服务器上运行。

## SQLite 数据文件

默认路径：

```text
%LOCALAPPDATA%\HostComputerLearn\monitor.sqlite
```

可以用 SQLite 浏览器或 Python 标准库查看：

```powershell
python -c "import os,sqlite3; p=os.path.join(os.environ['LOCALAPPDATA'],'HostComputerLearn','monitor.sqlite'); db=sqlite3.connect(p); print(db.execute('select count(*),max(temperature_c) from readings').fetchone())"
```

要在课后练习里查询所有报警数据：

```sql
SELECT captured_at_utc, temperature_c, rpm
FROM readings WHERE alarm = 1 ORDER BY id DESC LIMIT 20;
```

## 源码导航（按这个顺序阅读）

| 文件 | 作用 | 对照后端知识 |
| --- | --- | --- |
| `DeviceReading.cs` | 单次测量模型和报警阈值 | DTO / dataclass |
| `ModbusDeviceClient.cs` | TCP 建连、MBAP 长度分帧、解析寄存器 | Python socket / protocol parser |
| `MainViewModel.cs` | 设备采集循环、数据绑定、报警、趋势 | Controller + 定时服务 |
| `SqliteHistoryStore.cs` | 有界 Channel + 后台 SQLite 落盘 | Celery/生产者消费者 |
| `MainWindow.xaml` | 工业设备监控页的真实布局 | 前端页面模板 |

## 已知边界

- Windows 上的 C# 编译、WPF UI 与 SQLite 原生依赖运行**尚待在带 .NET 10 SDK 的 Windows 机器验证**；本次服务器无 SDK，不把静态 XML 验证当成完整编译通过。
- 只支持一个固定的模拟设备，不做任意 PLC 地址配置，不具备生产现场的权限体系和安全联锁。
- 高温告警阈值固定 32 ℃，不支持历史报警确认/解除流程；后续可独立建报警表。
- 只有一个只读轮询任务，没有自动重连；这是为了让你先能观察断线发生的状态，并练习主动重新连接。
- SQLite 在后台异步队列写入，但 SQLite ADO.NET 命令本身为同步 API；每条数据同步写入发生在**后台任务**，不会阻塞 WPF Dispatcher。

## 练习

1. 将温度阈值改为 30 ℃，观察报警开始和恢复的事件变化。
2. 将界面最后一次采集时间改成只显示 `HH:mm:ss`。
3. 分析两个连续异常：先停止模拟器，然后在不重新连接时点击“导出 CSV”，解释为什么已有数据仍然可以导出。
4. 用 SQL 查询最近 10 条采集记录，并比较屏幕上的最后一个数值是否对应最后一次采样。

相关教材：[05-3 WPF 工业监控实战](/05-project/wpf-lab)。
