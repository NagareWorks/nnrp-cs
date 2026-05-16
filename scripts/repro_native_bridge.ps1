param(
    [string]$RemoteHost = "127.0.0.1",
    [int]$Port,
    [int]$GatewayPort = 0,
    [string]$TlsServerName = "localhost",
    [string]$RequestedModel = "engine-sr",
    [uint32]$RequestedSessionId = 41,
    [uint32[]]$FrameIds = @(303, 304, 305, 306),
    [switch]$PingAfterOpen,
    [uint32[]]$CancelFrameIds = @(),
    [switch]$SkipSubmit,
    [switch]$UseAutoTransport,
    [string]$ManagedRoot,
    [string]$NativeRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$managedRoot = if ($ManagedRoot) { $ManagedRoot } else { Join-Path $repoRoot "src/Nnrp.NativeBridge/bin/Release/netstandard2.1" }
$nativeRoot = if ($NativeRoot) { $NativeRoot } else { Join-Path $repoRoot "native/nnrp_quic_bridge/target/release" }
$tcpPort = if ($GatewayPort -gt 0) { [uint16]$GatewayPort } else { [uint16]$Port }

$env:PATH = "$nativeRoot;$env:PATH"

$coreAssembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $managedRoot "Nnrp.Core.dll"))
$clientAssembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $managedRoot "Nnrp.Client.dll"))
$assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $managedRoot "Nnrp.NativeBridge.dll"))
$packetHelperType = $coreAssembly.GetType("Nnrp.Core.SmokePackets", $true)
$transportPolicyType = $coreAssembly.GetType("Nnrp.Core.TransportPolicy", $true)
$type = $assembly.GetType("Nnrp.NativeBridge.NnrpNativeQuicClient", $true)
$clientProfileType = $clientAssembly.GetType("Nnrp.Client.ClientProfile", $true)
$probeOptionsType = $clientAssembly.GetType("Nnrp.Client.NnrpTransportProbeOptions", $true)
$autoOptionsType = $assembly.GetType("Nnrp.NativeBridge.NnrpAutoTransportClientOptions", $true)
$autoClientType = $assembly.GetType("Nnrp.NativeBridge.NnrpAutoTransportClient", $true)
$frameSubmitType = $coreAssembly.GetType("Nnrp.Core.FrameSubmitMessage", $true)
$pingMessageType = $coreAssembly.GetType("Nnrp.Core.PingMessage", $true)
$frameCancelType = $coreAssembly.GetType("Nnrp.Core.FrameCancelMessage", $true)
$openMethod = $type.GetMethod("Open")
$submitMethod = $type.GetMethod("Submit", [Type[]]@([uint64], $frameSubmitType))
$pingMethod = $type.GetMethod("Ping", [Type[]]@([uint64], $pingMessageType))
$cancelMethod = $type.GetMethod("Cancel", [Type[]]@([uint64], $frameCancelType))
$closeMethod = $type.GetMethod("Close")
$connectAutoMethod = $autoClientType.GetMethod("ConnectAsync", [Type[]]@($probeOptionsType, [uint64], [System.Threading.CancellationToken]))
$submitAutoMethod = $autoClientType.GetMethod("SubmitAsync", [Type[]]@([byte[]], [System.Threading.CancellationToken]))
$pingAutoMethod = $autoClientType.GetMethod("PingAsync", [Type[]]@([byte[]], [System.Threading.CancellationToken]))
$cancelAutoMethod = $autoClientType.GetMethod("CancelAsync", [Type[]]@([byte[]], [System.Threading.CancellationToken]))
$closeAutoMethod = $autoClientType.GetMethod("CloseAsync", [Type[]]@([string], [uint64], [System.Threading.CancellationToken]))
$buildSubmitMethod = $packetHelperType.GetMethod("CreateSmokeFrameSubmitMessage")
$buildSubmitPacketMethod = $packetHelperType.GetMethod("BuildSmokeFrameSubmitPacket")
$describeResultPacketMethod = $packetHelperType.GetMethod("DescribeResultPacket")
$pingCreateMethod = $pingMessageType.GetMethod("Create")
$cancelCreateMethod = $frameCancelType.GetMethod("Create")

function New-SubmitMessage {
    param(
        [uint32]$SessionId,
        [uint32]$FrameId
    )

    return ,($buildSubmitMethod.Invoke($null, [object[]]@([uint32]$SessionId, [uint32]$FrameId, [uint16]0, [uint64]0)))
}

function New-SubmitPacket {
    param(
        [uint32]$SessionId,
        [uint32]$FrameId
    )

    return [byte[]]($buildSubmitPacketMethod.Invoke($null, [object[]]@([uint32]$SessionId, [uint32]$FrameId, [uint16]0, [uint64]0)))
}

function Wait-Awaitable {
    param([object]$Awaitable)

    if ($null -eq $Awaitable) {
        return ,$null
    }

    return ,($Awaitable.GetAwaiter().GetResult())
}

function Get-ResultSummaryJson {
    param([byte[]]$PacketBytes)

    $invokeArgs = New-Object object[] 1
    $invokeArgs[0] = $PacketBytes
    $summary = $describeResultPacketMethod.Invoke($null, $invokeArgs)
    $summaryType = $summary.GetType()
    return @{
        frame_id = $summaryType.GetProperty("FrameId").GetValue($summary)
        msg_type = [string]$summaryType.GetProperty("MessageType").GetValue($summary)
        session_id = $summaryType.GetProperty("SessionId").GetValue($summary)
        tile_count = $summaryType.GetProperty("TileCount").GetValue($summary)
    } | ConvertTo-Json -Compress
}

function Invoke-DirectQuicRepro {
    $openResult = $openMethod.Invoke($null, [object[]]@($RemoteHost, [uint16]$Port, $TlsServerName, $RequestedModel, $RequestedSessionId))
    $openType = $openResult.GetType()
    $handle = [uint64]$openType.GetProperty("Handle").GetValue($openResult)
    $negotiatedSessionId = [uint32]$openType.GetProperty("NegotiatedSessionId").GetValue($openResult)
    $negotiatedWireFormat = $openType.GetProperty("NegotiatedWireFormat").GetValue($openResult)
    $activeModelName = [string]$openType.GetProperty("ActiveModelName").GetValue($openResult)

    Write-Output ("OPEN handle={0} requested_wire_format={1} negotiated_wire_format={2} negotiated_session_id={3} active_model_name={4}" -f $handle, 0, $negotiatedWireFormat, $negotiatedSessionId, $activeModelName)

    try {
        if ($PingAfterOpen) {
            $pingMessage = $pingCreateMethod.Invoke($null, [object[]]@([uint32]$negotiatedSessionId, [uint64]1))
            $pongMessage = $pingMethod.Invoke($null, [object[]]@([uint64]$handle, $pingMessage))
            $pongHeader = $pongMessage.GetType().GetProperty("Header").GetValue($pongMessage)
            $pongHeaderType = $pongHeader.GetType()
            Write-Output ("PING trace_id={0} session_id={1} msg_type={2}" -f 
                $pongHeaderType.GetProperty("TraceId").GetValue($pongHeader),
                $pongHeaderType.GetProperty("SessionId").GetValue($pongHeader),
                $pongHeaderType.GetProperty("MessageType").GetValue($pongHeader))
        }

        foreach ($cancelFrameId in $CancelFrameIds) {
            $cancelMessage = $cancelCreateMethod.Invoke($null, [object[]]@([uint32]$negotiatedSessionId, [uint32]$cancelFrameId, [uint16]0, [uint64]0))
            $null = $cancelMethod.Invoke($null, [object[]]@([uint64]$handle, $cancelMessage))
            Write-Output ("CANCEL frame_id={0}" -f $cancelFrameId)
        }

        foreach ($frameId in $FrameIds) {
            Write-Output ("SUBMIT frame_id={0}" -f $frameId)
            $submitMessage = New-SubmitMessage -SessionId $negotiatedSessionId -FrameId $frameId
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                $invokeArgs = New-Object object[] 2
                $invokeArgs[0] = [uint64]$handle
                $invokeArgs[1] = $submitMessage
                $resultMessage = $submitMethod.Invoke($null, $invokeArgs)
                $sw.Stop()
                $resultType = $resultMessage.GetType()
                $header = $resultType.GetProperty("Header").GetValue($resultMessage)
                $metadata = $resultType.GetProperty("Metadata").GetValue($resultMessage)
                $headerType = $header.GetType()
                $metadataType = $metadata.GetType()
                $decoded = @{
                    frame_id = $headerType.GetProperty("FrameId").GetValue($header)
                    msg_type = $headerType.GetProperty("MessageType").GetValue($header).ToString()
                    session_id = $headerType.GetProperty("SessionId").GetValue($header)
                    tile_count = $metadataType.GetProperty("TileCount").GetValue($metadata)
                } | ConvertTo-Json -Compress
                Write-Output ("RESULT frame_id={0} elapsed_ms={1} decoded={2}" -f $frameId, $sw.ElapsedMilliseconds, $decoded)
            }
            catch {
                $sw.Stop()
                $message = $_.Exception.Message
                $inner = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { "" }
                Write-Output ("FAIL frame_id={0} elapsed_ms={1} error={2} inner={3}" -f $frameId, $sw.ElapsedMilliseconds, $message, $inner)
                break
            }
        }
    }
    finally {
        $null = $closeMethod.Invoke($null, [object[]]@([uint64]$handle))
        Write-Output "CLOSE done"
    }
}

function Invoke-AutoTransportRepro {
    $profile = [Activator]::CreateInstance($clientProfileType)
    $clientProfileType.GetProperty("TransportPolicy").SetValue($profile, [Enum]::Parse($transportPolicyType, "Auto"))
    $probeOptions = [Activator]::CreateInstance($probeOptionsType)
    $probeOptionsType.GetProperty("WarmupProbeCount").SetValue($probeOptions, 0)
    $probeOptionsType.GetProperty("ScoredProbeCount").SetValue($probeOptions, 1)
    $probeOptionsType.GetProperty("PayloadBytes").SetValue($probeOptions, 64)

    $clientOptions = [Activator]::CreateInstance(
        $autoOptionsType,
        [object[]]@(
            $RemoteHost,
            [uint16]$Port,
            $TlsServerName,
            $RequestedModel,
            [uint32]$RequestedSessionId,
            [uint16]$tcpPort))
    $client = [Activator]::CreateInstance($autoClientType, [object[]]@($profile, $clientOptions))

    try {
        $connectResult = Wait-Awaitable ($connectAutoMethod.Invoke($client, [object[]]@($probeOptions, [uint64]0, [System.Threading.CancellationToken]::None)))
        $connectResultType = $connectResult.GetType()
        $selectedTransport = $connectResultType.GetProperty("SelectedTransportId").GetValue($connectResult)
        $selectedBinding = [string]$connectResultType.GetProperty("SelectedBindingName").GetValue($connectResult)
        $negotiatedSessionId = [uint32]$connectResultType.GetProperty("NegotiatedSessionId").GetValue($connectResult)
        $negotiatedWireFormat = $connectResultType.GetProperty("NegotiatedWireFormat").GetValue($connectResult)
        $activeModelName = [string]$connectResultType.GetProperty("ActiveModelName").GetValue($connectResult)
        $wasProbed = [bool]$connectResultType.GetProperty("WasProbed").GetValue($connectResult)

        Write-Output ("OPEN selected_transport={0} selected_binding={1} was_probed={2} requested_wire_format={3} negotiated_wire_format={4} negotiated_session_id={5} active_model_name={6}" -f $selectedTransport, $selectedBinding, $wasProbed, 0, $negotiatedWireFormat, $negotiatedSessionId, $activeModelName)

        $probeSelection = $connectResultType.GetProperty("ProbeSelection").GetValue($connectResult)
        if ($null -ne $probeSelection) {
            foreach ($summary in $probeSelection.Summaries) {
                $summaryType = $summary.GetType()
                Write-Output ("PROBE_SUMMARY transport={0} binding={1} success_count={2} failure_count={3} median_rtt_us={4} median_throughput={5}" -f 
                    $summaryType.GetProperty("TransportId").GetValue($summary),
                    $summaryType.GetProperty("BindingName").GetValue($summary),
                    $summaryType.GetProperty("SuccessCount").GetValue($summary),
                    $summaryType.GetProperty("FailureCount").GetValue($summary),
                    $summaryType.GetProperty("MedianRttMicroseconds").GetValue($summary),
                    [math]::Round([double]$summaryType.GetProperty("MedianThroughputBytesPerSecond").GetValue($summary), 2))
            }
        }

        if ($PingAfterOpen) {
            $pingMessage = $pingCreateMethod.Invoke($null, [object[]]@([uint32]$negotiatedSessionId, [uint64]1))
            $pingPacket = $pingMessage.ToArray()
            $pongPacket = Wait-Awaitable ($pingAutoMethod.Invoke($client, [object[]]@($pingPacket, [System.Threading.CancellationToken]::None)))
            Write-Output ("PING session_id={0} packet_bytes={1}" -f $negotiatedSessionId, $pongPacket.Length)
        }

        foreach ($cancelFrameId in $CancelFrameIds) {
            $cancelMessage = $cancelCreateMethod.Invoke($null, [object[]]@([uint32]$negotiatedSessionId, [uint32]$cancelFrameId, [uint16]0, [uint64]0))
            $cancelPacket = $cancelMessage.ToArray()
            $null = Wait-Awaitable ($cancelAutoMethod.Invoke($client, [object[]]@($cancelPacket, [System.Threading.CancellationToken]::None)))
            Write-Output ("CANCEL frame_id={0}" -f $cancelFrameId)
        }

        if (-not $SkipSubmit) {
            foreach ($frameId in $FrameIds) {
                Write-Output ("SUBMIT frame_id={0}" -f $frameId)
                $submitPacket = New-SubmitPacket -SessionId $negotiatedSessionId -FrameId $frameId
                if ($submitPacket -is [object[]]) {
                    if ($submitPacket.Length -eq 1 -and $submitPacket[0] -is [byte[]]) {
                        $submitPacket = [byte[]]$submitPacket[0]
                    }
                    else {
                        $submitPacket = [byte[]]@($submitPacket)
                    }
                }
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                try {
                    $invokeArgs = New-Object object[] 2
                    $invokeArgs[0] = $submitPacket
                    $invokeArgs[1] = [System.Threading.CancellationToken]::None
                    $resultPacket = Wait-Awaitable ($submitAutoMethod.Invoke($client, $invokeArgs))
                    $sw.Stop()
                    $decoded = Get-ResultSummaryJson -PacketBytes $resultPacket
                    Write-Output ("RESULT frame_id={0} elapsed_ms={1} decoded={2}" -f $frameId, $sw.ElapsedMilliseconds, $decoded)
                }
                catch {
                    $sw.Stop()
                    $message = $_.Exception.Message
                    $inner = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { "" }
                    Write-Output ("FAIL frame_id={0} elapsed_ms={1} error={2} inner={3}" -f $frameId, $sw.ElapsedMilliseconds, $message, $inner)
                    break
                }
            }
        }
    }
    finally {
        $null = Wait-Awaitable ($closeAutoMethod.Invoke($client, [object[]]@("auto-transport-repro", [uint64]0, [System.Threading.CancellationToken]::None)))
        if ($client -is [System.IDisposable]) {
            $client.Dispose()
        }
        Write-Output "CLOSE done"
    }
}

if ($UseAutoTransport) {
    Invoke-AutoTransportRepro
}
else {
    Invoke-DirectQuicRepro
}
