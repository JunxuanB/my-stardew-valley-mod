using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace Welcome;

/// <summary>Shows one local welcome message for each entry into a world.</summary>
public sealed class ModEntry : Mod
{
    // Each split-screen player has their own pending message.
    private readonly PerScreen<bool> pendingWelcome = new();
    private string? updateNotice;

    public override void Entry(IModHelper helper)
    {
        Monitor.Log($"Welcome {ModManifest.Version} initialized.", LogLevel.Info);
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => pendingWelcome.Value = false;
        helper.Events.GameLoop.GameLaunched += (_, _) =>
        {
            var updater = new AutoUpdater(helper.DirectoryPath, ModManifest.Version.ToString(), Monitor,
                message => Interlocked.Exchange(ref updateNotice, message));
            _ = Task.Run(updater.RunAsync);
        };
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        // SaveLoaded also fires for farmhands joining a multiplayer game.
        pendingWelcome.Value = true;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (Context.IsPlayerFree && !Game1.fadeToBlack && Interlocked.Exchange(ref updateNotice, null) is { } notice)
            Game1.addHUDMessage(new HUDMessage(notice, HUDMessage.newQuest_type));

        // Wait until loading fades and introductory events have finished.
        if (!pendingWelcome.Value || !Context.IsPlayerFree || Game1.fadeToBlack)
            return;

        pendingWelcome.Value = false;
        Game1.addHUDMessage(new HUDMessage("欢迎！", HUDMessage.newQuest_type));
        Monitor.Log($"Welcome shown for player {Game1.player.UniqueMultiplayerID} on screen {Context.ScreenId}.", LogLevel.Info);
    }
}
