// Compile the production ModEntry with a controlled updater and minimal game API doubles.
// A ready update must exit this child process before gameplay handlers can be registered.
using StardewModdingAPI;

Welcome.AutoUpdater.Ready = args.Single() == "ready";
var helper = new TestHelper();
new Welcome.ModEntry().Entry(helper);
if (helper.Events.GameLoop.Subscriptions != 3)
    throw new Exception("Normal startup failed to register gameplay handlers.");
Console.WriteLine("GAMEPLAY_ENABLED");

namespace Welcome
{
    internal sealed class AutoUpdater
    {
        public static bool Ready;
        public AutoUpdater(string directory, string version, IMonitor monitor) { }
        public Task<bool> RunAsync() => Task.FromResult(Ready);
    }
}

namespace StardewModdingAPI
{
    public enum LogLevel { Info, Error }
    public interface IMonitor { void Log(string message, LogLevel level); }
    public sealed class TestMonitor : IMonitor
    {
        public void Log(string message, LogLevel level) => Console.WriteLine($"{level}: {message}");
    }
    public sealed class TestManifest { public Version Version => new(0, 3, 0); }
    public abstract class Mod
    {
        public IMonitor Monitor { get; } = new TestMonitor();
        public TestManifest ModManifest { get; } = new();
        public abstract void Entry(IModHelper helper);
    }
    public interface IModHelper
    {
        string DirectoryPath { get; }
        TestEvents Events { get; }
    }
    public sealed class TestHelper : IModHelper
    {
        public string DirectoryPath => ".";
        public TestEvents Events { get; } = new();
    }
    public sealed class TestEvents { public TestGameLoop GameLoop { get; } = new(); }
    public sealed class TestGameLoop
    {
        public int Subscriptions { get; private set; }
        public event EventHandler<Events.SaveLoadedEventArgs> SaveLoaded
        {
            add { Subscriptions++; } remove { Subscriptions--; }
        }
        public event EventHandler<Events.UpdateTickedEventArgs> UpdateTicked
        {
            add { Subscriptions++; } remove { Subscriptions--; }
        }
        public event EventHandler<EventArgs> ReturnedToTitle
        {
            add { Subscriptions++; } remove { Subscriptions--; }
        }
    }
    public static class Context
    {
        public static bool IsPlayerFree => true;
        public static int ScreenId => 0;
    }
}
namespace StardewModdingAPI.Events
{
    public sealed class SaveLoadedEventArgs : EventArgs { }
    public sealed class UpdateTickedEventArgs : EventArgs { }
}
namespace StardewModdingAPI.Utilities
{
    public sealed class PerScreen<T> { public T Value { get; set; } = default!; }
}
namespace StardewValley
{
    public sealed class HUDMessage
    {
        public const int newQuest_type = 0;
        public HUDMessage(string message, int type) { }
    }
    public sealed class TestPlayer
    {
        public long UniqueMultiplayerID => 1;
        public string Name => "测试玩家";
    }
    public static class Game1
    {
        public static bool fadeToBlack => false;
        public static TestPlayer player { get; } = new();
        public static void addHUDMessage(HUDMessage message) { }
    }
}
