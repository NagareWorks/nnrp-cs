[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConformanceRoot,

    [Parameter(Mandatory = $true)]
    [string]$NativeRoot,

    [string]$OutputRoot = "artifacts/wire-runtime-e2e",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$pathComparison = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    [System.StringComparison]::OrdinalIgnoreCase
} else {
    [System.StringComparison]::Ordinal
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command $($Arguments -join ' ')"
    }
}

function Invoke-ExpectedCommandFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedText
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Command
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Failed to start expected-failure command: $Command"
        }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            if (-not $process.HasExited) {
                try {
                    $process.Kill($true)
                }
                catch [System.InvalidOperationException] {
                    if (-not $process.HasExited) {
                        throw
                    }
                }
            }
            $process.WaitForExit()
            $stdout = $stdoutTask.GetAwaiter().GetResult()
            $stderr = $stderrTask.GetAwaiter().GetResult()
            throw "Expected-failure command timed out after 30 seconds: $Command $($Arguments -join ' ')`n$stdout`n$stderr"
        }
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $combined = "$stdout`n$stderr"
        if ($process.ExitCode -eq 0) {
            throw "Expected command to fail but it exited successfully: $Command $($Arguments -join ' ')"
        }
        if ($combined.IndexOf($ExpectedText, [System.StringComparison]::Ordinal) -lt 0) {
            throw "Expected command failure to contain '$ExpectedText', got: $combined"
        }
    } finally {
        $process.Dispose()
    }
}

function Copy-JsonDocument {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Document
    )

    return $Document | ConvertTo-Json -Depth 100 | ConvertFrom-Json
}

function Write-JsonDocument {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Document,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $Document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Assert-NoLinkTraversal {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root)
    $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $Path)
    $currentPath = $resolvedRoot
    $pathSegments = @($relativePath -split '[\\/]')
    foreach ($segment in @(".") + $pathSegments) {
        if ($segment -ne ".") {
            $currentPath = Join-Path $currentPath $segment
        }

        $item = Get-Item -Force -LiteralPath $currentPath
        $hasLinkType = $item.PSObject.Properties.Name -contains "LinkType" -and
            -not [string]::IsNullOrEmpty([string]$item.LinkType)
        $isReparsePoint = ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        if ($hasLinkType -or $isReparsePoint) {
            throw "Wire evidence path traverses a symbolic link or reparse point: $currentPath"
        }
    }
}

function Assert-CompleteWireReport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlanPath,

        [Parameter(Mandatory = $true)]
        [string]$ResultsPath,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceRoot
    )

    $plan = Get-Content -Raw -LiteralPath $PlanPath | ConvertFrom-Json
    $report = Get-Content -Raw -LiteralPath $ResultsPath | ConvertFrom-Json
    $resolvedEvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)
    $evidencePrefix = $resolvedEvidenceRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $scenarios = @($plan.scenarios)
    $results = @($report.results)
    if ($scenarios.Count -ne 6) {
        throw "Expected six frozen runtime-control wire scenarios, selected $($scenarios.Count)."
    }

    $expectedModes = @("suite_as_client", "suite_as_proxy", "suite_as_server")
    $actualModes = @($scenarios | ForEach-Object { [string]$_.mode } | Sort-Object -Unique)
    if (@(Compare-Object $expectedModes $actualModes).Count -ne 0) {
        throw "Wire plan does not cover all three runner modes."
    }

    $expectedTransports = @("ipc", "quic", "tcp", "websocket")
    $actualTransports = @($scenarios | ForEach-Object { [string]$_.transport } | Sort-Object -Unique)
    if (@(Compare-Object $expectedTransports $actualTransports).Count -ne 0) {
        throw "Wire plan does not cover TCP, QUIC, IPC, and WebSocket."
    }

    if ($results.Count -ne $scenarios.Count) {
        throw "Wire result count $($results.Count) does not match plan count $($scenarios.Count)."
    }

    foreach ($scenario in $scenarios) {
        $matching = @($results | Where-Object { [string]$_.id -eq [string]$scenario.id })
        if ($matching.Count -ne 1) {
            throw "Wire report must contain exactly one result for $($scenario.id)."
        }

        $result = $matching[0]
        if ([string]$result.outcome -ne "passed") {
            throw "Wire scenario $($scenario.id) did not pass: $($result.message)"
        }
        if ([string]$result.terminal -ne [string]$scenario.expect.terminal) {
            throw "Wire scenario $($scenario.id) reported terminal $($result.terminal), expected $($scenario.expect.terminal)."
        }

        $frames = @($result.observed_frames)
        if ($frames.Count -eq 0) {
            throw "Wire scenario $($scenario.id) did not report observed frames."
        }
        [long]$previousTimestamp = -1
        foreach ($frame in $frames) {
            if ($frame.PSObject.Properties.Name -notcontains "timestamp_us") {
                throw "Wire scenario $($scenario.id) contains a frame without timing evidence."
            }
            [long]$timestamp = $frame.timestamp_us
            if ($timestamp -lt $previousTimestamp) {
                throw "Wire scenario $($scenario.id) contains non-monotonic timing evidence."
            }
            $previousTimestamp = $timestamp
        }

        $evidencePaths = @($result.evidence_paths)
        if ($evidencePaths.Count -ne 1) {
            throw "Wire scenario $($scenario.id) must report exactly one suite-owned evidence path."
        }
        $evidencePath = [System.IO.Path]::GetFullPath([string]$evidencePaths[0])
        if (-not $evidencePath.StartsWith($evidencePrefix, $script:pathComparison)) {
            throw "Wire scenario $($scenario.id) evidence is outside the suite-owned directory: $evidencePath"
        }
        if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
            throw "Wire scenario $($scenario.id) evidence is missing: $evidencePath"
        }
        Assert-NoLinkTraversal -Root $resolvedEvidenceRoot -Path $evidencePath
        if ((Get-Item -LiteralPath $evidencePath).Length -eq 0) {
            throw "Wire scenario $($scenario.id) evidence is empty: $evidencePath"
        }
    }
}

function Assert-ReportValidationFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlanPath,

        [Parameter(Mandatory = $true)]
        [string]$ResultsPath,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceRoot,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedText
    )

    $rejected = $false
    try {
        Assert-CompleteWireReport `
            -PlanPath $PlanPath `
            -ResultsPath $ResultsPath `
            -EvidenceRoot $EvidenceRoot
    } catch {
        if ($_.Exception.Message.IndexOf($ExpectedText, [System.StringComparison]::Ordinal) -lt 0) {
            throw
        }
        $rejected = $true
    }

    if (-not $rejected) {
        throw "Expected report validation to fail with: $ExpectedText"
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$conformancePath = (Resolve-Path -LiteralPath $ConformanceRoot).Path
$nativePath = (Resolve-Path -LiteralPath $NativeRoot).Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
$repositoryPrefix = $repositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$isRepositoryRoot = $outputPath.Equals($repositoryRoot, $pathComparison)
if ($isRepositoryRoot -or
    -not $outputPath.StartsWith($repositoryPrefix, $pathComparison)) {
    throw "Wire runtime output must be a non-root directory inside the repository worktree."
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
$arch = switch ($architecture) {
    ([System.Runtime.InteropServices.Architecture]::X64) { "x64" }
    ([System.Runtime.InteropServices.Architecture]::Arm64) { "arm64" }
    default { throw "Unsupported wire runtime E2E architecture: $architecture" }
}
if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    $rid = "win-$arch"
    $library = "nnrp_ffi.dll"
    $runnerName = "nnrp-conformance-runner.exe"
    $targetHostName = "Nnrp.WireConformance.exe"
} elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::OSX)) {
    $rid = "osx-$arch"
    $library = "libnnrp_ffi.dylib"
    $runnerName = "nnrp-conformance-runner"
    $targetHostName = "Nnrp.WireConformance"
} else {
    $rid = "linux-$arch"
    $library = "libnnrp_ffi.so"
    $runnerName = "nnrp-conformance-runner"
    $targetHostName = "Nnrp.WireConformance"
}

$stagedNativeRoot = Join-Path $outputPath "native-package"
$artifactVariables = @{
    tcp = "NNRP_NATIVE_TCP_ARTIFACT_PATH"
    quic = "NNRP_NATIVE_QUIC_ARTIFACT_PATH"
    ipc = "NNRP_NATIVE_IPC_ARTIFACT_PATH"
    websocket = "NNRP_NATIVE_WEBSOCKET_ARTIFACT_PATH"
}
$resolvedArtifacts = @{}
foreach ($transport in $artifactVariables.Keys) {
    $artifact = Join-Path $nativePath "transport-$transport/$rid/$library"
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) {
        throw "Missing $transport wire runtime artifact: $artifact"
    }

    $resolvedArtifact = (Resolve-Path -LiteralPath $artifact).Path
    $resolvedArtifacts[$transport] = $resolvedArtifact
    [Environment]::SetEnvironmentVariable(
        $artifactVariables[$transport],
        $resolvedArtifact,
        [EnvironmentVariableTarget]::Process)
}

$targetProject = Join-Path $repositoryRoot "tools/Nnrp.WireConformance/Nnrp.WireConformance.csproj"
$targetHost = Join-Path $repositoryRoot "tools/Nnrp.WireConformance/bin/$Configuration/net8.0/$targetHostName"
$runner = Join-Path $conformancePath "target/release/$runnerName"
if (-not $NoBuild) {
    Invoke-Checked dotnet "build" $targetProject "--configuration" $Configuration
    Push-Location $conformancePath
    try {
        Invoke-Checked cargo "build" "--locked" "--release" "-p" "nnrp-conformance-runner" "--bin" "nnrp-conformance-runner"
    } finally {
        Pop-Location
    }
}

foreach ($requiredFile in @($targetHost, $runner)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required wire runtime executable is missing: $requiredFile"
    }
}

$suite = Join-Path $conformancePath "wire-conformance/nnrp-1-preview4/manifest.json"
$targetManifest = Join-Path $outputPath "target.json"
$executionPlan = Join-Path $outputPath "plan.json"
$resultReport = Join-Path $outputPath "results.json"
$evidenceDirectory = Join-Path $outputPath "evidence"
$negativeDirectory = Join-Path $outputPath "negative"
$targetStdout = Join-Path $outputPath "target.stdout.log"
$targetStderr = Join-Path $outputPath "target.stderr.log"

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $targetHost
$startInfo.ArgumentList.Add("serve-target")
$startInfo.ArgumentList.Add("--manifest")
$startInfo.ArgumentList.Add($targetManifest)
$startInfo.ArgumentList.Add("--artifact-root")
$startInfo.ArgumentList.Add($stagedNativeRoot)
$startInfo.WorkingDirectory = $repositoryRoot
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true

$targetProcess = [System.Diagnostics.Process]::new()
$targetProcess.StartInfo = $startInfo
$targetStdoutTask = $null
$targetStderrTask = $null
$targetProcessStarted = $false
try {
    foreach ($transport in $resolvedArtifacts.Keys) {
        $scopedLibrary = if ($IsWindows) {
            "nnrp_ffi_$transport.dll"
        } elseif ($IsMacOS) {
            "libnnrp_ffi_$transport.dylib"
        } else {
            "libnnrp_ffi_$transport.so"
        }
        $stagedDirectory = Join-Path $stagedNativeRoot "runtimes/$rid/native/nnrp/transport/$transport"
        New-Item -ItemType Directory -Force -Path $stagedDirectory | Out-Null
        New-Item `
            -ItemType HardLink `
            -Path (Join-Path $stagedDirectory $scopedLibrary) `
            -Target $resolvedArtifacts[$transport] | Out-Null
    }

    if (-not $targetProcess.Start()) {
        throw "Failed to start the independent C# wire target process."
    }
    $targetProcessStarted = $true
    $targetStdoutTask = $targetProcess.StandardOutput.ReadToEndAsync()
    $targetStderrTask = $targetProcess.StandardError.ReadToEndAsync()

    $ready = $false
    for ($attempt = 0; $attempt -lt 300; $attempt += 1) {
        if (Test-Path -LiteralPath $targetManifest -PathType Leaf) {
            $ready = $true
            break
        }
        if ($targetProcess.HasExited) {
            throw "C# wire target exited before publishing its manifest with exit code $($targetProcess.ExitCode)."
        }
        Start-Sleep -Milliseconds 100
    }
    if (-not $ready) {
        throw "C# wire target did not publish its manifest within 30 seconds."
    }

    Invoke-Checked $runner "wire-plan" `
        "--suite" $suite `
        "--target" $targetManifest `
        "--output" $executionPlan `
        "--results-path" $resultReport `
        "--evidence-dir" $evidenceDirectory

    Invoke-Checked $runner "wire-run" `
        "--plan" $executionPlan `
        "--target" $targetManifest `
        "--output" $resultReport

    Invoke-Checked $runner "validate-wire-results" `
        "--plan" $executionPlan `
        "--results" $resultReport

    Assert-CompleteWireReport `
        -PlanPath $executionPlan `
        -ResultsPath $resultReport `
        -EvidenceRoot $evidenceDirectory

    New-Item -ItemType Directory -Force -Path $negativeDirectory | Out-Null
    $baselineReport = Get-Content -Raw -LiteralPath $resultReport | ConvertFrom-Json

    $missingFrames = Copy-JsonDocument $baselineReport
    $missingFrames.results[0].observed_frames = @()
    $missingFramesPath = Join-Path $negativeDirectory "missing-frames.json"
    Write-JsonDocument -Document $missingFrames -Path $missingFramesPath
    Invoke-ExpectedCommandFailure `
        -Command $runner `
        -Arguments @("validate-wire-results", "--plan", $executionPlan, "--results", $missingFramesPath) `
        -ExpectedText "missing or reordered expected frame TRACE_CONTEXT"

    $unexpectedFrame = Copy-JsonDocument $baselineReport
    $unexpectedFrame.results[0].observed_frames[0].frame = "UNDECLARED_FRAME"
    $unexpectedFramePath = Join-Path $negativeDirectory "unexpected-frame.json"
    Write-JsonDocument -Document $unexpectedFrame -Path $unexpectedFramePath
    Invoke-ExpectedCommandFailure `
        -Command $runner `
        -Arguments @("validate-wire-results", "--plan", $executionPlan, "--results", $unexpectedFramePath) `
        -ExpectedText "unexpected frame UNDECLARED_FRAME"

    $reorderedFrames = Copy-JsonDocument $baselineReport
    $reorderedResult = @(
        $reorderedFrames.results | Where-Object { [string]$_.id -eq "wire.control.cancel-abort.client" }
    )
    if ($reorderedResult.Count -ne 1) {
        throw "Expected exactly one wire.control.cancel-abort.client result when constructing reordered frame evidence."
    }

    $frames = @($reorderedResult[0].observed_frames)
    $traceIndex = -1
    $dropIndex = -1
    for ($index = 0; $index -lt $frames.Count; $index++) {
        if ([string]$frames[$index].frame -eq "TRACE_CONTEXT") {
            $traceIndex = $index
        }
        if ([string]$frames[$index].frame -eq "RESULT_DROP_REASON") {
            $dropIndex = $index
        }
    }
    if ($traceIndex -lt 0 -or $dropIndex -lt 0 -or $traceIndex -ge $dropIndex) {
        throw "Expected TRACE_CONTEXT before RESULT_DROP_REASON when constructing reordered frame evidence."
    }

    $temporaryFrame = $frames[$traceIndex]
    $frames[$traceIndex] = $frames[$dropIndex]
    $frames[$dropIndex] = $temporaryFrame
    $reorderedResult[0].observed_frames = $frames
    $reorderedFramesPath = Join-Path $negativeDirectory "reordered-frames.json"
    Write-JsonDocument -Document $reorderedFrames -Path $reorderedFramesPath
    Invoke-ExpectedCommandFailure `
        -Command $runner `
        -Arguments @("validate-wire-results", "--plan", $executionPlan, "--results", $reorderedFramesPath) `
        -ExpectedText "missing or reordered expected frame RESULT_DROP_REASON"

    $terminalMismatch = Copy-JsonDocument $baselineReport
    $terminalMismatch.results[0].terminal = if ($terminalMismatch.results[0].terminal -eq "error") {
        "success"
    } else {
        "error"
    }
    $terminalMismatchPath = Join-Path $negativeDirectory "terminal-mismatch.json"
    Write-JsonDocument -Document $terminalMismatch -Path $terminalMismatchPath
    Invoke-ExpectedCommandFailure `
        -Command $runner `
        -Arguments @("validate-wire-results", "--plan", $executionPlan, "--results", $terminalMismatchPath) `
        -ExpectedText "terminal mismatch"

    $duplicateScenario = Copy-JsonDocument $baselineReport
    $duplicateScenario.results = @($duplicateScenario.results) + @($duplicateScenario.results[0])
    $duplicateScenarioPath = Join-Path $negativeDirectory "duplicate-scenario.json"
    Write-JsonDocument -Document $duplicateScenario -Path $duplicateScenarioPath
    Invoke-ExpectedCommandFailure `
        -Command $runner `
        -Arguments @("validate-wire-results", "--plan", $executionPlan, "--results", $duplicateScenarioPath) `
        -ExpectedText "duplicate scenario id"

    $missingEvidence = Copy-JsonDocument $baselineReport
    $missingEvidence.results[0].evidence_paths = @()
    $missingEvidencePath = Join-Path $negativeDirectory "missing-evidence.json"
    Write-JsonDocument -Document $missingEvidence -Path $missingEvidencePath
    Assert-ReportValidationFailure `
        -PlanPath $executionPlan `
        -ResultsPath $missingEvidencePath `
        -EvidenceRoot $evidenceDirectory `
        -ExpectedText "exactly one suite-owned evidence path"

    $missingTiming = Copy-JsonDocument $baselineReport
    $missingTiming.results[0].observed_frames[0].PSObject.Properties.Remove("timestamp_us")
    $missingTimingPath = Join-Path $negativeDirectory "missing-timing.json"
    Write-JsonDocument -Document $missingTiming -Path $missingTimingPath
    Assert-ReportValidationFailure `
        -PlanPath $executionPlan `
        -ResultsPath $missingTimingPath `
        -EvidenceRoot $evidenceDirectory `
        -ExpectedText "frame without timing evidence"

    if (-not $targetProcess.WaitForExit(15000)) {
        throw "C# wire target remained alive after all selected scenarios completed."
    }
    $targetProcess.WaitForExit()
    if ($targetProcess.ExitCode -ne 0) {
        throw "C# wire target failed with exit code $($targetProcess.ExitCode)."
    }
} finally {
    if ($targetProcessStarted) {
        if (-not $targetProcess.HasExited) {
            $targetProcess.Kill($true)
            $targetProcess.WaitForExit()
        }
        $targetProcess.WaitForExit()

        $stdout = if ($null -eq $targetStdoutTask) {
            $targetProcess.StandardOutput.ReadToEnd()
        } else {
            $targetStdoutTask.GetAwaiter().GetResult()
        }
        $stderr = if ($null -eq $targetStderrTask) {
            $targetProcess.StandardError.ReadToEnd()
        } else {
            $targetStderrTask.GetAwaiter().GetResult()
        }
        $stdout | Set-Content -LiteralPath $targetStdout
        $stderr | Set-Content -LiteralPath $targetStderr
    }

    $targetProcess.Dispose()
    if (Test-Path -LiteralPath $stagedNativeRoot) {
        Remove-Item -LiteralPath $stagedNativeRoot -Recurse -Force
    }
}

Get-Content -LiteralPath $resultReport
Write-Host "Wire runtime negative validation passed: missing, unexpected, and reordered frames; terminal mismatch; duplicate IDs; evidence; and timing."
