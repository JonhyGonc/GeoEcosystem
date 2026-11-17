param(
    # "auto" instala em todo Civil 3D 2026 e 2027 encontrado neste computador.
    [ValidateSet("auto", "2024", "2025", "2026", "2027")]
    [string]$Version = "auto",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if ($Version -eq "auto") {
    $versions = @("2026", "2027") | Where-Object {
        Test-Path -LiteralPath (Join-Path $env:ProgramFiles "Autodesk\AutoCAD $_\acad.exe") -PathType Leaf
    }
    if (-not $versions) {
        throw "Nenhum Civil 3D 2026 ou 2027 foi encontrado em $env:ProgramFiles\Autodesk."
    }
    Write-Host "Civil 3D encontrado: $($versions -join ' e ')" -ForegroundColor Cyan
}
else {
    $versions = @($Version)
}

# Compatibility entry point: each deploy is deliberately scoped to one host.
foreach ($item in $versions) {
    Write-Host ""
    Write-Host "=== Instalando o GeoAutoEngine no Civil 3D $item ===" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "deploy.ps1") `
        -Version $item `
        -Configuration $Configuration `
        -SkipBuild:$SkipBuild
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

exit 0
