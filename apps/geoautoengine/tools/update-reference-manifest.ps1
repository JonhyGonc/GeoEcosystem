param()

$ErrorActionPreference = 'Stop'

# The reference collection lives outside the repository (never versioned). In the
# GeoEcosystem monorepo it can be in apps\, in the monorepo root or next to the
# clone, so search upward from the parent of the application folder.
$applicationRoot = Split-Path -Parent $PSScriptRoot
$referenceRoot = $null
$directory = Split-Path -Parent $applicationRoot
while ($directory) {
    $candidate = Join-Path $directory 'reference'
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $referenceRoot = $candidate
        break
    }
    $directory = Split-Path -Parent $directory
}
if (-not $referenceRoot) {
    throw "External folder 'reference' not found in any parent folder of $applicationRoot."
}

$manifestPath = Join-Path $referenceRoot 'SHA256SUMS.csv'
$dataDirectories = @('legacy_csv', 'legacy_outputs', 'legacy_dwgs', 'regression_data')

$rows = foreach ($directory in $dataDirectories) {
    $root = Join-Path $referenceRoot $directory
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName.Substring($referenceRoot.Length + 1).Replace('\', '/')
            Bytes = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
}

$rows | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding utf8
Write-Host "Reference manifest: $manifestPath ($($rows.Count) files)"
