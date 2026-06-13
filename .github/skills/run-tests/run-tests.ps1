#requires -Version 5.1
<#
.SYNOPSIS
    Run specific automated test case(s) by name and produce a dated result folder
    with a pass/fail summary (implements the run-tests skill).

.DESCRIPTION
    * Maps a test-case name (e.g. "P0_TC001_LaunchVisualStudioTestBVT") to the xUnit method.
    * Runs `dotnet test` with a name filter, emitting a TRX log.
    * Parses the TRX and writes testRunResult_YYYY-MM-DD/summary.md with:
        - Overall Results (Total / Passed / Failed / Pass Rate)
        - Results by Priority
        - Per-test-case table (Status / Duration / Start / End / Exit Code)

.PARAMETER TestName
    One or more test-case names to run. Accepts the .txt-style name
    (P0_TC001_LaunchVisualStudioTestBVT) or the bare method name (LaunchVisualStudioTestBVT).

.PARAMETER SkipPowerShellTests
    Recorded in the summary header (no PowerShell-side tests run by this skill).

.EXAMPLE
    .\run-tests.ps1 -TestName P0_TC001_LaunchVisualStudioTestBVT
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]] $TestName,
    [switch] $SkipPowerShellTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path

function Get-MethodName([string] $name) {
    $tokens = $name -split '_'
    $rest = $tokens | Where-Object { $_ -notmatch '^[Pp]\d+$' -and $_ -notmatch '^TC\d+$' -and $_ -ne 'BVT' }
    $method = ($rest -join '')
    if ([string]::IsNullOrWhiteSpace($method)) { $method = $name }
    return $method
}

function Get-Priority([string] $name) {
    if ($name -match '^([Pp]\d+)') { return $matches[1].ToUpper() }
    return 'NA'
}

$dateStr  = Get-Date -Format 'yyyy-MM-dd'
$runId    = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$resultDir = Join-Path $repoRoot "testRunResult_$dateStr"
New-Item -ItemType Directory -Force -Path $resultDir | Out-Null

$cases = @()
foreach ($name in $TestName) {
    $method = Get-MethodName $name
    $trx = Join-Path $resultDir "$name.trx"

    Push-Location $repoRoot
    & dotnet test --no-restore --tl:off `
        --filter "FullyQualifiedName~$method" `
        --logger "trx;LogFileName=$trx" 2>&1 | Out-Null
    $exit = $LASTEXITCODE
    Pop-Location

    $status = 'NO RUN'; $durationSec = 0; $start = ''; $end = ''
    if (Test-Path $trx) {
        [xml]$doc = Get-Content $trx -Raw
        $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
        $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $res = $doc.SelectSingleNode('//t:UnitTestResult', $ns)
        if ($res) {
            $status = if ($res.outcome -eq 'Passed') { 'PASS' } else { 'FAIL' }
            if ($res.duration) { $durationSec = [int][TimeSpan]::Parse($res.duration).TotalSeconds }
            if ($res.startTime) { $start = ([datetime]$res.startTime).ToString('HH:mm:ss') }
            if ($res.endTime)   { $end   = ([datetime]$res.endTime).ToString('HH:mm:ss') }
        }
    }

    $cases += [pscustomobject]@{
        Name = $name; Priority = (Get-Priority $name); Status = $status
        Duration = "$($durationSec)s"; Start = $start; End = $end; Exit = $exit
    }
}

$total  = $cases.Count
$passed = ($cases | Where-Object { $_.Status -eq 'PASS' }).Count
$failed = $total - $passed
$rate   = if ($total -gt 0) { [math]::Round(($passed / $total) * 100) } else { 0 }

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Test Execution Summary - $dateStr")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Run ID: $runId  PowerShell Tests Skipped: $([bool]$SkipPowerShellTests)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Overall Results")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Metric | Value |")
[void]$sb.AppendLine("|--------|-------|")
[void]$sb.AppendLine("| Total Tests | $total |")
[void]$sb.AppendLine("| Passed | $passed |")
[void]$sb.AppendLine("| Failed | $failed |")
[void]$sb.AppendLine("| Pass Rate | $rate% |")
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Results by Priority")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Priority | Total | Passed | Failed | Pass Rate |")
[void]$sb.AppendLine("|----------|-------|--------|--------|-----------|")
foreach ($g in ($cases | Group-Object Priority | Sort-Object Name)) {
    $gp = ($g.Group | Where-Object { $_.Status -eq 'PASS' }).Count
    $gt = $g.Count; $gf = $gt - $gp
    $gr = if ($gt -gt 0) { [math]::Round(($gp / $gt) * 100) } else { 0 }
    [void]$sb.AppendLine("| $($g.Name) | $gt | $gp | $gf | $gr% |")
}
[void]$sb.AppendLine()

foreach ($g in ($cases | Group-Object Priority | Sort-Object Name)) {
    [void]$sb.AppendLine("## $($g.Name) Test Cases ($($g.Count) tests)")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| # | Test Case | Status | Duration | Start | End | Exit Code |")
    [void]$sb.AppendLine("|---|-----------|--------|----------|-------|-----|-----------|")
    $i = 0
    foreach ($c in $g.Group) {
        $i++
        [void]$sb.AppendLine("| $i | $($c.Name) | $($c.Status) | $($c.Duration) | $($c.Start) | $($c.End) | $($c.Exit) |")
    }
    [void]$sb.AppendLine()
}

$summaryPath = Join-Path $resultDir 'summary.md'
Set-Content -Path $summaryPath -Value $sb.ToString() -Encoding UTF8
Write-Host "Summary written to: $summaryPath"
Write-Host "Result: $passed/$total passed ($rate%)"
