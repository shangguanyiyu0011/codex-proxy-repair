Codex 自动代理修复 v1.2.1 源码

构建
使用 Windows PowerShell 5.1 执行 build.ps1。脚本调用 Windows 自带的 .NET Framework C# 编译器，不下载第三方包。
输出位于 build/Codex（自动代理修复）.exe。EXE 内置三个 PowerShell 组件，不要求用户另带脚本。

源代码
src/Main.cs：原生界面、代理解析、入口与扫描。
src/ServiceVerification.cs：有界只读复检、兼容预检。
src/RepairLedger.cs：本机持久撤销记录及沙盒自测。
src/UpdateRepair.cs、src/UpdateHelper.ps1：商店权限和官方签名更新路线。
src/ToolDiagnostics.cs：白名单脱敏诊断，仅本地导出。
src/LaunchHelper.ps1、src/ScanHelper.ps1：官方包身份启动与只读进程观察。

验证
EXE --self-test 运行不改真实设置的自检。
Windows PowerShell 5.1 中也可运行 .\Start-Repair.ps1 -Mode SelfTest。
EXE --diagnose 或 --scan <输出JSON路径> 只读诊断。
系统或官方客户端的包结构、日志格式不认识时，结果必须保持未知，不应将零错误解释为服务正常。

签名
发布 EXE 未签名，没有生成或安装自签名信任证书。
获得受目标用户信任的代码签名证书后，可运行 sign-build.ps1 -CertificateThumbprint <已安装证书指纹>，按服务商要求提供 TimestampServer。
不要把私钥或口令加入源码。签名后重新生成SHA256校验值及压缩包。

分发状态
项目主页：https://github.com/shangguanyiyu0011/codex-proxy-repair
此源码包附本地构建脚本，支持审阅与重建，不承诺不同环境下产物逐字节一致。
未对另一台电脑做实机测试；不声称所有版本、代理软件或网络故障均适用。

多语言实现
src/Localization.cs：仅本地化显示文本，内部状态比较和修复逻辑保持原样。
src/locales/{en,zh-TW,ja,ko}.json：四套 UTF-8 翻译；简体中文为原文。
语言资源同时嵌入 EXE 与脚本动态程序集，无需联网下载翻译。
新增语言或提示时须同步资源键、检查完整性，并确认长文本排版。
语言偏好独立于修复账本目录，使用 LocalAppData/CodexAutoProxyRepairSettings/language.txt。
