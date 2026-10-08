# 文档站发布与增量维护

## 本地预览

```bash
npm install
npm run docs:dev
```

打开终端给出的本地地址。修改 `docs/` 下 Markdown 后即可看到内容更新。

## 构建和发布到 Cloudflare

```bash
npm run docs:build
npx wrangler deploy -c wrangler.jsonc
```

使用 Cloudflare Workers 静态资源托管（配置见仓库根目录 `wrangler.jsonc`）。部署前需要有效的 `CLOUDFLARE_API_TOKEN` 和可访问的 Cloudflare 账号，**令牌不要写入仓库或教材**。

此处部署为**手动触发**。如果后续希望每次 `git push` 自动更新，建议单独为 Cloudflare Workers Builds 连接 Git 仓库（或建立使用 GitHub Secrets 的 CI），确认账号权限后再启用；当前项目不承诺尚未配置的自动部署。

## 内容协作

新增内容时：

1. 从课程目录找到对应 `docs/` 页面。
2. 章节遵循“目标、原理、代码、练习、验收、坑点”。
3. 配套实例放 `examples/课程编号`，记录依赖与启动命令。
4. 更新 `docs/PROGRESS.md` 的完成状态和验证记录。
5. 本地 `npm run docs:build` 通过后再提交。

## 安全

- CF Token、设备密码、云服务账号必须放环境变量。
- 工控学习尽量在离线模拟器中进行。
- 不向公开仓库提交真实厂区网络拓扑、设备 IP、PLC 寄存器配置及机密报文。
