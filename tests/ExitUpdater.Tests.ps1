# Exercise the real detached Windows worker against a disposable installation.
. "$PSScriptRoot\..\scripts\Common.ps1"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('welcome-exit-tests-' + [guid]::NewGuid().ToString('N'))
$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$sleeper = $null
$worker = $null
function Assert($Condition, [string] $Message) { if (!$Condition) { throw $Message } }
function Write-Fixture([string] $Path, [string] $Version) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    $json = @{ UniqueID = 'JunxuanB.Welcome'; Version = $Version; EntryDll = 'Welcome.dll' } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $Path 'manifest.json'), $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Path 'Welcome.dll'), "fixture-$Version")
}
function New-Job([string] $Version, [string] $Hash) {
    $jobDir = Join-Path $testRoot ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $jobDir | Out-Null
    Copy-Item -LiteralPath "$PSScriptRoot\..\scripts\Common.ps1","$PSScriptRoot\..\scripts\Apply-AfterExit.ps1" -Destination $jobDir
    $jobPath = Join-Path $jobDir 'job.json'
    @{ ProcessId = $sleeper.Id; ProcessStartUtcTicks = $startTicks; TargetDirectory = $target;
       PackagePath = $zip; ExpectedVersion = $Version; ExpectedHash = $Hash } |
        ConvertTo-Json | Set-Content -LiteralPath $jobPath -Encoding UTF8
    return $jobPath
}
function Start-Worker([string] $Job) {
    return Start-Process -FilePath $windowsPowerShell -ArgumentList @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File',
        ('"' + (Join-Path (Split-Path $Job -Parent) 'Apply-AfterExit.ps1') + '"'), '-JobPath', ('"' + $Job + '"')) -WindowStyle Hidden -PassThru
}
function Wait-Worker($Process) {
    Assert ($Process.WaitForExit(30000)) 'Worker did not finish after game exit.'
    $Process.Refresh()
}
try {
    # A renamed mod folder verifies that the in-game updater uses its actual install directory.
    $target = Join-Path $testRoot 'Custom Mods\My Welcome'
    Write-Fixture $target '0.1.0'
    Set-Content -LiteralPath (Join-Path $target 'config.json') -Value '{"keep":true}'
    $incoming = Join-Path $testRoot 'package\Welcome'
    Write-Fixture $incoming '0.2.0'
    $zip = Join-Path $testRoot 'update.zip'
    Compress-Archive -LiteralPath $incoming -DestinationPath $zip
    $hash = (Get-FileHash -LiteralPath $zip).Hash
    $releaseFile = Join-Path $testRoot 'allow-exit'
    $waitScript = Join-Path $testRoot 'wait.ps1'
    'param([string] $ReleaseFile); while (!(Test-Path -LiteralPath $ReleaseFile)) { Start-Sleep -Milliseconds 100 }' |
        Set-Content -LiteralPath $waitScript -Encoding UTF8
    $sleeper = Start-Process -FilePath $windowsPowerShell -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
        ('"' + $waitScript + '"'), '-ReleaseFile', ('"' + $releaseFile + '"')) -WindowStyle Hidden -PassThru
    $startTicks = $sleeper.StartTime.ToUniversalTime().Ticks.ToString()
    $job = New-Job '0.2.0' $hash
    $worker = Start-Worker $job
    $ready = Join-Path (Split-Path $job -Parent) 'ready'
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (!(Test-Path -LiteralPath $ready) -and !$worker.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Assert (Test-Path -LiteralPath $ready) 'Worker failed before waiting for game exit.'
    Assert (!$worker.HasExited) 'Worker exited while game was still running.'
    Assert ((Get-Content -LiteralPath (Join-Path $target 'Welcome.dll') -Raw) -eq 'fixture-0.1.0') 'Worker changed a running game installation.'
    Set-Content -LiteralPath $releaseFile -Value 'exit'
    Wait-Worker $worker
    Assert ($worker.ExitCode -eq 0) "Worker failed: $(Get-Content -LiteralPath (Join-Path (Split-Path $job -Parent) 'update.log') -Raw)"
    Assert ((Get-Content -LiteralPath (Join-Path $target 'Welcome.dll') -Raw) -eq 'fixture-0.2.0') 'Worker did not install after exit.'
    Assert ((Get-Content -LiteralPath (Join-Path $target 'config.json') -Raw).Trim() -eq '{"keep":true}') 'Worker lost config.'

    $job = New-Job '0.1.0' $hash
    $worker = Start-Worker $job
    Wait-Worker $worker
    Assert ($worker.ExitCode -eq 0) 'Downgrade should be skipped without an error.'
    Assert ((Get-Content -LiteralPath (Join-Path $target 'Welcome.dll') -Raw) -eq 'fixture-0.2.0') 'Worker downgraded the installation.'

    $job = New-Job '0.3.0' ('0' * 64)
    $worker = Start-Worker $job
    Wait-Worker $worker
    Assert ($worker.ExitCode -eq 1) 'Worker accepted a changed package.'
    Assert ((Get-Content -LiteralPath (Join-Path $target 'Welcome.dll') -Raw) -eq 'fixture-0.2.0') 'Failed update changed the installation.'
    Write-Host 'PASS: detached worker waits for exit, installs into a renamed folder, preserves config, rejects downgrade and tampering.'
} finally {
    foreach ($ownedProcess in @($worker, $sleeper)) {
        if ($ownedProcess -and !$ownedProcess.HasExited) { $ownedProcess.Kill(); $ownedProcess.WaitForExit() }
        if ($ownedProcess) { $ownedProcess.Dispose() }
    }
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
        [IO.Path]::GetFileName($resolved) -match '^welcome-exit-tests-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
