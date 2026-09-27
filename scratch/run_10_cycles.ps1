<#
.SYNOPSIS
    StarPie SP-SOUND-001 10 轮无头音效稳定性回归验证脚本 (10-Cycle Stability Verification)

.DESCRIPTION
    按严格 10 轮循环执行无头音效自动化测试套件 (Sound Forensics Suite)。
    验证多线程并发、会话生命周期与 WAV 解析在高频迭代下的确定性与零退化。
    全程运行于 Headless Mock 模式 (--test-instance)，不占用物理扬声器，不修改真实配置。

.USAGE
    powershell -File scratch/run_10_cycles.ps1
    或在 pwsh 中直接执行: ./scratch/run_10_cycles.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "================================================================="
Write-Host "  StarPie SP-SOUND-001: 10-Cycle Stability Verification Gate"
Write-Host "================================================================="

Write-Host "Step 1: Building test project in Release mode..."
& dotnet build scratch/test_sound_forensics.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host "`nStep 2: Executing 10 consecutive test cycles..."
$results = @()
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 1; $i -le 10; $i++) {
    Write-Host "`n>>> [Cycle $i/10] Starting test execution..."
    $cycleSw = [System.Diagnostics.Stopwatch]::StartNew()

    & dotnet run --project scratch/test_sound_forensics.csproj -c Release --no-build -- --test-instance
    $cycleSw.Stop()

    $exitCode = $LASTEXITCODE
    $elapsedMs = $cycleSw.ElapsedMilliseconds
    $passed = ($exitCode -eq 0)

    $results += [PSCustomObject]@{
        Cycle       = $i
        Passed      = $passed
        ExitCode    = $exitCode
        DurationMs  = $elapsedMs
    }

    if (-not $passed) {
        Write-Error "Cycle $i FAILED with exit code $exitCode after ${elapsedMs}ms!"
        exit 1
    }
    Write-Host "[Cycle $i/10] PASSED in ${elapsedMs}ms."
}

$totalStopwatch.Stop()

Write-Host "`n================================================================="
Write-Host "  10-Cycle Stability Verification Summary"
Write-Host "================================================================="
$results | Format-Table -AutoSize -Property Cycle, Passed, ExitCode, DurationMs

$allPassed = ($results | Where-Object { -not $_.Passed }).Count -eq 0
$totalDuration = $totalStopwatch.Elapsed

if ($allPassed) {
    Write-Host "RESULT: 10/10 CYCLES PASSED (100% Pass Rate, Total Time: $($totalDuration.ToString('mm\:ss')))" -ForegroundColor Green
    exit 0
} else {
    Write-Error "RESULT: Verification FAILED"
    exit 1
}
