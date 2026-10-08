# 02-2 · MVVM：让 UI 不背锅

## 核心思想

```text
View（XAML/视觉层）
    ↕ Binding / Command
ViewModel（可观察状态 + 用户动作）
    ↓
Model / Communication（数据和设备连接）
```

- **View**：负责显示，不应该决定 Modbus 功能码。
- **ViewModel**：持有“温度/在线状态/错误信息”，处理按钮命令和界面逻辑。
- **Model / Service**：负责协议数据、网络通信、业务规则。

本书 v0.1 演示代码为易理解把 TCP 连接暂放在 ViewModel 内，适合教学入门。后续 v0.2～v0.3 才抽出独立采集服务，避免在入门课中过度抽象。

## 属性更改通知

```csharp
public class DeviceViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _status = "未连接";

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(Status)));
        }
    }
}
```

只实现普通 `public string Status { get; set; }`，绑定值初始可显示，但之后变化不会自动发出通知。

## Command 的用途

在 XAML 中使用 `<Button Command="{Binding ConnectCommand}"/>`，让按钮只声明触发哪个行为，而不是在视图代码中堆积 `Button_Click` 业务代码。

有真实的复杂项目时可以使用 CommunityToolkit.Mvvm 减少重复代码；本书初始示例刻意使用标准库接口，让你先理解事件和绑定本质。

## 与后端常见分层的差异

后端控制器每个 HTTP 请求调用一次 Service；ViewModel 则**长时间存在**，需要管理订阅、取消和设备连接生命周期。窗口关闭时，必须关闭连接与后台循环，不应遗留“幽灵采集”。

## 练习与验收

增加一个 `LastUpdated` 属性，把最近一次接收到数据的时间显示在 XAML 中。检查停止模拟器后，界面是否仍可点击且能提示连接断开。

**验收：** 能解释 ViewModel 为什么要触发 `PropertyChanged`，且 UI 不应直接解析 TCP 字节。
