param(
 [ValidateSet('Inspect','ApplyUpdate')][string]$Mode='Inspect',
 [Parameter(Mandatory=$true)][string]$ResultPath,
 [string]$PackagePath=''
)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
function Get-OfficialPackage {
 $p=Get-AppxPackage -Name 'OpenAI.Codex' | Sort-Object {[version]$_.Version} -Descending | Select-Object -First 1
 if(!$p -or $p.PackageFamilyName -cne 'OpenAI.Codex_2p2nqsd0c76g0' -or "$($p.SignatureKind)" -ne 'Store' -or $p.IsDevelopmentMode -or "$($p.Status)" -ne 'Ok'){return $null}
 return $p
}
function Publish-Result($value) {
 $temporary=$ResultPath+'.new'
 $value | ConvertTo-Json -Depth 5 -Compress | Set-Content -LiteralPath $temporary -Encoding UTF8
 Move-Item -LiteralPath $temporary -Destination $ResultPath -ErrorAction Stop
}
$sourceLock=$null
try {
 $current=Get-OfficialPackage
 if($Mode -eq 'Inspect') {
  $store=Get-AppxPackage -Name 'Microsoft.WindowsStore' | Select-Object -First 1
  $healthyStore=$store -and $store.PackageFamilyName -ceq 'Microsoft.WindowsStore_8wekyb3d8bbwe' -and "$($store.SignatureKind)" -eq 'Store' -and !$store.IsDevelopmentMode -and "$($store.Status)" -eq 'Ok'
  $answer=@{ok=$true;installed=[bool]$current;version='';publisher='';architecture='';storePresent=[bool]$healthyStore}
  if($current){$answer.version="$($current.Version)";$answer.publisher=$current.Publisher;$answer.architecture="$($current.Architecture)"}
  Publish-Result $answer
  exit 0
 }
 if(!$current){throw '未找到状态正常的官方 Store 版 Codex，无法应用此更新。'}
 if("$($current.Architecture)" -ne 'X64'){throw '此更新路线只支持 x64 版 Codex。'}
 $item=Get-Item -LiteralPath $PackagePath -ErrorAction Stop
 if($item.PSIsContainer -or $item.Length -lt 1024 -or $item.Extension -ne '.msix'){throw '候选安装包无效。'}
 $sourceLock=[IO.File]::Open($PackagePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
 $signature=Get-AuthenticodeSignature -LiteralPath $PackagePath
 if("$($signature.Status)" -ne 'Valid'){throw '官方候选包数字签名未通过验证，已停止更新。'}
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead($PackagePath)
 try {
  $entry=$archive.GetEntry('AppxManifest.xml')
  if(!$entry -or $entry.Length -gt 1048576){throw '候选包清单无效。'}
  $settings=New-Object Xml.XmlReaderSettings
  $settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit
  $settings.XmlResolver=$null
  $stream=$entry.Open()
  $reader=[Xml.XmlReader]::Create($stream,$settings)
  try {$manifest=New-Object Xml.XmlDocument;$manifest.XmlResolver=$null;$manifest.Load($reader)} finally {$reader.Dispose();$stream.Dispose()}
 } finally {$archive.Dispose()}
 $identity=$manifest.SelectSingleNode('/*[local-name()="Package"]/*[local-name()="Identity"]')
 if(!$identity -or $identity.GetAttribute('Name') -cne 'OpenAI.Codex' -or $identity.GetAttribute('Publisher') -cne $current.Publisher -or $identity.GetAttribute('ProcessorArchitecture') -ne 'x64'){throw '候选包身份、发布者或架构与当前官方应用不一致。'}
 if($signature.SignerCertificate.Subject -cne $current.Publisher){throw '候选包签名发布者不匹配。'}
 $candidate=[version]$identity.GetAttribute('Version')
 # Re-read immediately before submitting to Windows; Store may have updated meanwhile.
 $now=Get-OfficialPackage
 if(!$now -or $now.Publisher -cne $current.Publisher -or "$($now.Architecture)" -ne 'X64'){throw '当前安装状态已改变，请重新检测后再试。'}
 if($candidate -le [version]$now.Version){
  Publish-Result @{ok=$true;state='up_to_date';version="$($now.Version)";message='当前已是相同或更高版本，无需更新。'}
  exit 0
 }
 Add-AppxPackage -Path $PackagePath -DeferRegistrationWhenPackagesAreInUse -ErrorAction Stop
 $after=Get-OfficialPackage
 $state='staged'
 $message='官方新版已成功暂存。请在当前任务结束后正常退出并重新打开 Codex，使更新生效。'
 if($after -and [version]$after.Version -ge $candidate){$state='applied';$message='官方新版已成功注册。'}
 Publish-Result @{ok=$true;state=$state;version="$candidate";message=$message}
 exit 0
} catch {
 try {Publish-Result @{ok=$false;message=$_.Exception.Message}} catch {Write-Output '无法写入更新结果。'}
 exit 1
} finally {
 if($sourceLock){$sourceLock.Dispose()}
}
