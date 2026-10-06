param(
    [string]$Executable = (Join-Path $PSScriptRoot '..\CadTest\bin\Debug\net10.0\CadTest.exe')
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$outputDirectory = Join-Path $PSScriptRoot '..\CadTest\output'

# No CAD modes: exercise the real executable's early-return/invalid-input paths.
# Metadata detects accidental output creation/replacement; this is not a byte audit.
function Get-OutputSnapshot {
    @(Get-ChildItem -LiteralPath $outputDirectory -File -Recurse |
        Sort-Object FullName |
        ForEach-Object { '{0}|{1}|{2}' -f $_.FullName, $_.Length, $_.LastWriteTimeUtc.Ticks })
}
$before = Get-OutputSnapshot
foreach ($mode in @('', '--help', '-h')) {
    $lines = if ($mode -eq '') { & $Executable } else { & $Executable $mode }
    if ($LASTEXITCODE -ne 0 -or ($lines -join "`n") -notmatch 'no CAD action selected') {
        throw "Safe launch failed: '$mode'"
    }
    Write-Output "[PASS] Safe launch '$mode'."
}
# Redirect native stderr without treating the expected rejection as a PS error.
$previousPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $wrongSource = Join-Path $PSScriptRoot '..\TODO.md'
    foreach ($m12Args in @(@('--inspect-m12-end-profiles'), @('--inspect-m12-end-profiles', $wrongSource))) {
        $rejection = & $Executable @m12Args 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException|Pinned working M6 integration') {
            throw 'Invalid M12 end inspection input was not rejected before CAD.'
        }
    }
    foreach ($endArgs in @(@('--inspect-m8-inner-ends'), @('--inspect-m8-inner-ends', $wrongSource), @('--inspect-m8-blend-interstitial'), @('--inspect-m8-blend-interstitial', $wrongSource))) {
        $rejection = & $Executable @endArgs 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException|Pinned working M6 integration') {
            throw 'Invalid M8 end inspection input was not rejected before CAD.'
        }
    }
    foreach ($integrationArgs in @(@('--verify-m6-integration'), @('--verify-m6-integration', $wrongSource))) {
        $rejection = & $Executable @integrationArgs 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException|Pinned overlap repair') {
            throw 'Invalid integration input was not rejected before CAD.'
        }
    }
    $rejection = & $Executable '--verify-m6-repair-final' $wrongSource 2>&1
    if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'Pinned overlap repair') {
        throw 'Unpinned final repair input was not rejected before CAD.'
    }
    $rejection = & $Executable '--verify-m6-repair-final' 2>&1
    if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException') {
        throw 'Incomplete final repair mode was not rejected before CAD.'
    }
    $rejection = & $Executable '--verify-precision-m6-repair' 2>&1
    if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException') {
        throw 'Incomplete combined repair verification was not rejected before CAD.'
    }
    foreach ($repairMode in @('--repair-precision-m6-overlap', '--repair-precision-m6-alignment', '--clear-precision-m6-pilots')) {
        $rejection = & $Executable $repairMode $wrongSource 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'Pinned precision pattern') {
            throw "Unpinned repair source was not rejected before CAD: $repairMode"
        }
        $rejection = & $Executable $repairMode 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException') {
            throw "Incomplete repair mode was not rejected before CAD: $repairMode"
        }
        Write-Output "[PASS] Repair input guards: $repairMode."
    }
    foreach ($pinnedMode in @('--complete-precision-m6-pattern', '--verify-completed-endings-model')) {
        $rejection = & $Executable $pinnedMode $wrongSource 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'Pinned completed') {
            throw "Unpinned model was not rejected before CAD: $pinnedMode"
        }
        Write-Output "[PASS] Wrong source rejected before CAD: $pinnedMode."
    }
    foreach ($invalidMode in @('--inspect-precision-m6-witnesses', '--verify-precision-m6-profiles', '--verify-precision-m6-void', '--repair-precision-m6-alignment', '--clear-precision-m6-pilots', '--verify-precision-m6-boundaries', '--verify-precision-m6-pattern', '--complete-precision-m6-pattern', '--verify-completed-endings-model', '--complete-m12-entries', '--repair-m12-start-trim', '--trial-precision-m6', '--align-precision-m6', '--verify-final-thread-sections', '--verify-final-thread-endings', '--verify-precision-m6', '--compare-m6-start-extension')) {
        $rejection = & $Executable $invalidMode 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException') {
            throw "Incomplete thread-finishing mode was not rejected before CAD: $invalidMode"
        }
        Write-Output "[PASS] Incomplete thread-finishing mode rejected: $invalidMode."
    }
    foreach ($invalidMode in @('--extend-variant-m6-starts', '--trial-kg-helical', '--build-agreed-variant', '--export-agreed-step', '--audit-variant-m6-mass', '--audit-variant-thread-edges', '--audit-variant-m90-short', '--audit-variant-flanks', '--prepare-agreed-sldprt', '--verify-kg-helical', '--trial-m8-mouth-rollback', '--trial-m8-mouth-no-trim', '--verify-m8-mouths', '--diagnose-feature-errors', '--compare-m8-blend-trim', '--inspect-m8-helical-extents')) {
        $rejection = & $Executable $invalidMode 2>&1
        if ($LASTEXITCODE -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException') {
            throw "Invalid invocation was not rejected before CAD work: $invalidMode"
        }
        Write-Output "[PASS] Invalid invocation rejected: $invalidMode."
    }
    foreach ($invalidMode in @('--not-a-valid-mode', '--build-tiff-pp-chamfer', '--diagnose-tiff-pp', '--trial-tiff-l-exit', '--complete-trial-l-profile', '--trial-m6-points', '--trial-channel-points', '--trial-feed-points', '--trial-radial-point', '--audit-current-m8-envelope', '--trial-m8-transitions', '--verify-m8-definitions', '--trial-m8-mouth-r05', '--trial-m8-mouth-before-thread', '--verify-other-geometry', '--correct-left-slot-width', '--verify-left-end', '--verify-left-shoulders', '--audit-left-mass', '--build-kg-entries', '--verify-kg-entries')) {
        $rejection = & $Executable $invalidMode 2>&1
        $rejectionCode = $LASTEXITCODE
        if ($rejectionCode -ne 1 -or ($rejection -join "`n") -notmatch 'ArgumentException') {
            throw "Invalid invocation was not rejected before CAD work: $invalidMode"
        }
        Write-Output "[PASS] Invalid invocation rejected: $invalidMode."
    }
} finally {
    $ErrorActionPreference = $previousPreference
}
$after = Get-OutputSnapshot
if (Compare-Object $before $after) { throw 'Output files changed during safe CLI checks.' }
Write-Output '[PASS] Unknown/incomplete options rejected; output file inventory/size/timestamps unchanged.'
