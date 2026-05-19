param(
    [string]$ExternalAppRepoRoot = "",
    [string]$RuntimeHost = "127.0.0.1",
    [int]$Port = 50058,
    [int]$GatewayPort = 50051,
    [string]$ManagedRoot = "",
    [string]$NativeRoot = "",
    [string]$TlsServerName = "localhost",
    [string]$RequestedModel = "engine-sr",
    [uint32]$RequestedSessionId = 41,
    [uint32[]]$FrameIds = @(303),
    [uint32[]]$CancelFrameIds = @(302),
    [int]$StartupRetries = 15,
    [int]$StartupDelaySeconds = 1,
    [switch]$SkipStagePackage,
    [switch]$UseAutoTransport,
    [switch]$KeepLogs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-LoopbackAddress {
    param([string]$BindHost)

    $parsed = $null
    if ([System.Net.IPAddress]::TryParse($BindHost, [ref]$parsed)) {
        return $parsed
    }

    $addresses = [System.Net.Dns]::GetHostAddresses($BindHost)
    foreach ($address in $addresses) {
        if ($address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) {
            return $address
        }
    }

    if ($addresses.Length -gt 0) {
        return $addresses[0]
    }

    throw "Could not resolve a bind address for host '$BindHost'."
}

function Test-TcpPortAvailable {
    param(
        [System.Net.IPAddress]$Address,
        [int]$Port
    )

    $listener = [System.Net.Sockets.TcpListener]::new($Address, $Port)
    try {
        $listener.Start()
        return $true
    }
    catch [System.Net.Sockets.SocketException] {
        return $false
    }
    finally {
        $listener.Stop()
    }
}

function Get-FreeTcpPort {
    param([System.Net.IPAddress]$Address)

    $listener = [System.Net.Sockets.TcpListener]::new($Address, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Resolve-RequestedPort {
    param(
        [System.Net.IPAddress]$Address,
        [int]$RequestedPort,
        [int[]]$ReservedPorts = @()
    )

    if ($ReservedPorts -notcontains $RequestedPort -and (Test-TcpPortAvailable -Address $Address -Port $RequestedPort)) {
        return $RequestedPort
    }

    do {
        $candidatePort = Get-FreeTcpPort -Address $Address
    }
    while ($ReservedPorts -contains $candidatePort)

    return $candidatePort
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExternalAppRepoRoot) {
    throw "ExternalAppRepoRoot is required for this opt-in smoke. Pass -ExternalAppRepoRoot explicitly."
}

$bindAddress = Resolve-LoopbackAddress -BindHost $RuntimeHost
$resolvedGatewayPort = Resolve-RequestedPort -Address $bindAddress -RequestedPort $GatewayPort
$resolvedTransportPort = Resolve-RequestedPort -Address $bindAddress -RequestedPort $Port -ReservedPorts @($resolvedGatewayPort)

if ($resolvedGatewayPort -ne $GatewayPort -or $resolvedTransportPort -ne $Port) {
    Write-Host ("[nnrp-cs] remapped external loopback ports gateway={0}->{1} transport={2}->{3}" -f $GatewayPort, $resolvedGatewayPort, $Port, $resolvedTransportPort)
}

$externalAppScript = Join-Path $ExternalAppRepoRoot "scripts/run_local_dev.ps1"
$reproScript = Join-Path $repoRoot "scripts/repro_native_bridge.ps1"
if (-not $ManagedRoot) {
    $ManagedRoot = Join-Path $repoRoot "src/Nnrp.NativeBridge/bin/Release/netstandard2.1"
}

if (-not $NativeRoot) {
    $NativeRoot = Join-Path $repoRoot "native/nnrp_quic_bridge/target/release"
}

if (-not (Test-Path $externalAppScript)) {
    throw "External application script not found: $externalAppScript"
}

if (-not (Test-Path $reproScript)) {
    throw "Native bridge repro script not found: $reproScript"
}

$logRoot = Join-Path $env:TEMP ("nnrp-loopback-smoke-{0}" -f ([guid]::NewGuid().ToString("N")))
$null = New-Item -ItemType Directory -Force -Path $logRoot
$externalStdoutPath = Join-Path $logRoot "external-app.stdout.log"
$externalStderrPath = Join-Path $logRoot "external-app.stderr.log"
$smokeOutputPath = Join-Path $logRoot "native-smoke.log"
$configPath = Join-Path $logRoot "external-loopback.yaml"

$null = $SkipStagePackage

$externalAppArgs = @(
    "-NoLogo",
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $externalAppScript,
    "-Backend",
    "fake",
    "-ListenHost",
    $RuntimeHost,
    "-Port",
    $resolvedGatewayPort,
    "-EnableNnrpTransport",
    "-TransportPort",
    $resolvedTransportPort,
    "-ConfigPath",
    $configPath
)

$externalAppProcess = Start-Process -FilePath "pwsh" `
    -ArgumentList $externalAppArgs `
    -RedirectStandardOutput $externalStdoutPath `
    -RedirectStandardError $externalStderrPath `
    -PassThru `
    -WindowStyle Hidden

try {
    $lastError = $null
    for ($attempt = 1; $attempt -le $StartupRetries; $attempt++) {
        try {
            $reproArgs = @(
                '-NoLogo',
                '-NoProfile',
                '-ExecutionPolicy',
                'Bypass',
                '-File',
                $reproScript,
                '-RemoteHost',
                $RuntimeHost,
                '-Port',
                $resolvedTransportPort,
                '-GatewayPort',
                $(if ($UseAutoTransport) { $resolvedTransportPort } else { $resolvedGatewayPort }),
                '-TlsServerName',
                $TlsServerName,
                '-ManagedRoot',
                $ManagedRoot,
                '-NativeRoot',
                $NativeRoot,
                '-RequestedModel',
                $RequestedModel,
                '-RequestedSessionId',
                $RequestedSessionId,
                '-PingAfterOpen'
            )
            if ($UseAutoTransport) {
                $reproArgs += '-UseAutoTransport'
            }
            $reproArgs += '-CancelFrameIds'
            $reproArgs += $CancelFrameIds
            $reproArgs += '-FrameIds'
            $reproArgs += $FrameIds

            $reproOutput = & pwsh @reproArgs 2>&1
            $reproOutput | Tee-Object -FilePath $smokeOutputPath | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "Native bridge repro exited with code $LASTEXITCODE. See $smokeOutputPath"
            }
            $lastError = $null
            break
        }
        catch {
            $lastError = $_
            if ($attempt -eq $StartupRetries) {
                throw
            }

            Start-Sleep -Seconds $StartupDelaySeconds
        }
    }

    if ($null -ne $lastError) {
        throw $lastError
    }
}
finally {
    if ($null -ne $externalAppProcess -and -not $externalAppProcess.HasExited) {
        Stop-Process -Id $externalAppProcess.Id -Force
    }

    if ($KeepLogs) {
        Write-Host "[nnrp-cs] kept loopback logs at $logRoot"
    }
    elseif (Test-Path $logRoot) {
        Remove-Item -Recurse -Force $logRoot
    }
}