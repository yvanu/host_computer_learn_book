# 01-3 · 属性、接口、事件、泛型：后端开发者转 C# 必练

> **目标：** 理解 C# 如何用类型与事件表达设备数据，而不是死记关键字。建议在完成第一课控制台项目后阅读。

## 一、属性与字段：Python property 对照

在 Python 里，你可能用 `@property` 实现数据访问；C# 的属性经常用 `get` / `set`：

```csharp
public sealed class Device
{
    public string Name { get; init; } = "";
    public bool IsOnline { get; private set; }
    public void SetOnline(bool value) => IsOnline = value;
}
```

`init` 主要在初始化时赋值；`private set` 只允许类的内部修改。这样可避免让任意 UI 控件随意篡改设备连接状态。

**练习：** 增加 `LastSeen` 只读属性，由 `OnMessageReceived()` 负责更新。

## 二、接口：将“设备通信”与“具体协议”分开

```csharp
public interface IDeviceReader
{
    Task<ushort[]> ReadRegistersAsync(
        ushort address, ushort count, CancellationToken token);
}
```

WPF ViewModel 只需要“读取寄存器”的契约，不一定关心底层用 TCP、串口或 Mock 实现。但**不要一开始给所有类都加接口**；真正要替换协议或测试时，再在边界抽象。

## 三、事件：设备状态变化时通知界面

```csharp
public sealed class TemperatureSensor
{
    public event EventHandler<double>? TemperatureChanged;

    public void Publish(double value) =>
        TemperatureChanged?.Invoke(this, value);
}
```

订阅：

```csharp
var sensor = new TemperatureSensor();
sensor.TemperatureChanged += (_, value) =>
    Console.WriteLine($"新读数 {value:F1}");
sensor.Publish(25.3);
```

`event` 是通知订阅者的一种机制，概念上可对照 Python 里的回调。WPF 的 `INotifyPropertyChanged` 也是基于事件通知。

**重要：** 长生命周期对象的订阅关系要有配对的取消订阅，避免对象意外一直被引用。

## 四、泛型：`List<T>` / `Task<T>` 为什么有尖括号？

```csharp
public record Reading(DateTime Time, double Temperature);
List<Reading> history = new();
history.Add(new Reading(DateTime.Now, 25.3));

Task<Reading> LoadAsync() =>
    Task.FromResult(history[0]);
```

`List<Reading>` 不是“随便装什么”的 Python list；对列表元素的结构有编译期检查。`Task<Reading>` 表示异步完成后结果类型是 Reading。

## 五、完整的训练流程

1. 在 `examples/01-csharp-basics/` 运行已有数据解析程序。
2. 将 `Reading` record 添加 `Voltage` 字段。
3. 创建 `TemperatureSensor` 并订阅 `TemperatureChanged`，接收新数据后输出。
4. 将最近 5 条 `Reading` 放入 `List<Reading>`，输出最高温度。

## 六、6 道题与解析

1. `var x = 2` 是否让 x 变成 Python 式动态类型？**否，x 静态推断为 int。**
2. `private set` 的作用？**外部可读，但只能从类内部赋值。**
3. 为什么不推荐暴露公共字段 `public bool IsOnline;`？**无法集中校验状态改变的入口。**
4. `event` 和直接调用回调有何关系？**由事件源发布通知，多个订阅者可分别响应。**
5. `Task<int>` 表示什么？**异步操作最终产出 int，而不是一条独立线程。**
6. 为什么要取消事件订阅？**避免长寿命发布者持有不再需要的订阅者。**

**验收：** 能在一个 C# 控制台程序里使用 record、属性、event、`List<T>` 和 `Task<T>`，并指出它们在上位机中的实际用途。
