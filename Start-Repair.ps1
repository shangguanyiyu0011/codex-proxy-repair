[CmdletBinding()]
param([ValidateSet('Gui','SelfTest','Diagnose','Scan','Export')][string]$Mode='Gui',[string]$OutputPath='')
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$OutputEncoding=[Console]::OutputEncoding
try {
 Add-Type -AssemblyName System.Windows.Forms
 $taskSources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)+(Join-Path $PSScriptRoot 'AssemblyInfo.cs')
 if($taskSources.Count -lt 6){throw 'Source files are missing. Extract the complete script package first.'}
 $taskCompiler=New-Object CodeDom.Compiler.CompilerParameters
 $taskCompiler.GenerateInMemory=$true
 $taskCompiler.GenerateExecutable=$false
 foreach($taskReference in @('System.dll','System.Core.dll','System.Security.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll')){[void]$taskCompiler.ReferencedAssemblies.Add($taskReference)}
 $taskOptions=@('/optimize+','/platform:anycpu')
 foreach($taskLocale in @('en','zh-TW','ja','ko')){
  $taskLocalePath=Join-Path $PSScriptRoot ('src/locales/'+$taskLocale+'.json')
  if(-not (Test-Path -LiteralPath $taskLocalePath)){throw ('Language catalog missing: '+$taskLocale)}
  $taskOptions+=('/resource:"'+$taskLocalePath+'",CodexAutoProxyRepair.Locales.'+$taskLocale+'.json')
 }
 foreach($taskScript in @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.ps1' -File | Sort-Object Name)){$taskOptions+=('/resource:"'+$taskScript.FullName+'",CodexAutoProxyRepair.'+$taskScript.Name)}
 $taskCompiler.CompilerOptions=$taskOptions -join ' '
 $taskTypes=Add-Type -Path $taskSources -CompilerParameters $taskCompiler -PassThru
 $taskAssembly=$taskTypes[0].Assembly
 function Invoke-ToolStatic([string]$type,[string]$method,[object[]]$arguments){$taskType=$taskAssembly.GetType('CodexAutoProxyRepair.'+$type,$true);$taskMethod=$taskType.GetMethod($method,[Reflection.BindingFlags]'Public,NonPublic,Static');return $taskMethod.Invoke($null,$arguments)}
 if($Mode -eq 'SelfTest'){
  foreach($taskType in @('L10n','ProxyConfig','RepairLedger','ToolDiagnostics')){$taskExit=Invoke-ToolStatic $taskType 'SelfTest' @();if($taskExit -ne 0){throw ('Self-test failed: '+$taskType+' / '+$taskExit)}}
  Write-Output 'Self-tests passed.';exit 0
 }
 if($Mode -eq 'Export'){if(!$OutputPath){throw 'OutputPath is required.'};[void](Invoke-ToolStatic 'ToolDiagnostics' 'Write' @($OutputPath));exit 0}
 if($Mode -eq 'Diagnose'){$taskValue=Invoke-ToolStatic 'ProxyConfig' 'Read' @()}
 if($Mode -eq 'Scan'){$taskValue=Invoke-ToolStatic 'Scanner' 'Run' @()}
 if($Mode -in @('Diagnose','Scan')){$taskJson=$taskValue|ConvertTo-Json -Depth 6;if($OutputPath){[IO.File]::WriteAllText($OutputPath,$taskJson,(New-Object Text.UTF8Encoding($true)))}else{Write-Output $taskJson};exit 0}
 $taskArguments=New-Object object[] 1
 $taskArguments[0]=[string[]]@()
 $taskExit=Invoke-ToolStatic 'Program' 'Main' $taskArguments
 exit [int]$taskExit
} catch {
 if($Mode -eq 'Gui'){[void][Windows.Forms.MessageBox]::Show('Unable to start the script edition. Extract the complete package and use Windows PowerShell 5.1.'+[Environment]::NewLine+'脚本版启动未完成。请完整解压并使用 Windows PowerShell 5.1。'+[Environment]::NewLine+$_.Exception.Message,'Codex Auto Proxy Repair')}
 else{Write-Error $_.Exception.Message}
 exit 1
}
