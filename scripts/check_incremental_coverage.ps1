param(
    [string]$BaseSha = "",
    [string]$HeadSha = "",
    [double]$Threshold = 90.0,
    [string]$RepoRoot = "",
    [string]$CoverageRoot = "artifacts/test-results"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (& git rev-parse --show-toplevel).Trim()
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    throw "Unable to determine the repository root."
}

Set-Location $RepoRoot

function Get-NormalizedRepoPath {
    param([string]$Path)

    return [System.IO.Path]::GetRelativePath($RepoRoot, [System.IO.Path]::GetFullPath($Path)).Replace("\", "/")
}

function Get-OrCreateCoverageEntry {
    param(
        [hashtable]$CoverageByFile,
        [string]$RelativePath
    )

    if (-not $CoverageByFile.ContainsKey($RelativePath)) {
        $CoverageByFile[$RelativePath] = [pscustomobject]@{
            Executable = [System.Collections.Generic.HashSet[int]]::new()
            Covered = [System.Collections.Generic.HashSet[int]]::new()
        }
    }

    return $CoverageByFile[$RelativePath]
}

$diffArgs = @("diff", "--unified=0", "--diff-filter=AMCR")
if (-not [string]::IsNullOrWhiteSpace($BaseSha) -and -not [string]::IsNullOrWhiteSpace($HeadSha)) {
    $diffArgs += @($BaseSha, $HeadSha)
}

$diffArgs += @("--", "src")
$diffOutput = & git @diffArgs
if ($LASTEXITCODE -ne 0) {
    throw "git diff failed while computing incremental coverage input."
}

$changedFiles = @{}
$currentFile = $null

foreach ($line in $diffOutput) {
    if ($line -match "^\+\+\+\s+b/(.+)$") {
        $path = $matches[1].Replace("\", "/")
        if ($path.StartsWith("src/") -and $path.EndsWith(".cs")) {
            $currentFile = $path
            if (-not $changedFiles.ContainsKey($currentFile)) {
                $changedFiles[$currentFile] = [System.Collections.Generic.HashSet[int]]::new()
            }
        }
        else {
            $currentFile = $null
        }

        continue
    }

    if ($null -eq $currentFile) {
        continue
    }

    if ($line -match "^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@") {
        $startLine = [int]$matches[1]
        $lineCount = if ($matches[2]) { [int]$matches[2] } else { 1 }
        if ($lineCount -le 0) {
            continue
        }

        for ($offset = 0; $offset -lt $lineCount; $offset++) {
            $null = $changedFiles[$currentFile].Add($startLine + $offset)
        }
    }
}

if ($changedFiles.Count -eq 0) {
    Write-Host "No changed production C# lines under src/. Skipping incremental coverage check."
    exit 0
}

$coverageRootPath = if ([System.IO.Path]::IsPathRooted($CoverageRoot)) {
    $CoverageRoot
}
else {
    Join-Path $RepoRoot $CoverageRoot
}

$coverageFiles = @(Get-ChildItem -Path $coverageRootPath -Recurse -Filter "coverage.cobertura.xml" -File -ErrorAction SilentlyContinue)
if ($coverageFiles.Count -eq 0) {
    throw "No coverage.cobertura.xml files were found under '$coverageRootPath'."
}

$coverageByFile = @{}

foreach ($coverageFile in $coverageFiles) {
    [xml]$xml = Get-Content $coverageFile.FullName
    $sourceRoots = @()

    foreach ($sourceNode in @($xml.coverage.sources.source)) {
        $sourceRoot = [string]$sourceNode
        if ([string]::IsNullOrWhiteSpace($sourceRoot) -and $null -ne $sourceNode.InnerText) {
            $sourceRoot = [string]$sourceNode.InnerText
        }

        if (-not [string]::IsNullOrWhiteSpace($sourceRoot)) {
            $sourceRoots += $sourceRoot
        }
    }

    if ($sourceRoots.Count -eq 0) {
        $sourceRoots = @($RepoRoot)
    }

    foreach ($package in @($xml.coverage.packages.package)) {
        foreach ($class in @($package.classes.class)) {
            $filename = [string]$class.filename
            if ([string]::IsNullOrWhiteSpace($filename)) {
                continue
            }

            $resolvedPath = $null
            if ([System.IO.Path]::IsPathRooted($filename)) {
                $resolvedPath = [System.IO.Path]::GetFullPath($filename)
            }
            else {
                foreach ($sourceRoot in $sourceRoots) {
                    $candidatePath = [System.IO.Path]::GetFullPath((Join-Path $sourceRoot $filename))
                    if (Test-Path $candidatePath) {
                        $resolvedPath = $candidatePath
                        break
                    }

                    if ($null -eq $resolvedPath) {
                        $resolvedPath = $candidatePath
                    }
                }
            }

            $relativePath = Get-NormalizedRepoPath $resolvedPath
            $coverageEntry = Get-OrCreateCoverageEntry -CoverageByFile $coverageByFile -RelativePath $relativePath

            foreach ($lineNode in @($class.lines.line)) {
                $lineNumber = [int]$lineNode.number
                $hits = [int]$lineNode.hits
                $null = $coverageEntry.Executable.Add($lineNumber)
                if ($hits -gt 0) {
                    $null = $coverageEntry.Covered.Add($lineNumber)
                }
            }
        }
    }
}

$missingCoverageFiles = [System.Collections.Generic.List[string]]::new()
$uncoveredChangedLines = [System.Collections.Generic.List[object]]::new()
$totalChangedExecutableLines = 0
$totalCoveredChangedLines = 0

foreach ($changedFile in ($changedFiles.Keys | Sort-Object)) {
    if (-not $coverageByFile.ContainsKey($changedFile)) {
        $missingCoverageFiles.Add($changedFile)
        continue
    }

    $coverageEntry = $coverageByFile[$changedFile]
    $fileUncoveredLines = [System.Collections.Generic.List[int]]::new()

    foreach ($lineNumber in $changedFiles[$changedFile]) {
        if (-not $coverageEntry.Executable.Contains($lineNumber)) {
            continue
        }

        $totalChangedExecutableLines++
        if ($coverageEntry.Covered.Contains($lineNumber)) {
            $totalCoveredChangedLines++
            continue
        }

        $fileUncoveredLines.Add($lineNumber)
    }

    if ($fileUncoveredLines.Count -gt 0) {
        $uncoveredChangedLines.Add([pscustomobject]@{
            File = $changedFile
            Lines = ($fileUncoveredLines | Sort-Object | ForEach-Object { $_.ToString() }) -join ", "
        })
    }
}

if ($missingCoverageFiles.Count -gt 0) {
    Write-Host "Missing coverage data for changed production files:"
    foreach ($file in $missingCoverageFiles) {
        Write-Host " - $file"
    }

    exit 1
}

if ($totalChangedExecutableLines -eq 0) {
    Write-Host "No changed executable lines were found in coverage reports. Skipping incremental coverage gate."
    exit 0
}

$incrementalCoverage = [math]::Round((100.0 * $totalCoveredChangedLines) / $totalChangedExecutableLines, 2)
Write-Host "Incremental line coverage: $incrementalCoverage% ($totalCoveredChangedLines/$totalChangedExecutableLines executable changed lines covered)."

if ($incrementalCoverage -lt $Threshold) {
    Write-Host "Changed executable lines that remain uncovered:"
    foreach ($entry in $uncoveredChangedLines) {
        Write-Host " - $($entry.File): $($entry.Lines)"
    }

    Write-Error "Incremental line coverage $incrementalCoverage% is below the required $Threshold%."
    exit 1
}

Write-Host "Incremental line coverage gate passed."