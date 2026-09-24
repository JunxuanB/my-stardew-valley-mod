param([Parameter(Mandatory = $true)][string] $JobPath)
. "$PSScriptRoot\Common.ps1"
$jobDirectory = Split-Path ([IO.Path]::GetFullPath($JobPath)) -Parent
$logPath = Join-Path $jobDirectory 'update.log'
$mutex = $null
$locked = $false
try {
    $job = Get-Content -LiteralPath $JobPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $null = Get-ModVersion $job.ExpectedVersion
    if ($job.ExpectedHash -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid expected checksum.' }
    $target = [IO.Path]::GetFullPath($job.TargetDirectory)
    $manifestPath = Join-Path $target 'manifest.json'
    $installed = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($installed.UniqueID -ne $script:ModId) { throw 'Installed mod identity mismatch.' }

    $game = Get-Process -Id $job.ProcessId -ErrorAction SilentlyContinue
    Set-Content -LiteralPath $logPath -Value 'Waiting for game exit.' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $jobDirectory 'ready') -Value 'ready' -Encoding ASCII
    if ($game -and $game.StartTime.ToUniversalTime().Ticks.ToString() -eq $job.ProcessStartUtcTicks) {
        # This worker is independent of SMAPI and remains alive after the game exits.
        $game.WaitForExit()
    }
    while (Get-Process -Name 'Stardew Valley','StardewModdingAPI' -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 2
    }

    $sha = [Security.Cryptography.SHA256]::Create()
    try { $key = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($target.ToLowerInvariant()))).Replace('-', '') }
    finally { $sha.Dispose() }
    $mutex = [Threading.Mutex]::new($false, "Local\JunxuanB.Welcome.Update.$key")
    try { $locked = $mutex.WaitOne(30000) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'Another updater is busy; keeping the current installation.' }

    $installed = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($installed.UniqueID -ne $script:ModId) { throw 'Installed mod identity changed.' }
    if ((Get-ModVersion $installed.Version) -ge (Get-ModVersion $job.ExpectedVersion)) {
        Set-Content -LiteralPath $logPath -Value 'Already up to date; skipped.' -Encoding UTF8
        exit 0
    }
    if ((Get-FileHash -LiteralPath $job.PackagePath -Algorithm SHA256).Hash -ne $job.ExpectedHash) {
        throw 'Downloaded package changed while waiting. Checksum verification failed.'
    }
    Install-ModPackage -PackagePath $job.PackagePath -ExpectedVersion $job.ExpectedVersion -TargetDirectory $target
    Set-Content -LiteralPath $logPath -Value "Installed $($job.ExpectedVersion) successfully." -Encoding UTF8
} catch {
    Set-Content -LiteralPath $logPath -Value "Update failed; current version retained. $($_.Exception.Message)" -Encoding UTF8
    exit 1
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    if ($mutex) { $mutex.Dispose() }
}
