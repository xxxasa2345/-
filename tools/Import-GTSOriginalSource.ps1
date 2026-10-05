param(
    [string]$Source = "",
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Destination = "src\Legacy\GTSErpSystemOriginal"
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path $RepositoryRoot).Path

if ([string]::IsNullOrWhiteSpace($Source)) {
    $candidateRoots = Get-PSDrive -PSProvider FileSystem |
        Where-Object { $_.Root -match '^[A-Z]:\\

New-Item -ItemType Directory -Force -Path $target | Out-Null

Write-Host "تم تحديد المصدر تلقائيًا: $resolvedSource" -ForegroundColor Green
Write-Host "نسخ المصدر الأصلي..." -ForegroundColor Cyan
Write-Host "من: $resolvedSource"
Write-Host "إلى: $target"

# No deletion is performed. Existing Saqer files are not touched.
Get-ChildItem -LiteralPath $resolvedSource -Recurse -File |
    Where-Object {
        $_.Extension -in ".cs",".resx",".config",".xml",".json",".txt"
    } |
    ForEach-Object {
        $relative = $_.FullName.Substring($resolvedSource.Length).TrimStart('\')
        $destinationFile = Join-Path $target $relative
        $destinationDir = Split-Path -Parent $destinationFile
        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationFile -Force
    }

$count = (Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object).Count
Write-Host "تم استيراد $count ملفًا." -ForegroundColor Green
Write-Host "لم يتم حذف أي ملف من نظام صقر الحالي." -ForegroundColor Green

Write-Host ""
Write-Host "بعد مراجعة الملفات يمكنك تنفيذ:" -ForegroundColor Yellow
Write-Host "git add src/Legacy/GTSErpSystemOriginal"
Write-Host 'git commit -m "Import original GTS ERP source for real screen integration"'
Write-Host "git push origin main"
 } |
        Select-Object -ExpandProperty Root

    $candidates = New-Object System.Collections.Generic.List[string]

    foreach ($drive in $candidateRoots) {
        $patterns = @(
            (Join-Path $drive "ماجد سوفت\MajedSoft 04-08-2026\App\SourceCode\GTSErpSystem"),
            (Join-Path $drive "ماجد سوفت\MajedSoft 08-07-2026\App\SourceCode\GTSErpSystem"),
            (Join-Path $drive "ماجد سوفت\MajedSoft 04-08-2026\App\GTSErpSystem_Source\GTSErpSystem"),
            (Join-Path $drive "ماجد سوفت\MajedSoft 08-07-2026\App\GTSErpSystem_Source\GTSErpSystem")
        )
        foreach ($candidate in $patterns) {
            if (Test-Path $candidate -PathType Container) {
                $candidates.Add((Resolve-Path $candidate).Path)
            }
        }
    }

    if ($candidates.Count -eq 0) {
        $loose = Get-PSDrive -PSProvider FileSystem |
            Where-Object { $_.Root -match '^[A-Z]:\\

New-Item -ItemType Directory -Force -Path $target | Out-Null

Write-Host "نسخ المصدر الأصلي..." -ForegroundColor Cyan
Write-Host "من: $resolvedSource"
Write-Host "إلى: $target"

# No deletion is performed. Existing Saqer files are not touched.
Get-ChildItem -LiteralPath $resolvedSource -Recurse -File |
    Where-Object {
        $_.Extension -in ".cs",".resx",".config",".xml",".json",".txt"
    } |
    ForEach-Object {
        $relative = $_.FullName.Substring($resolvedSource.Length).TrimStart('\')
        $destinationFile = Join-Path $target $relative
        $destinationDir = Split-Path -Parent $destinationFile
        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationFile -Force
    }

$count = (Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object).Count
Write-Host "تم استيراد $count ملفًا." -ForegroundColor Green
Write-Host "لم يتم حذف أي ملف من نظام صقر الحالي." -ForegroundColor Green

Write-Host ""
Write-Host "بعد مراجعة الملفات يمكنك تنفيذ:" -ForegroundColor Yellow
Write-Host "git add src/Legacy/GTSErpSystemOriginal"
Write-Host 'git commit -m "Import original GTS ERP source for real screen integration"'
Write-Host "git push origin main"
 } |
            ForEach-Object {
                try {
                    Get-ChildItem -LiteralPath $_.Root -Filter "GTSErpSystem.csproj" -Recurse -File -ErrorAction SilentlyContinue |
                        ForEach-Object { Split-Path -Parent $_.FullName }
                } catch {}
            }
        foreach ($item in $loose) {
            if (Test-Path (Join-Path $item "GTSErpSystem.csproj")) {
                $candidates.Add((Resolve-Path $item).Path)
            }
        }
    }

    if ($candidates.Count -eq 0) {
        throw "لم يتم العثور تلقائيًا على مصدر GTSErpSystem.csproj في أقراص الجهاز."
    }

    $Source = $candidates | Select-Object -Unique | Select-Object -First 1
}

$resolvedSource = (Resolve-Path $Source).Path
$target = Join-Path $resolvedRoot $Destination

if (-not (Test-Path $resolvedSource -PathType Container)) {
    throw "لم يتم العثور على المصدر: $resolvedSource"
}

New-Item -ItemType Directory -Force -Path $target | Out-Null

Write-Host "نسخ المصدر الأصلي..." -ForegroundColor Cyan
Write-Host "من: $resolvedSource"
Write-Host "إلى: $target"

# No deletion is performed. Existing Saqer files are not touched.
Get-ChildItem -LiteralPath $resolvedSource -Recurse -File |
    Where-Object {
        $_.Extension -in ".cs",".resx",".config",".xml",".json",".txt"
    } |
    ForEach-Object {
        $relative = $_.FullName.Substring($resolvedSource.Length).TrimStart('\')
        $destinationFile = Join-Path $target $relative
        $destinationDir = Split-Path -Parent $destinationFile
        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationFile -Force
    }

$count = (Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object).Count
Write-Host "تم استيراد $count ملفًا." -ForegroundColor Green
Write-Host "لم يتم حذف أي ملف من نظام صقر الحالي." -ForegroundColor Green

Write-Host ""
Write-Host "بعد مراجعة الملفات يمكنك تنفيذ:" -ForegroundColor Yellow
Write-Host "git add src/Legacy/GTSErpSystemOriginal"
Write-Host 'git commit -m "Import original GTS ERP source for real screen integration"'
Write-Host "git push origin main"
