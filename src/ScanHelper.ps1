param([string]$Payload)
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$OutputEncoding=[Console]::OutputEncoding
$ErrorActionPreference='Stop'
try {
 $proxy=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Payload)) | ConvertFrom-Json
 $uris=@(); if($proxy.http -and $proxy.https){$uris=@([uri]$proxy.http,[uri]$proxy.https)}
 $pairs=@(); $resolutionFailed=$false; foreach($u in $uris){$literal=$null;if([Net.IPAddress]::TryParse($u.DnsSafeHost,[ref]$literal)){$addresses=@($literal)}else{try{$addresses=@([Net.Dns]::GetHostAddresses($u.DnsSafeHost));if(!$addresses.Count){$resolutionFailed=$true}}catch{$addresses=@();$resolutionFailed=$true}};foreach($ip in $addresses){$pairs+=@{IP=$ip.ToString();Port=$u.Port}}}
 $all=@(Get-CimInstance Win32_Process -Filter "Name='codex.exe'");$prefix=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin')).TrimEnd('\')+'\';$package=Get-AppxPackage -Name 'OpenAI.Codex' | Where-Object {$_.PackageFamilyName -ceq 'OpenAI.Codex_2p2nqsd0c76g0' -and $_.SignatureKind -eq 'Store' -and !$_.IsDevelopmentMode -and $_.Status -eq 'Ok'} | Sort-Object {[version]$_.Version} -Descending | Select-Object -First 1;$pkgRoot=if($package){[IO.Path]::GetFullPath($package.InstallLocation).TrimEnd('\')+'\'}else{$null};$items=@($all | Where-Object {$_.ExecutablePath -and ($_.ExecutablePath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or ($pkgRoot -and $_.ExecutablePath.StartsWith($pkgRoot,[StringComparison]::OrdinalIgnoreCase)))})
 $clients=@(Get-CimInstance Win32_Process -Filter "Name='ChatGPT.exe' OR Name='Codex.exe'" | Where-Object {$_.ExecutablePath -like '*\OpenAI.Codex_*\app\ChatGPT.exe' -or $_.ExecutablePath -like '*\OpenAI.Codex_*\app\Codex.exe'})
 $out=@{}
 foreach($role in @('app-server','exec-server')) {
  $processes=@($items | Where-Object {$_.CommandLine -match ('(?:^|\s)'+[regex]::Escape($role)+'(?:\s|$)')})
  if(!$processes.Count){if(@($all | Where-Object {!$_.ExecutablePath -or $_ -notin $items}).Count -or @($items | Where-Object {!$_.CommandLine}).Count){$out[$role]='未知（进程信息或安装结构无法识别）'}else{$out[$role]='未运行，无法判断'};continue}
  if(!$uris.Count){$out[$role]='代理不可用，无法判断';continue}
  try {$conns=@(Get-NetTCPConnection -State Established -ErrorAction Stop | Where-Object {$_.OwningProcess -in @($processes.ProcessId)});$matched=@(foreach($connection in $conns){foreach($pair in $pairs){if(([Net.IPAddress]::Parse($connection.RemoteAddress)).Equals([Net.IPAddress]::Parse($pair.IP)) -and $connection.RemotePort -eq $pair.Port){$connection;break}}}); if($matched.Count){$out[$role]='已观察到代理连接'}elseif($resolutionFailed){$out[$role]='连接状态未知（代理地址解析失败）'}else{$out[$role]='未观察到当前代理连接，可能暂时空闲或未使用代理'}}catch{$out[$role]='连接状态未知（无法读取）'}
 }
 [pscustomobject]@{client=if($clients.Count){'正在运行'}else{'未观察到运行中的客户端'};app=$out['app-server'];executor=$out['exec-server']} | ConvertTo-Json -Compress
}catch { '{"client":"未知（扫描失败）","app":"未知（扫描失败）","executor":"未知（扫描失败）"}' }
