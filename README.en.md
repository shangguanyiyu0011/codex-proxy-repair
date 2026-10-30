# Codex Auto Proxy Repair

A Windows utility for proxy inheritance issues affecting the official Store edition of Codex. It reads the current manual system proxy and provides separate **Codex reconnect, Dots connection, and update repair** actions, plus read-only scanning.

[简体中文](README.md) · [Latest release](https://github.com/shangguanyiyu0011/codex-proxy-repair/releases/latest) · [Troubleshooting](docs/TROUBLESHOOTING.md) · [Changelog](CHANGELOG.md)

![Actual utility UI: English main window](docs/screenshots/main-en.png)

Intended for similar cases where a proxy is running and the browser works, but Codex or Dots may not inherit it. This is an independent utility, unaffiliated with OpenAI. It does not promise to fix every connection issue.

## Download

**v1.2.0** · Windows 10/11 x64 · Windows PowerShell 5.1 and system .NET Framework.

| Download | Purpose |
| --- | --- |
| [Standalone EXE](https://github.com/shangguanyiyu0011/codex-proxy-repair/releases/latest/download/codex-proxy-repair-v1.2.0-windows.exe) | Recommended starting point |
| [Portable package](https://github.com/shangguanyiyu0011/codex-proxy-repair/releases/latest/download/codex-proxy-repair-v1.2.0-windows-portable.zip) | EXE and documentation |
| [Script and full source](https://github.com/shangguanyiyu0011/codex-proxy-repair/releases/latest/download/codex-proxy-repair-v1.2.0-script-source.zip) | Windows PowerShell 5.1 fallback and local build |
| [SHA256SUMS.txt](https://github.com/shangguanyiyu0011/codex-proxy-repair/releases/latest/download/SHA256SUMS.txt) | Download checksums |

The EXE is unsigned and Windows may show an unknown-publisher prompt. No self-signed trust certificate is installed. Check the checksum and source, and follow your Windows or organization security policy.

## Use in four steps

1. Enable the proxy application's **manual system proxy**. A browser extension alone is insufficient.
2. Open the EXE, or extract the entire source package and run `Start-Script.cmd`.
3. Run **Auto scan** and follow its suggestion, or select an independent action below. Save your work and exit Codex normally, including its tray process, before a connection launch.
4. Send a real message in Codex. For Dots, also check the device list and try a real task.

| Action | Behavior |
| --- | --- |
| Codex reconnect | Detects the official package identity and launches with the detected proxy. Refuses to launch while Codex is running and asks for a normal exit. |
| Dots connection | Provides a proxy-aware launch route. Does not create pairings or replace account/device authorization. |
| Update repair | Checks Store permissions or downloads, validates, and submits an official x64 package update. |
| Scan / verification | Observes proxy and package state, recent retries, and handshake signals; reports suggestions and uncertainty. |
| Undo / diagnostics | Undoes changes supported by ownership receipts; exports a local whitelist-based diagnostic file. |

## Languages

Simplified Chinese, Traditional Chinese, English, Japanese, and Korean are included. Switching is immediate and remembered for the current user. The initial language follows Windows UI culture; unsupported cultures fall back to English. Native Windows prompts and some system errors follow the OS language.

<details>
<summary>Guide and update UI screenshots</summary>

![Actual utility UI: Chinese usage guide](docs/screenshots/guide-zh-CN.png)

![Actual utility UI: Chinese update window before detection](docs/screenshots/update-zh-CN.png)

The update screenshot is a preview of the controls before detection, not evidence of a successful update.

</details>

## Scope and behavior

- Requires a working installation of the official Store Codex / unified ChatGPT app. Package identity and version are detected dynamically; no country, port, or client version is hard-coded as a requirement.
- Supports a Windows **manual HTTP system proxy**, including mixed proxies with an HTTP endpoint. PAC/WPAD, authenticated proxies, and unknown protocols are unsupported.
- Connection launching does not change system proxy settings or permanent environment variables, alter models or authorization, install services, or terminate Codex.
- Store permission repair requests Windows administrator approval only to add the single Microsoft Store loopback exemption when absent. Current-user DPAPI protects ownership receipts; undo only removes changes attributed to this tool.
- Official update downloads must pass signature, publisher, identity, x64 architecture, and version checks before `Add-AppxPackage` submission. Updates to running applications are deferred, without forced shutdown.
- Diagnostic export stays local and includes whitelisted fields only, without raw logs, tokens, chat text, or full command lines. Runtime state, language preferences, and receipts are current-user data; do not copy runtime records across computers.

**Verification still requires real use.** Zero observed retries do not prove that a message succeeded; handshake signals do not prove full Dots recovery. Scan counts may include other local chats. Unrecognized log formats or package structures remain unknown. See [troubleshooting](docs/TROUBLESHOOTING.md).

## Build from source

The application is native .NET Framework WinForms. The build script uses the C# compiler included with Windows and downloads no third-party dependencies.

From the repository root in **Windows PowerShell 5.1**:

```powershell
.\build.ps1
.\Start-Repair.ps1 -Mode SelfTest
```

Output: `build/Codex（自动代理修复）.exe`. Run EXE self-tests with:

```powershell
& '.\build\Codex（自动代理修复）.exe' --self-test
```

The source supports review and local rebuilding; byte-identical output is not guaranteed. See [SOURCE-README.txt](SOURCE-README.txt) for entry points. Organization policies can also restrict the script fallback.

## Validation

Local validation covered EXE and script self-tests, 40 UI rendering samples across five languages at default and minimum window sizes, and live log-display switching. **No second-computer test has been completed.** Coverage of every proxy, network, and client version is not claimed.

## License

Source is under the [MIT License](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) for platform, font, and brand information.
