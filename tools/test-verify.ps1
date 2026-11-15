#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$verificationScript = Join-Path $repositoryRoot 'verify.ps1'
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "jam-weaver-verify-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testDirectory | Out-Null

# A child process observes the script's real exit code without running the build or MIDI tests.
$worker = @'
param($VerificationScript, $ScenarioDirectory, [int] $FailureStep, [int] $FailureCode, $Configuration)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$dotnetState = @{ CallCount = 0 }
$fakeDotnet = {
    $dotnetState.CallCount++
    Add-Content -LiteralPath (Join-Path $ScenarioDirectory 'calls.txt') -Value ($args -join ' ')
    Add-Content -LiteralPath (Join-Path $ScenarioDirectory 'locations.txt') -Value (Get-Location).Path
    $global:LASTEXITCODE = if ($dotnetState.CallCount -eq $FailureStep) { $FailureCode } else { 0 }
}.GetNewClosure()
Set-Item -Path Function:dotnet -Value $fakeDotnet
$originalLocation = (Get-Location).Path
& $VerificationScript -Configuration $Configuration
$verificationExitCode = $LASTEXITCODE
if ((Get-Location).Path -ne $originalLocation) { throw 'Verification did not restore the working directory.' }
exit $verificationExitCode
'@

try
{
    $workerPath = Join-Path $testDirectory 'worker.ps1'
    Set-Content -LiteralPath $workerPath -Value $worker
    $shellPath = (Get-Process -Id $PID).Path
    $scenarios = @(
        @{ Step = 0; Code = 0; Configuration = 'Debug'; Calls = 3 },
        @{ Step = 0; Code = 0; Configuration = 'Release'; Calls = 3 },
        @{ Step = 1; Code = 17; Configuration = 'Release'; Calls = 1 },
        @{ Step = 2; Code = 23; Configuration = 'Release'; Calls = 2 },
        @{ Step = 3; Code = 31; Configuration = 'Release'; Calls = 3 }
    )
    for ($index = 0; $index -lt $scenarios.Count; $index++)
    {
        $scenario = $scenarios[$index]
        $scenarioDirectory = Join-Path $testDirectory $index
        New-Item -ItemType Directory -Path $scenarioDirectory | Out-Null
        Push-Location -LiteralPath $scenarioDirectory
        try
        {
            & $shellPath -NoProfile -File $workerPath $verificationScript $scenarioDirectory $scenario.Step $scenario.Code $scenario.Configuration
            $actualExitCode = $LASTEXITCODE
        }
        finally
        {
            Pop-Location
        }
        if ($actualExitCode -ne $scenario.Code)
        {
            throw "Scenario $index returned $actualExitCode instead of $($scenario.Code)."
        }
        $configuration = $scenario.Configuration
        $expectedCalls = @(
            "build JamWeaver.sln --configuration $configuration --disable-build-servers -m:1 -p:UseSharedCompilation=false",
            "run --project tests/JamWeaver.Core.Tests --configuration $configuration --no-build",
            "run --project tests/JamWeaver.Console.Tests --configuration $configuration --no-build"
        )
        $actualCalls = @(Get-Content -LiteralPath (Join-Path $scenarioDirectory 'calls.txt'))
        if (($actualCalls -join "`n") -cne ($expectedCalls[0..($scenario.Calls - 1)] -join "`n"))
        {
            throw "Scenario $index ran unexpected commands or continued after failure."
        }
        $locations = @(Get-Content -LiteralPath (Join-Path $scenarioDirectory 'locations.txt'))
        if (@($locations | Where-Object { $_ -ne $repositoryRoot }).Count -ne 0)
        {
            throw "Scenario $index ran outside the repository root."
        }
    }
    Write-Host 'Verification script checks passed (success, configurations, all failure stages, and working directories).'
}
finally
{
    # Delete only this run's unique directory directly beneath the temporary root.
    $resolvedDirectory = [System.IO.Path]::GetFullPath($testDirectory)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::GetDirectoryName($resolvedDirectory) -ne $temporaryRoot -or
        [System.IO.Path]::GetFileName($resolvedDirectory) -notmatch '^jam-weaver-verify-[0-9a-f]{32}$')
    {
        throw 'Refusing to remove an unexpected test directory.'
    }
    Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
}
# Expected failing child processes must not leak their exit code to a CI shell wrapper.
exit 0
