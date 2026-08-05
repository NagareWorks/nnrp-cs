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
