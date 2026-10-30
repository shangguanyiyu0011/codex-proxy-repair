param([string]$Payload,[switch]$Child)
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$OutputEncoding=[Console]::OutputEncoding
$ErrorActionPreference='Stop'
try {
 if($Payload -notmatch '^[A-Za-z0-9+/=]+$' -or $Payload.Length -gt 12000){throw '代理参数无效。'}
 $pkg=Get-AppxPackage -Name 'OpenAI.Codex' | Sort-Object { [version]$_.Version } -Descending | Select-Object -First 1
 if(!$pkg -or $pkg.PackageFamilyName -cne 'OpenAI.Codex_2p2nqsd0c76g0' -or "$($pkg.SignatureKind)" -ne 'Store' -or $pkg.IsDevelopmentMode -or "$($pkg.Status)" -ne 'Ok'){throw '未找到状态正常的官方 Store 版 Codex。'}
 $manifest=Get-AppxPackageManifest $pkg
 $app=@($manifest.Package.Applications.Application | Where-Object {$_.Id -eq 'App'})[0]
 if(!$app -or !$app.Executable){throw '官方应用清单中没有启动程序。'}
 $root=[IO.Path]::GetFullPath($pkg.InstallLocation).TrimEnd('\')+'\'
 $relative=[string]$app.Executable
 if([IO.Path]::IsPathRooted($relative) -or [IO.Path]::GetFileName($relative) -notin @('ChatGPT.exe','Codex.exe')){throw '官方应用启动路径不符合预期。'}
 $exe=[IO.Path]::GetFullPath((Join-Path $root $relative))
 if(!$exe.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $exe -PathType Leaf)){throw '官方应用启动程序不可用。'}
 $result=Join-Path $PSScriptRoot 'result.txt'
 if($Child){
  Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public static class PackageCheck {
 [DllImport("kernel32.dll",SetLastError=true)] public static extern IntPtr OpenProcess(uint access,bool inherit,int id);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern int GetPackageFamilyName(IntPtr h,ref uint n,StringBuilder b);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern int GetApplicationUserModelId(IntPtr h,ref uint n,StringBuilder b);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
 public static bool Matches(int id,string family) { IntPtr h=OpenProcess(0x1000,false,id); if(h==IntPtr.Zero)return false; try { uint n=0; if(GetPackageFamilyName(h,ref n,null)!=122)return false; var b=new StringBuilder((int)n); if(GetPackageFamilyName(h,ref n,b)!=0||b.ToString()!=family)return false; n=0;if(GetApplicationUserModelId(h,ref n,null)!=122)return false;b=new StringBuilder((int)n);return GetApplicationUserModelId(h,ref n,b)==0&&b.ToString()==family+"!App"; } finally {CloseHandle(h);} }
}
'@
  if(![PackageCheck]::Matches($PID,$pkg.PackageFamilyName)){throw '启动组件未获得官方包身份。'}
  $p=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Payload)) | ConvertFrom-Json
  foreach($value in @($p.http,$p.https)){ $u=[uri]$value; if($u.Scheme -ne 'http' -or !$u.Host -or $u.Port -le 0 -or $u.UserInfo -or $u.AbsolutePath -ne '/' -or $u.Query -or $u.Fragment){throw '代理参数无效。'} }
  $env:HTTP_PROXY=$p.http; $env:HTTPS_PROXY=$p.https; if($p.http -eq $p.https){$env:ALL_PROXY=$p.https}else{Remove-Item Env:ALL_PROXY -ErrorAction SilentlyContinue};$env:NO_PROXY='127.0.0.1,localhost,::1'
  $running=@(Get-CimInstance Win32_Process -Filter "Name='ChatGPT.exe' OR Name='Codex.exe'" | Where-Object { (!$_.ExecutablePath) -or ($_.ExecutablePath -like '*\OpenAI.Codex_*\app\ChatGPT.exe') -or ($_.ExecutablePath -like '*\OpenAI.Codex_*\app\Codex.exe') })
  if($running.Count){throw 'Codex 正在运行。'}
  $client=Start-Process -FilePath $exe -PassThru
  Start-Sleep -Milliseconds 500
  if(![PackageCheck]::Matches($client.Id,$pkg.PackageFamilyName)){throw '新客户端包身份验证失败。'}
  $tmp=$result+'.tmp'; Set-Content -LiteralPath $tmp -Value ('OK '+$client.Id) -Encoding UTF8; Move-Item -LiteralPath $tmp -Destination $result
  exit 0
 }
 $running=@(Get-CimInstance Win32_Process -Filter "Name='ChatGPT.exe' OR Name='Codex.exe'" | Where-Object { (!$_.ExecutablePath) -or ($_.ExecutablePath -like '*\OpenAI.Codex_*\app\ChatGPT.exe') -or ($_.ExecutablePath -like '*\OpenAI.Codex_*\app\Codex.exe') })
 if($running.Count){throw 'Codex 正在运行，请先正常退出（包括托盘）后重试。'}
 $args2="-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSCommandPath`" -Payload $Payload -Child"
 Invoke-CommandInDesktopPackage -PackageFamilyName $pkg.PackageFamilyName -AppId App -Command "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Args $args2 -PreventBreakaway -ErrorAction Stop
 $deadline=(Get-Date).AddSeconds(25);while(!(Test-Path -LiteralPath $result)){if((Get-Date) -ge $deadline){throw '启动验证超时，请先检查 Codex 是否已打开。'};Start-Sleep -Milliseconds 250}
 $line=Get-Content -LiteralPath $result -Raw
 if($line -notmatch '^OK \d+'){throw '应用启动失败，请检查官方安装。'}
 Write-Output 'OK';exit 0
} catch {
 if($Child){try{$tmp=Join-Path $PSScriptRoot 'result.txt.tmp';Set-Content -LiteralPath $tmp -Value 'ERROR' -Encoding UTF8;Move-Item -LiteralPath $tmp -Destination (Join-Path $PSScriptRoot 'result.txt')}catch{};exit 1}
 Write-Output $_.Exception.Message;exit 1
}
