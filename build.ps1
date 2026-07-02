# Builds dist\AngelsOnlineFixRes.exe with the in-box .NET Framework compiler
# (no SDK needed). Embeds the icon and the version-info resource.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) not found." }

$dist = Join-Path $root "dist"
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$out  = Join-Path $dist "AngelsOnlineFixRes.exe"
$icon = Join-Path $root "angel.ico"
$src  = @(
  (Join-Path $root "src\RenderPatch.cs"),
  (Join-Path $root "src\FixCore.cs"),
  (Join-Path $root "src\GameFiles.cs"),
  (Join-Path $root "src\AssemblyInfo.cs"),
  (Join-Path $root "src\Program.cs")
)
& $csc /nologo /target:winexe /codepage:65001 "/out:$out" "/win32icon:$icon" `
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
  $src
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
Write-Host "Built: $out"
