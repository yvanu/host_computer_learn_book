// Plain VitePress configuration; avoids coupling builds to a locally installed package.
export default {
  lang: 'zh-CN',
  title: '上位机开发学习手册',
  description: '为有后端经验的工程师设计的 C#、WPF、工业通信实战教材',
  cleanUrls: true,
  lastUpdated: true,
  head: [
    ['meta', { name: 'theme-color', content: '#ffffff' }],
    ['meta', { property: 'og:title', content: '上位机开发学习手册' }]
  ],
  themeConfig: {
    logo: '/logo.svg',
    siteTitle: '上位机学习手册',
    search: { provider: 'local' },
    nav: [
      { text: '开始学习', link: '/00-start/' },
      { text: '学习路线', link: '/roadmap' },
      { text: '练习项目', link: '/05-project/blueprint' },
      { text: '学习进度', link: '/PROGRESS' },
      { text: 'GitHub', link: 'https://github.com/yvanu/host_computer_learn_book' }
    ],
    sidebar: [
      {
        text: '学习概览',
        items: [
          { text: '课程首页', link: '/' },
          { text: '学习路线（12 周）', link: '/roadmap' },
          { text: '版本进度 / 下一步', link: '/PROGRESS' }
        ]
      },
      {
        text: '00 · 准备起步',
        items: [
          { text: '00-1 这门课怎么学', link: '/00-start/' },
          { text: '00-2 环境安装与验证', link: '/00-start/environment' },
          { text: '00-3 从后端到上位机', link: '/00-start/backend-map' }
        ]
      },
      {
        text: '01 · C# 与异步',
        items: [
          { text: '01-1 C# 必备语法', link: '/01-csharp/' },
          { text: '01-2 async / await', link: '/01-csharp/async' },
          { text: '01-3 属性、接口与事件', link: '/01-csharp/oop-practice' }
        ]
      },
      {
        text: '02 · WPF 桌面界面',
        items: [
          { text: '02-1 XAML 与数据绑定', link: '/02-wpf/' },
          { text: '02-2 MVVM 入门', link: '/02-wpf/mvvm' }
        ]
      },
      {
        text: '03 · 通信与协议',
        items: [
          { text: '03-1 字节、大小端和帧', link: '/03-protocol/bytes' },
          { text: '03-2 TCP 长连接实战', link: '/03-protocol/tcp' },
          { text: '03-3 串口与 RS-485', link: '/03-protocol/serial' }
        ]
      },
      {
        text: '04 · 工业通信',
        items: [
          { text: '04-1 Modbus 基础', link: '/04-modbus/' },
          { text: '04-2 寄存器、功能码与地址', link: '/04-modbus/registers' },
          { text: '04-3 Modbus TCP 实验', link: '/04-modbus/tcp-lab' },
          { text: '04-4 RTU CRC16 实验', link: '/04-modbus/crc-lab' },
          { text: '04-5 联调排障手册', link: '/04-modbus/debugging' },
          { text: '04-6 Modbus 自测 12 题', link: '/04-modbus/quiz' }
        ]
      },
      {
        text: '05 · 综合项目',
        items: [
          { text: '05-1 工业监控项目设计', link: '/05-project/blueprint' },
          { text: '05-2 分阶段验收', link: '/05-project/steps' }
        ]
      },
      {
        text: '06 · 复习与延伸',
        items: [
          { text: '求职与知识清单', link: '/06-interview/checklist' },
          { text: '文档部署与维护', link: '/deploy' }
        ]
      }
    ],
    outline: { level: [2, 3], label: '本页目录' },
    docFooter: { prev: '上一页', next: '下一页' },
    editLink: { pattern: 'https://github.com/yvanu/host_computer_learn_book/edit/main/docs/:path', text: '在 GitHub 编辑本页' },
    lastUpdated: { text: '最后更新' },
    returnToTopLabel: '返回顶部',
    sidebarMenuLabel: '菜单',
    darkModeSwitchLabel: '主题',
    lightModeSwitchTitle: '浅色模式',
    darkModeSwitchTitle: '深色模式'
  },
  vite: {}
}
