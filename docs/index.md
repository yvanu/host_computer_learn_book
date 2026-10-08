---
layout: home

hero:
  name: '从后端到上位机'
  text: '学 C# · 做 WPF · 连真实设备'
  tagline: '用你已经会的网络、异步和工程经验，走通工业设备监控的完整链路。先模拟、再联网、最后连接真实硬件。'
  image:
    src: /logo.svg
    alt: 上位机学习手册
  actions:
    - theme: brand
      text: 从第 0 课开始 →
      link: /00-start/
    - theme: alt
      text: 12 周学习路线
      link: /roadmap
    - theme: alt
      text: 查看配套代码
      link: https://github.com/yvanu/host_computer_learn_book/tree/main/examples

features:
  - icon: 🧭
    title: 专为后端开发者设计
    details: 不重复数据库、HTTP、Python 等基础，重点讲 C#、WPF、设备协议及实时交互之间的差异。
  - icon: 🛠️
    title: 每课有实验、有验收
    details: 从控制台字节解析到模拟设备 TCP 数据，再到 WPF 监控面板。讲知识，更讲怎样证明自己掌握。
  - icon: 📡
    title: 不买硬件也能开始
    details: 内置 Python TCP 模拟设备和 C# 客户端。真实 PLC、Modbus RTU 的接入留到基础扎实以后。
  - icon: 📋
    title: 长期增量维护
    details: 用学习进度表区分已完成教材、已验证实验与后续课程，源码与文档同仓库版本化管理。
---

## 三步走通第一条设备通信链路

**第一步：** 阅读 [环境准备](/00-start/environment)，在 Windows 安装 .NET SDK 和 Visual Studio 的 WPF 开发支持。

**第二步：** 运行 [Python 模拟设备](/03-protocol/tcp)，让本地 TCP 9000 端口每秒输出一帧温度/压力/转速数据。

**第三步：** 启动 C# TCP 客户端验证分帧与解析，然后在 Windows 启动 WPF 实时监控界面。

**第四步：** 进入 [Modbus TCP 实操](/04-modbus/tcp-lab)，理解真实 MBAP 帧、功能码与寄存器读写，再做 [RTU CRC16 校验实验](/04-modbus/crc-lab)。

> [!TIP] 学习方法
> 每课先自己运行实验，再尝试不看答案完成课后练习。能解释为什么这样设计，才算真正学会。
