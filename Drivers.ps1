#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Drivers', 'ConsultarDrivers')]
    [string]$Modo
)

$ErrorActionPreference = 'Stop'
$script:Falhas = 0
$script:ReinicioPendente = $false

function Registrar-Falha {
    param([string]$Mensagem)

    $script:Falhas++
    Write-Warning $Mensagem
}

function Atualizar-Drivers {
    param([switch]$SomenteConsulta)

    if (-not $SomenteConsulta) {
        $usuario = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
        if (-not $usuario.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw 'Para instalar drivers, autorize a elevacao de administrador no PosFormatacao.'
        }
    }

    Write-Host "`nBuscando drivers no Windows Update. Isso pode levar alguns minutos..."
    $sessao = New-Object -ComObject Microsoft.Update.Session
    $sessao.ClientApplicationID = 'PosFormatacao'
    $busca = $sessao.CreateUpdateSearcher()
    $resultado = $busca.Search("IsInstalled=0 and IsHidden=0 and Type='Driver' and IsAssigned=1 and BrowseOnly=0")
    if ($resultado.ResultCode -ne 2) {
        throw "A busca de drivers nao foi concluida (resultado $($resultado.ResultCode))."
    }
    if ($resultado.Updates.Count -eq 0) {
        Write-Host 'O Windows Update nao ofereceu drivers recomendados para instalar.'
        return
    }

    foreach ($driver in $resultado.Updates) {
        Write-Host "- $($driver.Title)"
    }
    if ($SomenteConsulta) { return }

    $sistema = New-Object -ComObject Microsoft.Update.SystemInfo
    if ($sistema.RebootRequired) {
        $script:ReinicioPendente = $true
        return
    }

    foreach ($driver in $resultado.Updates) {
        try {
            if ($driver.InstallationBehavior.CanRequestUserInput) {
                Registrar-Falha "Driver requer interacao e deve ser instalado pelo Windows Update: $($driver.Title)"
                continue
            }
            if (-not $driver.EulaAccepted) { $driver.AcceptEula() }

            $lote = New-Object -ComObject Microsoft.Update.UpdateColl
            [void]$lote.Add($driver)
            if (-not $driver.IsDownloaded) {
                Write-Host "Baixando $($driver.Title)..."
                $download = $sessao.CreateUpdateDownloader()
                $download.Updates = $lote
                $baixado = $download.Download().GetUpdateResult(0)
                if ($baixado.ResultCode -ne 2) {
                    throw ('Falha no download (0x{0:X8}).' -f $baixado.HResult)
                }
            }

            Write-Host "Instalando $($driver.Title)..."
            $instalador = $sessao.CreateUpdateInstaller()
            $instalador.Updates = $lote
            $instalador.AllowSourcePrompts = $false
            if ($instalador.RebootRequiredBeforeInstallation) {
                $script:ReinicioPendente = $true
                break
            }
            $instalacao = $instalador.Install()
            $script:ReinicioPendente = $instalacao.RebootRequired
            $item = $instalacao.GetUpdateResult(0)
            if ($item.ResultCode -ne 2) {
                throw ('Falha na instalacao (0x{0:X8}).' -f $item.HResult)
            }
            Write-Host 'Driver instalado.'
        } catch {
            Registrar-Falha "$($driver.Title): $($_.Exception.Message)"
        }
        if ($script:ReinicioPendente) { break }
    }
}

if ($Modo) {
    try { Atualizar-Drivers -SomenteConsulta:($Modo -eq 'ConsultarDrivers') }
    catch { Registrar-Falha $_.Exception.Message }
}
if ($script:ReinicioPendente) {
    Write-Host "`nReinicie o Windows e execute a etapa de drivers novamente para verificar o que ainda estiver pendente."
}
if ($script:Falhas -gt 0) {
    Write-Host "`nExecucao encerrada com $script:Falhas falha(s). Consulte os avisos acima."
    exit 1
}
if ($script:ReinicioPendente) { exit 3010 }
exit 0
