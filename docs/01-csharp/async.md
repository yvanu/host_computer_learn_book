# 01-2 · async / await：不阻塞 UI 的第一步

## 先记住这件事

`async` **不是“自动开启新线程”**；`await` 让异步操作在等待期间不占用当前线程。网络 I/O 通常不需要每次新建一个 Thread。

```csharp
using var client = new TcpClient();
await client.ConnectAsync("127.0.0.1", 9000);
```

这段等待连接操作不会像 `Thread.Sleep()` 那样阻塞当前线程。

## 对照 Python asyncio

```python
await asyncio.sleep(1)
```

```csharp
await Task.Delay(1000);
```

但 WPF 还有关键区别：修改界面属性应由 UI 线程进行。一般在 UI 事件中 `await` 一个 I/O Task 后，代码会恢复到先前的 UI 同步上下文；如果使用 `ConfigureAwait(false)`，则不能继续假设自己在 UI 线程。

## 停止采集必须可取消

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("采集已取消或超时");
}
```

真实设备还要明确区分**用户主动取消、连接断开、读取超时和协议解析错误**，不能统一显示“网络异常”。

## 关于 async void

只在事件处理器等必须返回 `void` 的入口使用；服务层尽量返回 `Task`，以便等待结束、传递异常、执行单元测试。

## WPF 典型错误

❌ 在按钮点击事件调用 `Task.Wait()` 或 `.Result`，可能让窗口冻结甚至死锁。

✅ 使用 `await ConnectAsync()`，把连接中/已连接/失败状态绑定到界面属性。

## 练习与验收

编写一个每秒打印递增计数的异步循环：要求 Ctrl+C 或 CancellationToken 能正常停止，不应无限卡在退出阶段。

**验收：** 能说明 Task 与 Thread 的区别；能说明为什么不应在 UI 线程使用 `Thread.Sleep`。
