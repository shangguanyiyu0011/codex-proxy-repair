[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$CertificateThumbprint,[string]$TimestampServer='')
$ErrorActionPreference='Stop'
$taskExe=Join-Path $PSScriptRoot 'build\Codex（自动代理修复）.exe'
if(!(Test-Path -LiteralPath $taskExe -PathType Leaf)){throw 'Run build.ps1 first.'}
$taskThumbprint=$CertificateThumbprint.Replace(' ','').ToUpperInvariant()
if($taskThumbprint -notmatch '^[A-F0-9]{40}$'){throw 'Provide the thumbprint of an installed code-signing certificate.'}
$taskCert=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$taskThumbprint) -ErrorAction Stop
if(!$taskCert.HasPrivateKey -or $taskCert.NotAfter -le (Get-Date) -or $taskCert.NotBefore -gt (Get-Date)){throw 'The certificate must be current and have its private key available.'}
if($taskCert.EnhancedKeyUsageList.ObjectId.Value -notcontains '1.3.6.1.5.5.7.3.3'){throw 'The certificate does not allow code signing.'}
if($taskCert.Subject -eq $taskCert.Issuer){throw 'This publishing script does not use self-signed certificates.'}
$taskParameters=@{FilePath=$taskExe;Certificate=$taskCert;HashAlgorithm='SHA256'}
if($TimestampServer){$taskUri=[uri]$TimestampServer;if(!$taskUri.IsAbsoluteUri -or $taskUri.Scheme -notin @('http','https') -or $taskUri.UserInfo){throw 'Invalid timestamp server URL.'};$taskParameters.TimestampServer=$TimestampServer}
$taskSignature=Set-AuthenticodeSignature @taskParameters
if($taskSignature.Status -ne 'Valid'){throw ('Signing did not produce a valid signature: '+$taskSignature.Status)}
$taskSignature | Select-Object Status,@{Name='Signer';Expression={$_.SignerCertificate.Subject}}
Write-Output 'Recreate release checksums and archives after signing. Do not publish earlier hashes.'
