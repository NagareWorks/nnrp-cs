param(
    [string]$RuntimeRepoRoot = "",
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
if (-not $RuntimeRepoRoot) {
    $RuntimeRepoRoot = Join-Path (Split-Path -Parent $repoRoot) "neural-render-runtime"
}

$bindAddress = Resolve-LoopbackAddress -BindHost $RuntimeHost
$resolvedGatewayPort = Resolve-RequestedPort -Address $bindAddress -RequestedPort $GatewayPort
$resolvedTransportPort = Resolve-RequestedPort -Address $bindAddress -RequestedPort $Port -ReservedPorts @($resolvedGatewayPort)

if ($resolvedGatewayPort -ne $GatewayPort -or $resolvedTransportPort -ne $Port) {
    Write-Host ("[nnrp-cs] remapped runtime smoke ports gateway={0}->{1} transport={2}->{3}" -f $GatewayPort, $resolvedGatewayPort, $Port, $resolvedTransportPort)
}

$runtimeScript = Join-Path $RuntimeRepoRoot "scripts/run_local_dev.ps1"
$reproScript = Join-Path $repoRoot "scripts/repro_native_bridge.ps1"
if (-not $ManagedRoot) {
    $ManagedRoot = Join-Path $repoRoot "src/Nnrp.NativeBridge/bin/Release/netstandard2.1"
}

if (-not $NativeRoot) {
    $NativeRoot = Join-Path $repoRoot "native/nnrp_quic_bridge/target/release"
}

if (-not (Test-Path $runtimeScript)) {
    throw "Runtime script not found: $runtimeScript"
}

if (-not (Test-Path $reproScript)) {
    throw "Native bridge repro script not found: $reproScript"
}

$logRoot = Join-Path $env:TEMP ("nnrp-loopback-smoke-{0}" -f ([guid]::NewGuid().ToString("N")))
$null = New-Item -ItemType Directory -Force -Path $logRoot
$runtimeStdoutPath = Join-Path $logRoot "runtime.stdout.log"
$runtimeStderrPath = Join-Path $logRoot "runtime.stderr.log"
$smokeOutputPath = Join-Path $logRoot "native-smoke.log"
$configPath = Join-Path $logRoot "runtime-loopback.yaml"

$null = $SkipStagePackage

$runtimeArgs = @(
    "-NoLogo",
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $runtimeScript,
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

$runtimeProcess = Start-Process -FilePath "pwsh" `
    -ArgumentList $runtimeArgs `
    -RedirectStandardOutput $runtimeStdoutPath `
    -RedirectStandardError $runtimeStderrPath `
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

    $smokeOutput = if (Test-Path $smokeOutputPath) { Get-Content -Path $smokeOutputPath -Raw } else { "" }
    if ($smokeOutput -match '(?m)^FAIL frame_id=') {
        throw "Native bridge repro reported a frame failure. See $smokeOutputPath"
    }

    if ($smokeOutput -notmatch '(?m)^RESULT frame_id=') {
        throw "Native bridge repro did not emit a RESULT line. See $smokeOutputPath"
    }

    if ($UseAutoTransport) {
        if ($smokeOutput -notmatch '(?m)^OPEN selected_transport=') {
            throw "Auto-transport repro did not emit an OPEN selected_transport line. See $smokeOutputPath"
        }

        if ($smokeOutput -notmatch '(?m)^PROBE_SUMMARY transport=Quic ') {
            throw "Auto-transport repro did not emit a QUIC probe summary. See $smokeOutputPath"
        }

        if ($smokeOutput -notmatch '(?m)^PROBE_SUMMARY transport=Tcp ') {
            throw "Auto-transport repro did not emit a TCP probe summary. See $smokeOutputPath"
        }
    }

    $runtimeStdout = if (Test-Path $runtimeStdoutPath) { Get-Content -Path $runtimeStdoutPath -Raw } else { "" }
    $runtimeStderr = if (Test-Path $runtimeStderrPath) { Get-Content -Path $runtimeStderrPath -Raw } else { "" }
    $runtimeLogs = ($runtimeStdout, $runtimeStderr -join [Environment]::NewLine)

    if ([string]::IsNullOrWhiteSpace($runtimeLogs)) {
        throw "Runtime loopback smoke did not capture any runtime logs. See $runtimeStdoutPath"
    }

    Write-Host ("[nnrp-cs] loopback smoke passed on {0}:{1}" -f $RuntimeHost, $resolvedTransportPort)
    Write-Host ("[nnrp-cs] runtime log: {0}" -f $runtimeStdoutPath)
    Write-Host ("[nnrp-cs] smoke log: {0}" -f $smokeOutputPath)
}
finally {
    if ($runtimeProcess -and -not $runtimeProcess.HasExited) {
        & taskkill.exe /PID $runtimeProcess.Id /T /F | Out-Null
        $runtimeProcess.WaitForExit()
    }

    $logScopedProcesses = Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and
        $null -ne $_.CommandLine -and
        $_.CommandLine -like "*$logRoot*"
    }

    foreach ($logScopedProcess in ($logScopedProcesses | Sort-Object ProcessId -Descending)) {
        try {
            & taskkill.exe /PID $logScopedProcess.ProcessId /T /F | Out-Null
        }
        catch {
            # Best-effort teardown for runtime child processes that already exited.
        }
    }

    if (-not $KeepLogs -and (Test-Path $logRoot)) {
        try {
            Remove-Item -Path $logRoot -Recurse -Force
        }
        catch {
            Write-Warning ("Failed to remove loopback smoke logs at {0}: {1}" -f $logRoot, $_.Exception.Message)
        }
    }
}