param([string]$OutputPath = 'FileTracker.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    $target = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $PSScriptRoot $OutputPath }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/out:$target" /win32icon:assets\FileTracker.ico /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /win32manifest:app.manifest Core.cs App.cs Localization.cs UI.cs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $configSource = Join-Path $PSScriptRoot 'FileTracker.exe.config'
    if (-not [string]::Equals($configSource, "$target.config", [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $configSource -Destination "$target.config"
    }
} finally { Pop-Location }
