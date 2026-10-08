# 01-1 · 后端工程师够用的 C# 基础

## 本课目标

能读懂 `record`、属性、`TryParse`、`List<T>`、异常和 `using`，并运行第一个测量数据解析程序。

## Python 和 C# 写法对比

```python
temperature = 23.5
if temperature > 40:
    print("报警")
```

```csharp
double temperature = 23.5;
if (temperature > 40)
{
    Console.WriteLine("报警");
}
```

C# 用类型描述数据和契约，语句通常以分号结尾。局部变量也可以用 `var`，但 **var 是编译器推断静态类型，不是 Python 的动态类型**。

## 属性、record 和对象

```csharp
public record Reading(DateTime Time, double Temperature, double Pressure);
var current = new Reading(DateTime.Now, 23.5, 101.2);
Console.WriteLine(current.Temperature);
```

`record` 适合只承载数据的示例模型；涉及设备状态变化、资源生命周期和复杂行为时再考虑普通 `class`。

## 安全转换：不要让坏报文把程序打崩

```csharp
using System.Globalization;

var raw = "23.5";
if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
    Console.WriteLine($"温度：{value:F1} ℃");
else
    Console.WriteLine("非法温度数据");
```

通信协议使用英文小数点时应该明确采用不受本机区域设置影响的 `InvariantCulture`。

## 运行配套代码

```powershell
dotnet run --project examples/01-csharp-basics
```

示例通过 `key=value` 拆分模拟采样，将温度、压力和转速解析为强类型数据，同时演示损坏数据如何被拒绝。

## 常用语法速览

| C# | 作用 | Python 联想 |
| --- | --- | --- |
| `string?` | 可能为 null 的字符串 | `str \| None` |
| `List<Reading>` | 读数列表 | `list[Reading]` |
| `Dictionary<string,int>` | 键值表 | `dict[str,int]` |
| `try/catch/finally` | 异常处理 | `try/except/finally` |
| `using var stream = ...` | 作用域结束释放资源 | `with open(...) as f` |
| `event` | 发布通知 | 回调 / 观察者模式 |
| `CancellationToken` | 可协作取消 | asyncio Task cancellation（但机制不同） |

## 练习与验收

1. 为读数增加 `Voltage` 电压字段，并打印单位 V。
2. 人为把 `TEMP=abc` 放进输入，验证程序不崩溃。
3. 解释为什么 `float` 不适合所有金额场景（涉及精度时选 decimal）。

**验收：** 能独立定义一个 `record`、解析数字、处理失败路径。下一课：[Task、async 和取消](/01-csharp/async)。
