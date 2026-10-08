# 03-2 · TCP 长连接实战

这是本书第一条完整**后端 → 设备 → 上位机**链路。

## 一、启动模拟设备

在仓库根目录执行：

```powershell
python examples/02-device-simulator/simulator.py
```

输出类似 `Device simulator listening on 127.0.0.1:9000`。此服务不需要任何第三方库。

模拟器每秒发送一条**以换行结尾的消息**：

```text
TEMP=23.3;PRESSURE=101.2;RPM=1200
TEMP=23.6;PRESSURE=101.3;RPM=1200
```

它也接受 `SET RPM=1500` 命令并返回 `ACK=RPM:1500`。模拟器仅用于学习，不代表真正工业设备的控制协议。

## 二、运行 C# 客户端

新开 PowerShell：

```powershell
dotnet run --project examples/03-tcp-client
```

客户端使用 `TcpClient`、`StreamReader.ReadLineAsync`、`StreamWriter` 完成连接、收帧和可选命令输入。输入 `SET RPM=1500`，随后观察 `ACK` 和后续采样转速；输入 `quit` 退出。

## 三、为什么一次 Read 不是一帧？

TCP 只保证按序传递字节，不提供你自定义的消息边界。

比如发送两行：`A\nB\n`，接收端有可能得到 `A\n` 和 `B\n` 两块，也可能得到 `A\nB\n` 一块或更细的碎片。 `ReadLineAsync` 自行缓存字节直到发现分隔符，所以适合这个**演示文本协议**。

生产二进制协议必须使用协议中规定的长度、帧头、转义或其它分帧策略，不能一概按行读取。

## 四、连接状态的正常变化

```text
Disconnected → Connecting → Connected → Disconnected
                          ↘ Failed
```

连接失败要显示原因；服务器终止后 `ReadLineAsync` 会返回 `null` 或抛出异常。不要把“已连接”当成永久状态。

## 五、错误演练

1. **先开客户端、不开模拟器**：应快速提示连接失败。
2. **先连接成功，再关闭模拟器**：应报告断开，而不是无限显示“在线”。
3. **连续发送两条 SET 命令**：应收到可区分的两个 ACK（协议简化版本）。
4. **发送 `SET RPM=99999`**：应收到 `ERR=INVALID_RPM`，模拟器运行不应中断。

## 课后练习

给客户端增加连接超时配置，例如 `CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3))`。解释**网络层连接成功**与**业务协议握手成功**不是同一件事。

**验收：** 能说明半包、粘包、超时、断线处理各指什么；能成功从模拟设备收到数值并发送控制文本指令。
