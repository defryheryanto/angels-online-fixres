# Compiles and runs the console test harness using the in-box .NET Framework
# compiler (no SDK required).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) not found." }

$out = Join-Path $root "tests\run-tests.exe"
$src = @(
  (Join-Path $root "src\RenderPatch.cs"),
  (Join-Path $root "src\FixCore.cs"),
  (Join-Path $root "src\GameFiles.cs"),
  (Join-Path $root "tests\Tests.cs")
)
& $csc /nologo /target:exe "/out:$out" $src
if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }
& $out
exit $LASTEXITCODE
