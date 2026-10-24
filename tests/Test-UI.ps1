$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts\ui-preview'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $root
try {
    & $compiler /nologo /target:winexe /platform:x64 /utf8output /main:FileTracker.UiSmokeTests /out:artifacts\ui-preview\UiSmoke.exe /win32icon:assets\FileTracker.ico /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /win32manifest:app.manifest Core.cs App.cs Localization.cs UI.cs tests\UiSmokeTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'UI test build failed' }
    Copy-Item -LiteralPath (Join-Path $root 'FileTracker.exe.config') -Destination (Join-Path $output 'UiSmoke.exe.config')
    Start-Process -FilePath (Join-Path $output 'UiSmoke.exe') -ArgumentList '--headless' -WindowStyle Hidden -Wait
    $run = Get-Content -LiteralPath (Join-Path $output 'latest-run.txt')
    $result = Get-Content -LiteralPath (Join-Path $run 'checks.txt') -Raw
    $passed = ($result -split '\r?\n' | Where-Object { $_ -like 'PASS *' }).Count
    Write-Output "UI checks: $passed passed assertions."
    Write-Output "Rendered previews: $run"
    if ($result -notmatch 'COMPLETE' -or $result -match 'FAIL') { Write-Output $result; throw 'UI checks failed' }
} finally { Pop-Location }
