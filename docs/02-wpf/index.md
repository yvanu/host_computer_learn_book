# 02-1 · WPF、XAML 与数据绑定

## 从 Web 前端迁移思维

WPF 用 **XAML 描述窗口结构**，用 C# 管理状态和行为。你可以把 XAML 类比为 Vue/React 的模板，但别把 WPF ViewModel 当作后端 Controller：桌面程序没有每次请求后销毁的生命周期。

## 一个最简单的窗口

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="设备面板" Width="600" Height="380">
  <StackPanel Margin="20">
    <TextBlock Text="实时温度" FontSize="18"/>
    <TextBlock Text="{Binding Temperature}" FontSize="32"/>
  </StackPanel>
</Window>
```

`{Binding Temperature}` 表示从窗口的 **DataContext** 中读取 `Temperature` 属性。如果值发生变化并且实现了 `INotifyPropertyChanged`，WPF 会刷新对应的控件。

## 常用控件与布局

| WPF | 用途 |
| --- | --- |
| Grid | 行列布局，适合监控主页 |
| StackPanel | 横向/纵向线性堆叠 |
| TextBlock | 展示文本 |
| TextBox | 输入参数 |
| Button + Command | 触发连接/下发操作 |
| ItemsControl / DataGrid | 展示设备列表或采集记录 |
| Border | 状态块、分组与视觉层次 |

UI 不应该在后台 I/O 循环中直接访问 `TextBlock.Text`。推荐**数据模型 → ViewModel 属性 → Binding → UI**。

## 实操：修改示例界面

Windows 下运行：

```powershell
python examples/02-device-simulator/simulator.py
dotnet run --project examples/04-wpf-monitor
```

WPF 启动后点击“连接设备”，观察数值更新和连接状态。试着更改 XAML 中的温度字号和 Grid 列宽，重新编译查看变化。

## 最容易犯的错

- 改了 C# 属性值，但未触发 `PropertyChanged`，界面不会自动刷新。
- 在 UI 线程执行阻塞式网络读取，窗口“未响应”。
- 在后台线程直接修改绑定的 `ObservableCollection`，可能产生跨线程异常。
- 把通信解析、SQL、控件样式写进同一个按钮事件，项目难以测试。

## 练习与验收

增加“压力”指标卡，设置标题、单位和小数位。**验收：** 能解释 DataContext、Binding 与 `INotifyPropertyChanged` 的关系。
