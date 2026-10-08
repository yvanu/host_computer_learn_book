# 00-3 · 从后端知识迁移到上位机

已有后端经验是优势，但不要用 Web 的运行假设套设备软件。

| 熟悉的后端概念 | 上位机对应能力 | 关键变化 |
| --- | --- | --- |
| Flask Controller | WPF ViewModel / Command | 是用户动作触发，但执行不可阻塞 UI |
| Pydantic / dataclass | C# record / class | 强类型和编译期检查更明显 |
| asyncio / Celery | Task / async / CancellationToken | 要处理 UI 线程、生命周期和取消 |
| TCP Socket | TcpClient / NetworkStream | 设备常是长连接、原始字节、半包 |
| JSON DTO | 二进制协议帧 / 寄存器映射 | 字节序、缩放倍数、校验字不可忽视 |
| ORM / PostgreSQL | SQLite/数据库采集层 | 不能让同步磁盘写入卡住界面 |
| HTTP 超时 / 重试 | 设备连接状态机 | 重连不等于盲目重复“启动/停止”命令 |
| 服务日志 | 操作日志 / 报文抓包 | 谁在何时对哪个设备发了什么至关重要 |

## 典型后台请求 vs 设备采集

Web：`request → validate → SQL → JSON response`

上位机：`connect → subscribe/poll → frame → decode → device state → UI`

你需要特别注意：“读一次 TCP stream”不一定读到“一帧”，而“断线自动重连”也不是所有控制场景都安全。

## 实践：把 Python 字典迁成 C# record

Python：

```python
sample = {"temperature": 23.5, "pressure": 101.2}
```

C#：

```csharp
public record DeviceReading(double Temperature, double Pressure);
var sample = new DeviceReading(23.5, 101.2);
Console.WriteLine(sample.Temperature);
```

属性类型明确；编译器会帮助你在拼写和类型不匹配时尽早发现错误。

## 两个常见误区

**误区一：WPF 就是带窗口的 Flask。** 错。WPF 是长期运行的单进程桌面程序，UI Dispatcher 必须保持响应，连接事件和设备状态会持续到达。

**误区二：自动重试无成本。** 错。设备控制指令可能**非幂等**。例如“电机转一圈”超时后重发可能造成重复动作。需要设备支持序号、读回校验或安全的确认策略。

**练习：** 为温度超上限的场景画出一条链路，说明采集线程如何通知 UI，同时保存告警记录但不阻塞读设备。

**验收：** 能指出你的后端知识里 3 项可复用能力、3 项需要重新学习的能力。
