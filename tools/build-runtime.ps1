$ErrorActionPreference='Stop'
$forgeRoot=Split-Path -Parent $PSScriptRoot
$forgeBuild=Join-Path $forgeRoot 'build'
New-Item -ItemType Directory -Force -Path $forgeBuild | Out-Null
$forgeCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$forgeWpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$forgeRefs=@('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Xaml.dll',"$forgeWpf\WindowsBase.dll","$forgeWpf\PresentationCore.dll","$forgeWpf\PresentationFramework.dll")
$forgeArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/define:CUSTOMER_RELEASE',"/out:$forgeBuild\PetForge.exe","/win32manifest:$forgeRoot\runtime\src\app.manifest")
foreach($forgeRef in $forgeRefs){$forgeArgs+="/reference:$forgeRef"}
$forgeArgs+=@(Get-ChildItem -LiteralPath (Join-Path $forgeRoot 'runtime\src') -Filter '*.cs' | Where-Object {$_.Name -notlike '*Tests.cs' -and $_.Name -ne 'Tests.cs' -and $_.Name -ne 'MotionQuality.cs'} | Sort-Object Name | Select-Object -ExpandProperty FullName)
& $forgeCompiler @forgeArgs
if($LASTEXITCODE -ne 0){throw 'Desktop runtime compilation failed'}
Write-Output 'Built offline desktop runtime.'
$forgeValidator=@('/nologo','/target:exe','/platform:x64',"/out:$forgeBuild\ValidateBundle.exe","/reference:$forgeBuild\PetForge.exe")
foreach($forgeRef in $forgeRefs){$forgeValidator+="/reference:$forgeRef"}
$forgeValidator+=(Join-Path $PSScriptRoot 'ValidateBundle.cs')
& $forgeCompiler @forgeValidator
if($LASTEXITCODE -ne 0){throw 'Asset validator compilation failed'}
