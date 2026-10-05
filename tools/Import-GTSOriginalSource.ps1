param(
    [string]$Source = "G:\ماجد سوفت\MajedSoft 08-07-2026\App\GTSErpSystem_Source\GTSErpSystem",
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Destination = "src\Legacy\GTSErpSystemOriginal"
)

$ErrorActionPreference = "Stop"

$resolvedSource = (Resolve-Path $Source).Path
$resolvedRoot = (Resolve-Path $RepositoryRoot).Path
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
