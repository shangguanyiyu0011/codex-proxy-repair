# 更新记录 / Changelog

## v1.2.1

- 创建撤销记录临时文件时显式设置当前用户归属和仅用户/SYSTEM 可写的权限，处理提升权限令牌默认文件所有者的环境差异；保留严格归属校验和 DPAPI 保护。
- 补充隔离自检的失败诊断，Windows 工作流继续执行全部 EXE 与脚本自检，不跳过失败项。
- 更新固定版本的 GitHub Actions 依赖，适配 Node.js 24 运行时。
- 五种语言、界面、三个修复入口及诊断隐私范围保持一致。

English: explicitly assign the current user as owner when atomically creating protected receipt files, preserving strict ownership checks in elevated environments. Add isolated-test diagnostics and update the pinned Windows CI actions.

## v1.2.0

- 原生 .NET Framework WinForms 界面，提供独立 EXE 与 Windows PowerShell 5.1 完整源码备用入口。
- 保留 Codex 重连、Dots 连接、更新修复三个入口，加入自动扫描建议、分级只读复检和使用引导。
- 内置简体中文、繁體中文、English、日本語、한국어，记住当前用户语言选择。
- 动态识别官方包身份与版本，读取 Windows 手动 HTTP 系统代理；连接启动要求先正常退出 Codex。
- Store 单项权限修复、DPAPI 保护的归属记录与持久撤销；官方 x64 包验签和身份检查后提交延后更新。
- 提供仅本地的白名单诊断导出、兼容预检、本地构建与自检入口。
- 完成开发电脑上的 EXE / 脚本自检、40 个多语言界面渲染样本和实际日志显示切换检查；其他电脑尚未验证。
- 以 MIT 许可发布源码。分发 EXE 未签名，未安装自签名信任证书。

English summary: native Windows UI and script fallback, five remembered UI languages, proxy-aware launch actions, read-only verification, scoped Store permission repair with protected undo receipts, validated official updates, and local diagnostic export. Validation is local; cross-computer compatibility is not yet verified.
