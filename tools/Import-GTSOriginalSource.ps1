param(
    [string]$Source = "",
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Destination = "src\Legacy\GTSErpSystemOriginal"
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path

function Find-GtsSource {
    $candidates = New-Object System.Collections.Generic.List[string]

    $drives = Get-PSDrive -PSProvider FileSystem

    foreach ($drive in $drives) {
        $root = $drive.Root

        $knownPaths = @(
            (Join-Path $root "ماجد سوفت\MajedSoft 04-08-2026\App\SourceCode\GTSErpSystem"),
            (Join-Path $root "ماجد سوفت\MajedSoft 08-07-2026\App\SourceCode\GTSErpSystem"),
            (Join-Path $root "ماجد سوفت\MajedSoft 04-08-2026\App\GTSErpSystem_Source\GTSErpSystem"),
            (Join-Path $root "ماجد سوفت\MajedSoft 08-07-2026\App\GTSErpSystem_Source\GTSErpSystem")
        )

        foreach ($candidate in $knownPaths) {
            try {
                if (Test-Path -LiteralPath $candidate -PathType Container) {
                    $full = (Resolve-Path -LiteralPath $candidate).Path
                    if (-not $candidates.Contains($full)) {
                        $candidates.Add($full)
                    }
                }
            } catch {
            }
        }
    }

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    Write-Host "Known GTS paths were not found. Searching for GTSErpSystem.csproj..." -ForegroundColor Yellow

    foreach ($drive in $drives) {
        try {
            $projectFiles = Get-ChildItem -LiteralPath $drive.Root -Filter "GTSErpSystem.csproj" -Recurse -File -ErrorAction SilentlyContinue
            foreach ($projectFile in $projectFiles) {
                $folder = (Split-Path -Parent $projectFile.FullName)
                if (Test-Path -LiteralPath $folder -PathType Container) {
                    return (Resolve-Path -LiteralPath $folder).Path
                }
            }
        } catch {
        }
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($Source)) {
    $Source = Find-GtsSource
    if ([string]::IsNullOrWhiteSpace($Source)) {
        throw "GTSErpSystem.csproj was not found on any available drive."
    }
    Write-Host ("GTS source found automatically: " + $Source) -ForegroundColor Green
}

$resolvedSource = (Resolve-Path -LiteralPath $Source).Path
$target = Join-Path $resolvedRoot $Destination

if (-not (Test-Path -LiteralPath $resolvedSource -PathType Container)) {
    throw ("Source folder not found: " + $resolvedSource)
}

New-Item -ItemType Directory -Force -Path $target | Out-Null

Write-Host "Copying original GTS source files..." -ForegroundColor Cyan
Write-Host ("FROM: " + $resolvedSource)
Write-Host ("TO:   " + $target)

$files = Get-ChildItem -LiteralPath $resolvedSource -Recurse -File |
    Where-Object {
        $_.Extension -in ".cs", ".resx", ".config", ".xml", ".json", ".txt"
    }

$count = 0

foreach ($file in $files) {
    $relative = $file.FullName.Substring($resolvedSource.Length).TrimStart("\")
    $destinationFile = Join-Path $target $relative
    $destinationDir = Split-Path -Parent $destinationFile

    New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destinationFile -Force
    $count++
}

Write-Host ("Imported files: " + $count) -ForegroundColor Green
Write-Host "No existing SaqerAccountingSystem files were deleted." -ForegroundColor Green
Write-Host ""
Write-Host "Next commands:" -ForegroundColor Yellow
Write-Host "git status"
Write-Host "git add src/Legacy/GTSErpSystemOriginal"
Write-Host "git commit -m \"Import original GTS ERP source for real screen integration\""
Write-Host "git push origin main"
