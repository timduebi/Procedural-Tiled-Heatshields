# Builds dist/AblatorDecals-<major>.<minor>.<patch>.zip from GameData/AblatorDecals + LICENSE, README.md, CHANGELOG.md.
# Version comes from GameData/AblatorDecals/AblatorDecals.version. PowerShell 5.1 compatible.
#   -Build      dotnet build src/AblatorDecals -c Release first
#   -SelfCheck  dotnet run --project tests/SelfCheck -c Release first (must succeed)
param([switch]$Build, [switch]$SelfCheck)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$mod = Join-Path $root 'GameData\AblatorDecals'

function Invoke-Step([string]$what, [string[]]$dotnetArgs) {
    Write-Host "== $what"
    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" }
}
if ($Build) { Invoke-Step 'build' @('build', (Join-Path $root 'src\AblatorDecals'), '-c', 'Release') }
if ($SelfCheck) { Invoke-Step 'self-check' @('run', '--project', (Join-Path $root 'tests\SelfCheck'), '-c', 'Release') }

$v = (Get-Content -Raw -Encoding UTF8 (Join-Path $mod 'AblatorDecals.version') | ConvertFrom-Json).VERSION
$version = '{0}.{1}.{2}' -f $v.MAJOR, $v.MINOR, $v.PATCH
foreach ($f in 'Plugins\AblatorDecals.dll') { if (-not (Test-Path (Join-Path $mod $f))) { throw "missing $f" } }

$dist = Join-Path $root 'dist'
$zip = Join-Path $dist "AblatorDecals-$version.zip"
New-Item -ItemType Directory -Force $dist | Out-Null
if (Test-Path $zip) { Remove-Item -Force $zip }

# entry name (forward slashes: PS 5.1's ZipFile.CreateFromDirectory writes '\', which CKAN and non-Windows unzip reject) -> file
$files = [ordered]@{}
foreach ($f in 'LICENSE', 'README.md', 'CHANGELOG.md') { $files[$f] = Join-Path $root $f }
# GameData/AblatorDecals without test builds, debug symbols, docs or preview renders
$modFull = (Get-Item $mod).FullName
Get-ChildItem -Recurse -File $mod | ForEach-Object {
    $rel = $_.FullName.Substring($modFull.Length + 1).Replace('\', '/')
    $dirs = @($rel.Split('/') | Select-Object -SkipLast 1)
    if ($_.Extension -ne '.pdb' -and -not ($dirs | Where-Object { $_ -match '^(AblatorDecalsTest|docs|preview)$' })) {
        $files["GameData/AblatorDecals/$rel"] = $_.FullName
    }
}

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$z = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($e in $files.GetEnumerator()) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $e.Value, $e.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $z.Dispose() }
"saved $zip ($($files.Count) files)"
