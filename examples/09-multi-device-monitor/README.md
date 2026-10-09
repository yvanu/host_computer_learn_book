# 09 · 两台设备的 WPF 工业监控台（v0.4）

这是 **Windows 桌面端** 的进阶项目，建立在 [08 单设备监控台](../08-wpf-modbus-monitor/) 的经验上。技术栈：C# / WPF / Modbus TCP / Microsoft.Data.Sqlite。

## 你将得到什么

- 左侧**两台独立模拟设备**，分别连接 `127.0.0.1:1502`、`127.0.0.1:1503`（只读 FC03）。
- 两台设备各有单独的 TCP 连接、事务号、生命周期、温度曲线和告警判断。
- 异常后**有限自动重连**：1、2、4 秒退避；最多连续 3 次；断开按钮可取消等待，重试结束后可手动重新连接。
- 每台设备单独配置高温阈值（20～80 ℃），不允许无穷大、NaN 或超范围值。
- SQLite **单消费者 + 多生产者有界 Channel（240 条）**，记录 `device_id` 和报警标记。
- 每台设备**分页历史查询**（每页 20 条）和只导出选中设备的 CSV。
- 每台温度曲线仅保存最近 60 个采样点；日志面板只显示最近 40 个事件。
- 模拟器支持 `--devices 2 --dynamic`，两台设备的温度基线不同。之前旧课程的无参数单设备模式仍可运行。

## Windows 运行步骤

要求 Windows 10/11、.NET 10 SDK 和 Visual Studio 的“.NET 桌面开发”；Python 3.10+ 作为设备模拟器。

打开仓库根目录，窗口 1：

```powershell
python examples/05-modbus-tcp-simulator/simulator.py --devices 2 --dynamic
```

看到模拟器监听 1502、1503 代表两台模拟设备已经启动。窗口 2：

```powershell
dotnet run --project examples/09-multi-device-monitor/MultiDeviceMonitor.csproj
```

桌面窗口里分别选择“模拟设备 01 / 02”，点击连接，切换并观察不同温度。更改设备 02 的阈值为 `29.0`，确认设备 01 的 32.0 阈值仍未改变。

## 断线与重连实验

1. 两台设备都连接后停止整个 Python 模拟器：界面应分别提示掉线和退避倒计时。
2. 在重试 1、2、4 秒的窗口内重新启动模拟器：下一次连接可能恢复；超时则显示“离线 · 重试已耗尽”。
3. 在设备 01 的重连等待期间点击“断开连接”：对应会话退出，另一台不受影响。
4. 如果三次重试已耗尽，重新点击“连接选中设备”可手动启动新会话。

**注意：** 重连仅对读操作使用；**不能将同一策略直接套到真实电机的写寄存器控制命令**。本项目不实现任何写功能。

## 历史和数据持久化

新版本使用独立 SQLite 路径，故**不会迁移、删除或覆盖** v0.3 的 `monitor.sqlite`：

```text
%LOCALAPPDATA%\HostComputerLearn\monitor_v04.sqlite
```

表字段：`device_id, captured_at_utc, temperature_c, pressure_kpa, rpm, alarm`；按设备和记录 ID 建联合索引。点击“查询历史”后只展示选中设备的最新 20 条；可以上一页/下一页。 CSV 也仅包含选中设备的数据。

用 Python 标准库核对：

```powershell
python -c "import os,sqlite3; p=os.path.join(os.environ['LOCALAPPDATA'],'HostComputerLearn','monitor_v04.sqlite'); db=sqlite3.connect(p); print(db.execute('select device_id,count(*),max(temperature_c) from readings group by device_id').fetchall())"
```

## 读源码顺序

1. `DevicePanel.cs`：为什么每台设备要有独立的 ViewModel？
2. `ModbusDeviceClient.cs`：一个设备对应自己的 TCP socket 和事务号。
3. `DeviceSession.cs`：循环采集、异常分类、最多三次重连和取消。
4. `MainViewModel.cs`：选中设备、修改阈值、命令、历史分页和事件。
5. `SqliteHistoryStore.cs`：多生产者/单消费者队列与参数化 SQL。
6. `MainWindow.xaml`：设备导航、动态绑定、折线与历史查询 UI。

## 编译/测试边界

这个服务器没有安装 .NET SDK，避免为验收占用其有限内存；新增项目在提交后交由仓库 GitHub Actions 的 **Windows .NET 10 CI** 编译验收。CI 编译通过也不等于已点击窗口、人工验证 SQLite 和告警状态；完整交互需要 Windows 图形桌面。

工程**仅允许 localhost 端口 1502/1503**，是教学项目，不是工业现场 SCADA，绝不可直接用于生产机器控制。

详细课程：[05-5 多设备与断线重连](/05-project/multi-device-lab)、[05-6 SQLite 与报警实战](/05-project/history-alerts)。
