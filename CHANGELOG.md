# 更新记录 / Changelog

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
