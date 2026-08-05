[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConformanceRoot,

    [Parameter(Mandatory = $true)]
    [string]$NativeRoot,

    [string]$OutputRoot = "artifacts/wire-host-route-e2e",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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

function ProviderJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Transport,

        [Parameter(Mandatory = $true)]
        [string]$ProviderId,

        [Parameter(Mandatory = $true)]
        [bool]$Installed,

        [Parameter(Mandatory = $true)]
        [string[]]$SecurityModes
    )

    @{
        transport = $Transport
        provider_id = $ProviderId
        installed = $Installed
        platforms = @("native")
        security_modes = $SecurityModes
    } | ConvertTo-Json -Compress
}

function Assert-AllCasesPassed {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlanPath,

        [Parameter(Mandatory = $true)]
        [string]$ResultsPath,

        [Parameter(Mandatory = $true)]
        [int]$ExpectedCount
    )

    $plan = Get-Content -Raw -LiteralPath $PlanPath | ConvertFrom-Json
    $results = Get-Content -Raw -LiteralPath $ResultsPath | ConvertFrom-Json
    $plannedIds = @($plan.scenarios | ForEach-Object { [string]$_.id })
    $reported = @($results.results)
    if ($plannedIds.Count -ne $ExpectedCount) {
        throw "Expected $ExpectedCount host-route scenarios, selected $($plannedIds.Count)."
    }

    if ($reported.Count -ne $plannedIds.Count) {
        throw "Host-route result count $($reported.Count) does not match plan count $($plannedIds.Count)."
    }

    $reportedIds = @($reported | ForEach-Object { [string]$_.id })
    if (@(Compare-Object $plannedIds $reportedIds).Count -ne 0) {
        throw "Host-route result identities do not match the execution plan."
    }

    $notPassed = @($reported | Where-Object { [string]$_.outcome -ne "passed" })
    if ($notPassed.Count -ne 0) {
        throw "Host-route scenarios did not pass: $($notPassed.id -join ', ')."
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$conformancePath = (Resolve-Path -LiteralPath $ConformanceRoot).Path
$nativePath = (Resolve-Path -LiteralPath $NativeRoot).Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
$repositoryPrefix = $repositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$isRepositoryRoot = $outputPath.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $isRepositoryRoot -and
    -not $outputPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Wire host-route output must stay inside the repository worktree."
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
$arch = switch ($architecture) {
    ([System.Runtime.InteropServices.Architecture]::X64) { "x64" }
    ([System.Runtime.InteropServices.Architecture]::Arm64) { "arm64" }
    default { throw "Unsupported host-route E2E architecture: $architecture" }
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

$artifactVariables = @{
    tcp = "NNRP_NATIVE_TCP_ARTIFACT_PATH"
    quic = "NNRP_NATIVE_QUIC_ARTIFACT_PATH"
    ipc = "NNRP_NATIVE_IPC_ARTIFACT_PATH"
    websocket = "NNRP_NATIVE_WEBSOCKET_ARTIFACT_PATH"
}
foreach ($transport in $artifactVariables.Keys) {
    $artifact = Join-Path $nativePath "transport-$transport/$rid/$library"
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) {
        throw "Missing $transport host-route artifact: $artifact"
    }

    [Environment]::SetEnvironmentVariable(
        $artifactVariables[$transport],
        (Resolve-Path -LiteralPath $artifact).Path,
        [EnvironmentVariableTarget]::Process)
}

$targetProject = Join-Path $repositoryRoot "tools/Nnrp.WireConformance/Nnrp.WireConformance.csproj"
$targetDll = Join-Path $repositoryRoot "tools/Nnrp.WireConformance/bin/$Configuration/net8.0/Nnrp.WireConformance.dll"
$targetHost = Join-Path $repositoryRoot "tools/Nnrp.WireConformance/bin/$Configuration/net8.0/$targetHostName"
$runner = Join-Path $conformancePath "target/release/$runnerName"
if (-not $NoBuild) {
    Invoke-Checked dotnet "build" $targetProject "--configuration" $Configuration
    Push-Location $conformancePath
    try {
        Invoke-Checked cargo "build" "--locked" "--release" "-p" "nnrp-conformance-runner"
    } finally {
        Pop-Location
    }
}

foreach ($requiredFile in @($targetDll, $targetHost, $runner)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required host-route executable is missing: $requiredFile"
    }
}

$suite = Join-Path $conformancePath "wire-conformance/nnrp-1-preview4/manifest.json"
$scenarios = Join-Path $conformancePath "wire-conformance/nnrp-1-preview4/cases/host-route-e2e.json"
$installedTarget = Join-Path $outputPath "target-installed.json"
$uninstalledTarget = Join-Path $outputPath "target-uninstalled.json"
$installedPlan = Join-Path $outputPath "plan-installed.json"
$uninstalledPlan = Join-Path $outputPath "plan-uninstalled.json"
$installedResults = Join-Path $outputPath "results-installed.json"
$uninstalledResults = Join-Path $outputPath "results-uninstalled.json"
$installedEvidence = Join-Path $outputPath "evidence-installed"
$uninstalledEvidence = Join-Path $outputPath "evidence-uninstalled"

$installedProviders = @(
    (ProviderJson "tcp" "nnrp.transport.tcp.native" $true @("plain", "tls_server_auth")),
    (ProviderJson "quic" "nnrp.transport.quic.native" $true @("tls_server_auth")),
    (ProviderJson "ipc" "nnrp.transport.ipc.native" $true @("plain")),
    (ProviderJson "websocket" "nnrp.transport.websocket.native" $true @("plain"))
)
$manifestArguments = @(
    $targetDll,
    "manifest",
    "--target-name", "nnrp-cs-preview4-host-route",
    "--suite-version", "0.1.0",
    "--mode", "suite_as_client",
    "--mode", "suite_as_server"
)
foreach ($provider in $installedProviders) {
    $manifestArguments += @("--host-route-provider", $provider)
}
$manifestArguments += @(
    "--capability", "host.routes",
    "--max-frame-bytes", "16777216",
    "--max-in-flight", "256",
    "--output", $installedTarget
)
Invoke-Checked dotnet @manifestArguments

$uninstalledProvider = ProviderJson -Transport "quic" -ProviderId "example.transport.quic.uninstalled" -Installed $false -SecurityModes @("tls_server_auth")
$uninstalledManifestArguments = @(
    $targetDll,
    "manifest",
    "--target-name", "nnrp-cs-preview4-host-route-uninstalled",
    "--suite-version", "0.1.0",
    "--mode", "suite_as_server",
    "--host-route-provider", $uninstalledProvider,
    "--capability", "host.routes",
    "--max-frame-bytes", "16777216",
    "--max-in-flight", "256",
    "--output", $uninstalledTarget
)
Invoke-Checked dotnet @uninstalledManifestArguments

$installedPlanArguments = @(
    "wire-plan",
    "--suite", $suite,
    "--target", $installedTarget,
    "--scenarios", $scenarios,
    "--output", $installedPlan,
    "--results-path", $installedResults,
    "--evidence-dir", $installedEvidence
)
Invoke-Checked $runner @installedPlanArguments
$uninstalledPlanArguments = @(
    "wire-plan",
    "--suite", $suite,
    "--target", $uninstalledTarget,
    "--scenarios", $scenarios,
    "--output", $uninstalledPlan,
    "--results-path", $uninstalledResults,
    "--evidence-dir", $uninstalledEvidence
)
Invoke-Checked $runner @uninstalledPlanArguments

$installedRunArguments = @(
    "wire-run",
    "--plan", $installedPlan,
    "--target", $installedTarget,
    "--host-route-target", $targetHost,
    "--output", $installedResults
)
Invoke-Checked $runner @installedRunArguments
$uninstalledRunArguments = @(
    "wire-run",
    "--plan", $uninstalledPlan,
    "--target", $uninstalledTarget,
    "--host-route-target", $targetHost,
    "--output", $uninstalledResults
)
Invoke-Checked $runner @uninstalledRunArguments

$installedValidationArguments = @(
    "validate-wire-results",
    "--plan", $installedPlan,
    "--results", $installedResults
)
Invoke-Checked $runner @installedValidationArguments
$uninstalledValidationArguments = @(
    "validate-wire-results",
    "--plan", $uninstalledPlan,
    "--results", $uninstalledResults
)
Invoke-Checked $runner @uninstalledValidationArguments

Assert-AllCasesPassed $installedPlan $installedResults 9
Assert-AllCasesPassed $uninstalledPlan $uninstalledResults 1

Write-Host "Wire host-route E2E passed: 9 installed scenarios and 1 known-uninstalled scenario."
