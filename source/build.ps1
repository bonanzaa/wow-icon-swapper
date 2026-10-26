$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$appRoot = Split-Path -Parent $PSScriptRoot
& $compiler /nologo /target:winexe /optimize+ "/out:$appRoot\WoW-Icon-Swapper.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\IconSwapper.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host 'Built WoW-Icon-Swapper.exe'
