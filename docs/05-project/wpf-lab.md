# 05-3 · 从后端到上位机：做一个 WPF 工业监控台

> 本课是 v0.3 的主要交付。你会把 Python/Flask、TCP、队列与数据库经验迁移到桌面端：**FC03 采集 → 状态更新 → 趋势 → 报警 → SQLite → CSV**。
>
> 本课配套源码：[examples/08-wpf-modbus-monitor](https://github.com/yvanu/host_computer_learn_book/tree/main/examples/08-wpf-modbus-monitor)。

## 一、先看最终运行的系统是什么

```text
 ┌────────────────────────────── WINDOWS 上位机 ──────────────────────────────────────┐
 │                    [连接设备]  [断开连接]  [导出 CSV]                               │
 ├─────────────┬─────────────────────────────────────────────────────────────────────┤
 │             │  温度 25.3 ℃         压力 101.2 kPa         转速 1200 rpm             │
 │  模拟设备   ├─────────────────────────────────────────────────────────────────────┤
 │ 127.0.0.1   │  温度实时趋势（最近 60 条）                                           │
 │  端口 1502  │  /\/\/\----/\/\/\                                                        │
 │             ├───────────────────────────────┬─────────────────────────────────────┤
 │  在线状态   │   高温报警状态                 │   最近采集、报警、恢复事件            │
 └─────────────┴───────────────────────────────┴─────────────────────────────────────┘
                              ↓ 后台队列
                         SQLite 历史数据库
```

**真正的数据不是写死的 UI 字符串。** 它来自独立的 Python 模拟设备发出的 Modbus TCP 寄存器响应；关闭模拟器会使通讯中断，界面应显示异常。

## 二、环境准备与实际启动

项目面向 Windows 10/11 + .NET 10 SDK + Visual Studio 的 WPF 支持。**不需要买 PLC**。

在仓库根目录打开 PowerShell 窗口 1：

```powershell
python examples/05-modbus-tcp-simulator/simulator.py --dynamic
```

`--dynamic` 使模拟器每秒改变温度值，升到 32 ℃ 时便于触发报警，再慢慢下降。旧版无参数模式仍然保留固定的初始值，用于早期 Modbus 单元测试。

然后打开 PowerShell 窗口 2：

```powershell
dotnet restore examples/08-wpf-modbus-monitor/IndustrialMonitor.csproj
dotnet run --project examples/08-wpf-modbus-monitor
```

首次执行 `dotnet restore` 会下载 SQLite 的官方 NuGet 驱动。以后恢复依赖通常不必重复下载。

点击**连接设备**后观察温度折线和报警事件。点击**断开连接**会停止轮询，但已有历史数据库仍可以导出。

> [!WARNING] Windows 编译验收状态
> 本书的服务器是低内存 Linux，没有安装 .NET SDK，也不具备 WPF 图形环境。当前源码已在 [GitHub 托管 Windows 运行器](https://github.com/yvanu/host_computer_learn_book/actions/runs/37806813899) **成功编译**，但云端编译不等同于真正打开界面后完成操作验收。Python 模拟器与 Modbus 网络测试已通过；请在 Windows 按上述步骤运行并记录真实交互结果。

## 三、把项目结构理解成你熟悉的后端分层

| 上位机文件 | 后端工程师熟悉的角色 | 本项目为什么这样分 |
| --- | --- | --- |
| `MainWindow.xaml` | Vue/HTML 模板 | 只渲染 UI，不处理网络协议 |
| `MainViewModel.cs` | Controller + 定时任务协调 | 负责启动、停止、状态与事件 |
| `ModbusDeviceClient.cs` | Socket Client + 协议解析器 | 只负责连接、分帧、检查与解码 |
| `DeviceReading.cs` | Python dataclass / Pydantic DTO | 说明数据是什么 |
| `SqliteHistoryStore.cs` | 后台消费任务 + 数据库访问层 | 采集线程不等待磁盘同步写入 |

注意本项目没有为了“架构漂亮”增加 8 层工厂、接口和依赖注入容器。先把**真数据走通**，再根据多设备需求抽象。

## 四、第一步：读懂 ModbusDeviceClient

你已经会 Python socket，所以重点不是重学 TCP，而是理解**Modbus TCP 的帧边界**。

客户端每秒请求：

```text
00 01 00 00 00 06 01 03 00 00 00 03
└TID┘ └PID┘ └长度┘ UID FC └起点┘ └数量┘
```

服务器返回 3 个 16 位寄存器。读取代码先执行 `ReadExactlyAsync(7)` 取完整 MBAP，再按 `length - 1` 读剩余 PDU，不会误把一个网络包当成完整协议帧。

```csharp
await stream.ReadExactlyAsync(header, cancellationToken);
ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
byte[] pdu = new byte[length - 1];
await stream.ReadExactlyAsync(pdu, cancellationToken);
```

真实代码还检查长度范围、事务号、站号、功能码和异常码，不能只复制上面三行。

**自主练习：** 为什么把 `ReadExactlyAsync` 换成简单的 `ReadAsync` 有风险？上一章的半包测试能否复现？

## 五、第二步：ViewModel 不允许卡住窗口

后端里，你可能写 Celery 任务每秒调用一个 API。在 WPF 里不能把网络等待放在按钮事件线程里。

本项目采集循环核心思想：

```csharp
while (true)
{
    var reading = await device.ReadAsync(token);
    await store.AddAsync(reading, token);
    ShowReading(reading);
    await Task.Delay(1000, token);
}
```

`await` 释放 UI 线程，用户仍可以点击“断开”；`CancellationToken` 控制停止；关闭窗口时等待后台 SQLite 队列写完再退出。

**主动练习：** 把 1000 改成 500，观察 UI 更新频率和历史入库数量的变化；解释为什么 UI 刷新频率不应该无限跟着设备推送频率增长。

## 六、第三步：为什么需要 SQLite 后台写入队列

与 Flask 请求类似，设备采集也需要持久化。但如果数据库同步 INSERT 占用 UI Dispatcher，窗口可能卡顿。

我们使用一个标准库 `Channel<DeviceReading>` 作为 **有界队列**，后台单消费者负责 `INSERT`：

```text
FC03 每秒读取 → DeviceReading → Channel（最多 120 条） → SQLite 写入任务
                          ↘ ViewModel + WPF Binding
```

数据库位置：`%LOCALAPPDATA%\HostComputerLearn\monitor.sqlite`。

关键思想：

- `Channel` 最多缓存 120 条；写入跟不上时采用 **Wait 背压**，而不是无限堆内存。
- INSERT 使用参数绑定，而不是 SQL 字符串拼接。
- 数据库存的是 UTC 时间、温度、压力、转速和告警标记。
- 退出时完成 Channel 并等待后台消费者处理剩余记录。

## 七、第四步：看温度折线为什么持续变化

项目的 `MainViewModel` 只保留最近 60 个温度值用于绘图，每次收到新值，将它映射到宽 900、高 160 的逻辑 Canvas：

```csharp
double y = 152.0 - Math.Clamp((temperature - 20.0) / 20.0, 0, 1) * 140.0;
```

然后生成 WPF `PointCollection`，绑定给 `Polyline.Points`。此方法适合 1 秒一帧的入门场景，不引入大型图表库。

**注意：** 本图纵向范围固定为 20～40 ℃；是教学示意，不是通用科学绘图模块。正式场景要考虑动态量程、时间刻度和缺测标记。

## 八、第五步：报警是状态变化，不是每秒弹窗

规则：`TemperatureC >= 32.0` 时为高温。ViewModel 使用上次报警状态做比较：

- 从正常 → 高温：写一条“高温告警”事件。
- 持续高温：更新状态，但**不重复制造几十条报警**。
- 从高温 → 正常：记录恢复事件。

这样比每次采集发现超温就弹窗更接近实际工程思路。

目前事件面板只保存内存最近 30 条；数据库保存每一笔采样的 `alarm` 字段。后续的告警确认、分级和持久化事件表是进阶任务。

## 九、学会用 SQL 验证“看起来正常”是不够的

打开保存的 SQLite 数据库，执行：

```sql
SELECT COUNT(*) AS total,
       ROUND(MAX(temperature_c), 1) AS max_temp
FROM readings;

SELECT captured_at_utc, temperature_c
FROM readings WHERE alarm = 1 ORDER BY id DESC LIMIT 10;
```

还可以在界面点击**导出 CSV**，使用 Excel 检查不同时间戳对应的温度和报警标志。

## 十、故障复现与验收

| 操作 | 期望表现 | 应验证 |
| --- | --- | --- |
| 未开模拟器 → 点连接 | 状态提示连接失败 | UI 不假死 |
| 模拟器正常运行 → 点连接 | 持续读取 3 个数值 | 单位换算正确 |
| 启动 `--dynamic` | 温度曲线缓慢变化 | 温度范围正确 |
| 温度跨过 32 ℃ | 只产生一次报警进入事件 | 恢复到阈值以下产生一次恢复事件 |
| 停止模拟器 | 连接中断或超时并提示 | 能重新手动连接 |
| 点击导出 CSV | 下载本地采样记录 | 历史与 SQLite 内容一致 |
| 在采集过程中关闭窗口 | 窗口退出无卡死 | SQLite 记录不丢最后样本 |

**验收标准：** 如果以上 7 项能在 Windows 逐项复现，并能解释数据流，说明已经从“看懂 Modbus”进入“能写工业监控上位机基础版”。

## 课后任务

1. 添加一个“压力上限 110 kPa”的新报警规则，不能每秒重复报警。
2. 在设备状态离线时将数值标记为“最后读数”（不要误称实时）。
3. 使用 SQL 查询按天分组的采样数，理解 UTC 与本地时间的不同。
4. 设计一个“手动重连”操作：连接失败后可再次连接，且不会产生双重采集循环。

**下一步：** [05-4 报警、历史数据和故障演练](/05-project/monitor-exercises)。
