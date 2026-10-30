[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskProjectRoot=$PSScriptRoot
$taskBuildDir=Join-Path $taskProjectRoot 'build'
[void](New-Item -ItemType Directory -Path $taskBuildDir -Force)
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $taskCompiler)){ $taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if(-not (Test-Path -LiteralPath $taskCompiler)){ throw '.NET Framework compiler was not found.' }

Add-Type -AssemblyName System.Drawing
$taskIconPath=Join-Path $taskBuildDir 'application.ico'
$taskPngs=New-Object 'System.Collections.Generic.List[byte[]]'
foreach($taskSize in @(16,32,48,256)){
 $taskBitmap=New-Object System.Drawing.Bitmap($taskSize,$taskSize)
 $taskGraphics=[System.Drawing.Graphics]::FromImage($taskBitmap)
 $taskMemory=New-Object System.IO.MemoryStream
 try {
  $taskGraphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $taskGraphics.Clear([System.Drawing.Color]::Transparent)
  $taskGraphics.ScaleTransform($taskSize/256.0,$taskSize/256.0)
  $taskBackground=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(105,78,210))
  $taskWhitePen=New-Object System.Drawing.Pen([System.Drawing.Color]::White,13)
  $taskWhitePen.StartCap=[System.Drawing.Drawing2D.LineCap]::Round
  $taskWhitePen.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
  $taskOutline=New-Object System.Drawing.Drawing2D.GraphicsPath
  try {
   $taskOutline.AddArc(10,10,54,54,180,90)
   $taskOutline.AddArc(192,10,54,54,270,90)
   $taskOutline.AddArc(192,192,54,54,0,90)
   $taskOutline.AddArc(10,192,54,54,90,90)
   $taskOutline.CloseFigure()
   $taskGraphics.FillPath($taskBackground,$taskOutline)
   $taskGraphics.DrawEllipse($taskWhitePen,48,48,160,160)
   $taskGraphics.DrawEllipse($taskWhitePen,91,48,74,160)
   $taskGraphics.DrawLine($taskWhitePen,53,101,203,101)
   $taskGraphics.DrawLine($taskWhitePen,53,155,203,155)
   $taskBadge=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(34,184,142))
   try { $taskGraphics.FillEllipse($taskBadge,153,153,91,91) } finally {$taskBadge.Dispose()}
   $taskGraphics.DrawLine($taskWhitePen,174,197,190,213)
   $taskGraphics.DrawLine($taskWhitePen,190,213,221,178)
  } finally { $taskOutline.Dispose(); $taskWhitePen.Dispose(); $taskBackground.Dispose() }
  $taskBitmap.Save($taskMemory,[System.Drawing.Imaging.ImageFormat]::Png)
  $taskPngs.Add($taskMemory.ToArray())
 } finally { $taskMemory.Dispose(); $taskGraphics.Dispose(); $taskBitmap.Dispose() }
}
$taskIconStream=[System.IO.File]::Create($taskIconPath)
$taskIconWriter=New-Object System.IO.BinaryWriter($taskIconStream)
try {
 $taskIconWriter.Write([uint16]0);$taskIconWriter.Write([uint16]1);$taskIconWriter.Write([uint16]$taskPngs.Count)
 $taskOffset=[uint32](6+16*$taskPngs.Count)
 $taskSizes=@(16,32,48,256)
 for($taskIndex=0;$taskIndex -lt $taskPngs.Count;$taskIndex++){
  $taskDimension=[byte]0;if($taskSizes[$taskIndex] -lt 256){$taskDimension=[byte]$taskSizes[$taskIndex]}
  $taskIconWriter.Write($taskDimension);$taskIconWriter.Write($taskDimension);$taskIconWriter.Write([byte]0);$taskIconWriter.Write([byte]0)
  $taskIconWriter.Write([uint16]1);$taskIconWriter.Write([uint16]32);$taskIconWriter.Write([uint32]$taskPngs[$taskIndex].Length);$taskIconWriter.Write($taskOffset)
  $taskOffset+=[uint32]$taskPngs[$taskIndex].Length
 }
 foreach($taskPng in $taskPngs){$taskIconWriter.Write([byte[]]$taskPng)}
} finally { $taskIconWriter.Dispose();$taskIconStream.Dispose() }

$taskExePath=Join-Path $taskBuildDir 'Codex（自动代理修复）.exe'
$taskSources=@(Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$taskResources=@()
foreach($taskLocale in @('en','zh-TW','ja','ko')){
 $taskLocalePath=Join-Path $taskProjectRoot ('src/locales/'+$taskLocale+'.json')
 if(-not (Test-Path -LiteralPath $taskLocalePath)){throw ('Language catalog missing: '+$taskLocale)}
 $taskResources+=('/resource:'+$taskLocalePath+',CodexAutoProxyRepair.Locales.'+$taskLocale+'.json')
}
foreach($taskScript in @(Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'src') -Filter '*.ps1' -File | Sort-Object Name)){
 $taskScriptText=[IO.File]::ReadAllText($taskScript.FullName,[Text.Encoding]::UTF8)
 [IO.File]::WriteAllText($taskScript.FullName,$taskScriptText,(New-Object Text.UTF8Encoding($true)))
 $taskResources+=('/resource:'+$taskScript.FullName+',CodexAutoProxyRepair.'+$taskScript.Name)
}
$taskCompilerArgs=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/debug-','/utf8output','/r:System.dll','/r:System.Core.dll','/r:System.Security.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll',('/win32manifest:'+(Join-Path $taskProjectRoot 'app.manifest')),('/win32icon:'+$taskIconPath),('/out:'+$taskExePath),(Join-Path $taskProjectRoot 'AssemblyInfo.cs'))+$taskResources+$taskSources
& $taskCompiler @taskCompilerArgs
if($LASTEXITCODE -ne 0){throw "Build failed with exit code $LASTEXITCODE"}
Get-Item -LiteralPath $taskExePath | Select-Object FullName,Length
