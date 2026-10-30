# 排查指南 / Troubleshooting

## 扫描发现代理缺失或不支持

在代理软件中开启 Windows 手动系统代理，再重新扫描。仅浏览器扩展代理不等于系统代理。混合代理需要提供 HTTP 入口；PAC/WPAD、带账号密码的代理和未知协议暂不支持。工具不替你修改系统代理，也不要求固定国家、端口或节点。

Enable the proxy application's Windows manual system proxy and scan again. Browser extensions alone are insufficient. A mixed proxy must expose HTTP. PAC/WPAD, authenticated proxies, and unknown protocols are unsupported.

## 提示 Codex 仍在运行

先保存任务，再正常退出 Codex，包括托盘图标中的退出操作，然后重试连接入口。工具不会强制结束进程。Dots 连接入口不会配对新设备；账户或设备授权仍需在官方应用中完成。

Save your work and exit Codex normally, including its tray process, then retry. No processes are forcibly terminated. Pairing and authorization must still be completed in the official app.

## 复检没有重试，但仍无法聊天

复检是有界只读观察。零重试可能仅表示观察窗口中没有该信号；计数也可能包含其他本地聊天。请实际发送消息，保留官方客户端显示的错误。Dots 还需检查设备列表并尝试实际任务。未知包结构或日志格式不会被当作成功。

Read-only verification observes a bounded window. Zero retries may only mean no matching signal was seen; counts can include other local chats. Send a real message. For Dots, check the device list and try a real task. Unknown formats are not treated as success.

## Store 修复或更新未完成

Store 权限操作可能需要 Windows 管理员确认，只处理缺少的 Microsoft Store 单项例外。若属于组织策略、账户、服务或其他网络问题，此操作可能无效。官方新版包必须通过签名、发布者、身份、架构和版本检查；失败时不会跳过验证。运行中的应用会延后更新，正常退出后重开检查。下载可取消；验证和提交阶段需等待完成。

Store permission repair may require Windows administrator approval and only adds the missing Store exemption. Organization, account, service, or unrelated network issues may remain. Official package checks are mandatory. Running-app updates are deferred; exit normally and reopen to check. Downloads can be cancelled; let validation and submission finish.

## 未签名提示、脚本不能启动

EXE 未签名，可能显示未知发布者；先对照发布页的 SHA256 校验值和源码，按系统或组织安全策略处理。脚本版必须完整解压并使用 Windows PowerShell 5.1；组织策略仍可能阻止执行。无需安装自签名信任证书或更改永久执行策略。

The EXE is unsigned. Review checksums and source and follow your security policy. Fully extract the script package and use Windows PowerShell 5.1. Organization policy can still block it; no self-signed trust certificate or permanent policy change is required.

## 撤销记录未知或无法读取

撤销依赖当前用户 DPAPI 保护的本机归属凭据。不要将运行记录复制到另一台电脑或账户。原本已有的权限不是工具新增项；记录损坏、中断或归属未知时，工具拒绝猜测撤销。语言偏好独立于撤销记录。

Undo relies on current-user DPAPI-protected local ownership receipts. Do not move runtime records across computers or accounts. Existing permissions are not owned changes. Corrupt, interrupted, or unknown records are not guessed away. Language preferences are stored separately.

## 提交问题时提供什么

可通过“导出诊断”选择本地文件，再自行检查并决定是否附到 issue。该功能仅导出白名单状态，不上传、不包含原始日志、令牌、对话文本或完整命令行。补充工具版本、Windows 版本、EXE 或脚本入口、操作步骤及实际错误；截图前检查是否有个人信息。

Export diagnostics locally, review the file, and decide whether to attach it to an issue. The export uses whitelisted state, uploads nothing, and excludes raw logs, tokens, chat text, and full command lines. Include tool/Windows versions, EXE or script entry, steps, and the actual error. Review screenshots for personal information.
