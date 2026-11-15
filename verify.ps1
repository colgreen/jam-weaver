#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Inspect native exit codes explicitly, even if the caller enables native-error promotion.
$PSNativeCommandUseErrorActionPreference = $false

function Invoke-DotNet
{
    param([string[]] $Arguments)

    Write-Host "Running: dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }
}

Push-Location -LiteralPath $PSScriptRoot
try
{
    # Keep verification independent of persistent MSBuild/compiler servers and their process state.
    Invoke-DotNet -Arguments @('build', 'JamWeaver.sln', '--configuration', $Configuration,
        '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false')
    Invoke-DotNet -Arguments @('run', '--project', 'tests/JamWeaver.Core.Tests', '--configuration', $Configuration, '--no-build')
    Invoke-DotNet -Arguments @('run', '--project', 'tests/JamWeaver.Console.Tests', '--configuration', $Configuration, '--no-build')
    Write-Host 'Verification passed.'
}
finally
{
    Pop-Location
}
exit 0
