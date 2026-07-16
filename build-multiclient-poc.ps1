$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) not found." }
$dist = Join-Path $root "dist"
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$src = @("RenderPatch.cs", "FixCore.cs", "GameFiles.cs", "MultiClientPoc.cs") | ForEach-Object { Join-Path $root "src\$_" }
& $csc /nologo /target:exe /codepage:65001 "/out:$dist\MultiClientPoc.exe" /reference:System.dll /reference:System.Windows.Forms.dll $src
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
Write-Host "Built: $dist\MultiClientPoc.exe"
