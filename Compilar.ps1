param([string]$Arquivo = 'PosFormatacao.exe')

$ErrorActionPreference = 'Stop'
$framework = if ([Environment]::Is64BitOperatingSystem) { 'Framework64' } else { 'Framework' }
$compiler = Join-Path $env:WINDIR "Microsoft.NET\$framework\v4.0.30319\csc.exe"
$output = Join-Path $PSScriptRoot $Arquivo
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 "/out:$output" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll `
    "/resource:$PSScriptRoot\catalogo.tsv,Catalogo" "/resource:$PSScriptRoot\Drivers.ps1,Motor" `
    "$PSScriptRoot\PosFormatacao.cs"
if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao ($LASTEXITCODE)." }
Write-Host "Executavel criado em $output"
