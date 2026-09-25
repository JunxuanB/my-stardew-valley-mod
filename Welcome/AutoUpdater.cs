using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using StardewModdingAPI;

namespace Welcome;

/// <summary>Checks once during SMAPI entry; an independent worker installs after the game exits.</summary>
internal sealed class AutoUpdater
{
    private const string Repository = "JunxuanB/my-stardew-valley-mod";
    private readonly string modDirectory;
    private readonly Version installedVersion;
    private readonly IMonitor monitor;

    public AutoUpdater(string modDirectory, string installedVersion, IMonitor monitor)
    {
        this.modDirectory = Path.GetFullPath(modDirectory);
        this.installedVersion = Version.Parse(installedVersion);
        this.monitor = monitor;
    }

    /// <returns>True only when a verified update has a ready installation worker.</returns>
    public async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            monitor.Log("Automatic installation currently supports Windows only.", LogLevel.Info);
            return false;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JunxuanB-Welcome-AutoUpdater/1.0");
        try
        {
            return await StageUpdateAsync(client);
        }
        catch (Exception ex)
        {
            monitor.Log($"Update check/download failed; continuing with the current version. {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private async Task<bool> StageUpdateAsync(HttpClient client)
    {
        using var checkTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var release = JsonDocument.Parse(await client.GetStringAsync($"https://api.github.com/repos/{Repository}/releases/latest", checkTimeout.Token));
        JsonElement root = release.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            return false;

        string version = root.GetProperty("tag_name").GetString()!.TrimStart('v');
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") || Version.Parse(version) <= installedVersion)
            return false;

        monitor.Log($"发现新版 {version}，正在下载并校验；准备完成后将结束本次启动，请稍候。", LogLevel.Warn);
        // Bound streaming reads too: HttpClient.Timeout alone only covers headers with ResponseHeadersRead.
        using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        CancellationToken downloadToken = downloadTimeout.Token;

        string zipName = $"Welcome-{version}.zip";
        string zipUrl = FindAsset(root, zipName);
        string hashUrl = FindAsset(root, zipName + ".sha256");
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JunxuanB.Welcome", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        string zipPath = Path.Combine(cache, zipName);

        // A failed download never touches the installed mod.
        using (var response = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, downloadToken))
        {
            response.EnsureSuccessStatusCode();
            using var input = await response.Content.ReadAsStreamAsync(downloadToken);
            using var output = File.Create(zipPath);
            byte[] buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(), downloadToken)) != 0)
            {
                total += count;
                if (total > 64 * 1024 * 1024)
                    throw new InvalidDataException("Update package exceeds 64 MB.");
                await output.WriteAsync(buffer.AsMemory(0, count), downloadToken);
            }
        }

        string expectedHash = (await client.GetStringAsync(hashUrl, downloadToken)).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        using (var sha = SHA256.Create())
        using (var file = File.OpenRead(zipPath))
        {
            if (!Regex.IsMatch(expectedHash, "^[a-fA-F0-9]{64}$") ||
                !Convert.ToHexString(sha.ComputeHash(file)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update checksum mismatch.");
        }

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var manifestEntry = archive.GetEntry("Welcome/manifest.json") ?? throw new InvalidDataException("Missing update manifest.");
            using var manifestStream = manifestEntry.Open();
            using var manifest = await JsonDocument.ParseAsync(manifestStream);
            if (manifest.RootElement.GetProperty("UniqueID").GetString() != "JunxuanB.Welcome" ||
                manifest.RootElement.GetProperty("Version").GetString() != version)
                throw new InvalidDataException("Update identity/version mismatch.");
        }

        // Copy our currently installed worker outside Mods: it must survive replacement of the mod folder.
        foreach (string name in new[] { "Common.ps1", "Apply-AfterExit.ps1" })
            File.Copy(Path.Combine(modDirectory, "update", name), Path.Combine(cache, name));

        using var game = Process.GetCurrentProcess();
        string jobPath = Path.Combine(cache, "job.json");
        await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(new
        {
            ProcessId = game.Id,
            ProcessStartUtcTicks = game.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TargetDirectory = modDirectory,
            PackagePath = zipPath,
            ExpectedVersion = version,
            ExpectedHash = expectedHash
        }));

        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = cache
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
                     Path.Combine(cache, "Apply-AfterExit.ps1"), "-JobPath", jobPath })
            start.ArgumentList.Add(argument);

        using var worker = Process.Start(start) ?? throw new IOException("Could not start the update worker.");
        // Only announce success after the worker has acknowledged the job.
        string ready = Path.Combine(cache, "ready");
        for (int attempt = 0; attempt < 100 && !File.Exists(ready); attempt++)
        {
            if (worker.HasExited)
                throw new IOException($"Update worker exited early. See {cache}");
            await Task.Delay(100);
        }
        if (!File.Exists(ready))
            throw new IOException($"Update worker did not become ready. See {cache}");

        monitor.Log($"Update {installedVersion} -> {version} downloaded and verified. It will install after game exit. Status: {cache}", LogLevel.Info);
        return true;
    }

    private static string FindAsset(JsonElement release, string name)
    {
        var asset = release.GetProperty("assets").EnumerateArray().Single(item => item.GetProperty("name").GetString() == name);
        string url = asset.GetProperty("browser_download_url").GetString()!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" ||
            !uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected release download URL.");
        return url;
    }
}
