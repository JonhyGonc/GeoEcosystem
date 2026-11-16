param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("fast", "regression", "legacy", "cad", "full")]
    [string]$Suite,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$testProject = Join-Path $repositoryRoot "tests\GeoAutoEngine.Tests\GeoAutoEngine.Tests.csproj"
$solution = Join-Path $repositoryRoot "GeoAutoEngine.sln"

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Project-local .NET SDK not found: $dotnet"
}

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

function Invoke-Checked {
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Test command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-DotNetFast {
    Invoke-Checked {
        & $dotnet test $testProject `
            --configuration $Configuration `
            --no-restore `
            --filter "Category=FAST" `
            --nologo `
            --logger "console;verbosity=minimal"
    }
}

function Invoke-DotNetRegression {
    Invoke-Checked { & $dotnet build $solution --configuration $Configuration --no-restore --nologo }
    Invoke-Checked {
        & $dotnet test $testProject `
            --configuration $Configuration `
            --no-build `
            --nologo `
            --logger "console;verbosity=minimal"
    }
}

# The legacy system lives outside the repository (never versioned). It used to sit
# next to the clone; in the GeoEcosystem monorepo it can be in apps\, in the
# monorepo root or next to the clone, so search upward from the parent folder.
function Find-ExternalDirectory {
    param([string]$Name)
    $directory = Split-Path -Parent $repositoryRoot
    while ($directory) {
        $candidate = Join-Path $directory $Name
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return $candidate
        }
        $directory = Split-Path -Parent $directory
    }
    throw "External folder '$Name' not found in any parent folder of $repositoryRoot."
}

function Invoke-LegacySuite {
    param([string]$LegacySuite)
    $legacyRoot = Find-ExternalDirectory "GeoAutoEngine.Legacy"
    Invoke-Checked { & (Join-Path $legacyRoot "scripts\test_suites.ps1") -Suite $LegacySuite }
}

function Invoke-Legacy {
    Invoke-LegacySuite "legacy"
}

function Invoke-LegacyFull {
    Invoke-LegacySuite "full"
}

function Invoke-Cad {
    Invoke-Checked {
        & (Join-Path $repositoryRoot "scripts\run_stage4_cad_smoke.ps1") -Configuration Release
    }
}

Push-Location $repositoryRoot
try {
    switch ($Suite) {
        "fast" { Invoke-DotNetFast }
        "regression" { Invoke-DotNetRegression }
        "legacy" { Invoke-Legacy }
        "cad" { Invoke-Cad }
        "full" {
            $Configuration = "Release"
            Invoke-Checked { & $dotnet restore $solution --nologo }
            Invoke-DotNetRegression
            Invoke-LegacyFull
            Invoke-Cad
        }
    }
}
finally {
    Pop-Location
}
