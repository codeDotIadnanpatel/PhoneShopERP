<#
.SYNOPSIS
    Fails the build when a test result was not actually executed.

.DESCRIPTION
    A skipped, inconclusive or pending test must never be reported as a passing
    run (docs/12-engineering-readiness/TESTING_STRATEGY.md; Phase 00 P00-W04-T01
    acceptance criteria). This script inspects TRX files produced by
    `dotnet test` and fails if:
      * no TRX file exists, or
      * any result has outcome NotExecuted, Inconclusive or Pending.

.PARAMETER ResultsDirectory
    Directory passed to `dotnet test --results-directory`.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResultsDirectory
)

$ErrorActionPreference = 'Stop'

function Fail {
    param([Parameter(Mandatory = $true)][string] $Message)

    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Host "::error::$Message"
    }

    Write-Host $Message -ForegroundColor Red
    exit 1
}

$resolvedDirectory = Resolve-Path -LiteralPath $ResultsDirectory -ErrorAction SilentlyContinue
if (-not $resolvedDirectory) {
    Fail "Test results directory '$ResultsDirectory' does not exist. Tests produced no evidence."
}

$trxFiles = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter *.trx -Recurse -File)
if ($trxFiles.Count -eq 0) {
    Fail "No .trx result files found under '$ResultsDirectory'. Refusing to report success without test evidence."
}

$notExecuted = @()
$executed = 0

foreach ($file in $trxFiles) {
    [xml] $trx = Get-Content -LiteralPath $file.FullName -Raw

    foreach ($result in $trx.TestRun.Results.UnitTestResult) {
        $executed++

        if ($result.outcome -in @('NotExecuted', 'Inconclusive', 'Pending')) {
            $notExecuted += [pscustomobject]@{
                Test    = $result.testName
                Outcome = $result.outcome
                File    = $file.Name
            }
        }
    }
}

if ($notExecuted.Count -gt 0) {
    Write-Host 'Tests that did not execute:'
    $notExecuted | Format-Table -AutoSize | Out-String | Write-Host
    Fail "$($notExecuted.Count) test(s) did not execute. Skipped or inconclusive tests must not be reported as passing."
}

Write-Host "Verified $executed executed test result(s) across $($trxFiles.Count) TRX file(s); none skipped."
