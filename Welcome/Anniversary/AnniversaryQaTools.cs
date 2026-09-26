using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Welcome.Anniversary;

/// <summary>Explicit, player-operated tools for the dedicated QA farm only.</summary>
internal sealed class AnniversaryQaTools
{
    private const string SpawnMessage="QA/SpawnGiantCrop";
    private const string ResultMessage="QA/SpawnResult";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly Action<string> show;
    private readonly Dictionary<string,string> spawnResults=[];
    internal static bool Allowed=>Context.IsWorldReady
        &&Game1.MasterPlayer.farmName.Value.StartsWith("AnniversaryQA",StringComparison.Ordinal);

    internal AnniversaryQaTools(IModHelper helper,IMonitor monitor,Action<string> show)
    {
        this.helper=helper;this.monitor=monitor;this.show=show;
        helper.Events.Multiplayer.ModMessageReceived+=Message;
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>spawnResults.Clear();
    }

    internal void SpawnCrop()
    {
        if(!Ready())return;
        if(Game1.currentLocation is not Farm)
        {show("请回到农场，在面前留出 3×3 的空地后再生成巨大作物。");return;}
        var request=new QaCropRequest();
        if(Context.IsMainPlayer)SpawnFor(Game1.player.UniqueMultiplayerID,request);
        else
        {
            helper.Multiplayer.SendMessage(request,SpawnMessage,[AnniversaryContent.ModId],[Game1.MasterPlayer.UniqueMultiplayerID]);
            Game1.addHUDMessage(new HUDMessage("已请求房主在你面前生成巨大作物。",HUDMessage.newQuest_type));
        }
    }

    internal void Rescue(string kind)
    {
        if(!Ready())return;
        if(Game1.timeOfDay>=2600)
        {show("已经到凌晨 2 点，请等下一天再进行救援测试。");return;}
        if(kind=="passout")
        {
            if(!Game1.currentLocation.IsOutdoors||Game1.currentLocation.HasMapPropertyWithValue("PassOutSafe"))
            {show("请到普通户外区域测试昏倒，例如农场。室内或安全区域不会触发月亮卡的收费保护。");return;}
            if(Game1.player.Money<10)
            {show("金币不足 10，原版昏倒费用会是 0，月亮卡不会消耗。请留至少 10 金再测试。");return;}
            monitor.Log($"QA: player {Game1.player.UniqueMultiplayerID} requested native pass-out; moon cards={Count(RewardCatalog.Moon)}.",LogLevel.Info);
            // Let Game1 detect its real exhaustion threshold, start the animation,
            // apply its input lock, and send the ordinary multiplayer pass-out request.
            Game1.player.Stamina=-16f;
        }
        else if(kind=="defeat")
        {
            monitor.Log($"QA: player {Game1.player.UniqueMultiplayerID} requested native defeat; mars cards={Count(RewardCatalog.Mars)}.",LogLevel.Info);
            // Native Farmer.Update detects zero health and performs the real rescue event.
            Game1.player.health=0;
        }
    }

    internal static int Count(string id)=>Game1.player.Items.Where(i=>i?.QualifiedItemId==id).Sum(i=>i.Stack);

    private bool Ready()
    {
        if(!Allowed){monitor.Log("QA 工具只允许在 AnniversaryQA 测试农场使用。",LogLevel.Warn);return false;}
        if(Game1.currentLocation?.Map is null||Game1.fadeToBlack||Game1.locationRequest is not null)return false;
        if(Game1.eventUp||Game1.CurrentEvent is not null)
        {show("请先结束节日或剧情，再使用巨大作物和救援测试工具。");return false;}
        if(Game1.activeClickableMenu is not null||Game1.currentMinigame is not null||Game1.dialogueUp||!Game1.player.CanMove||Game1.player.UsingTool
            ||Game1.player.isEating||Game1.player.passedOut||Game1.killScreen||Game1.player.health<=0)
        {monitor.Log("请等当前动作、菜单或救援结束后再使用 QA 工具。",LogLevel.Info);return false;}
        return true;
    }

    private void Message(object? sender,ModMessageReceivedEventArgs e)
    {
        if(e.FromModID!=AnniversaryContent.ModId||!Allowed)return;
        if(e.Type==SpawnMessage&&Context.IsMainPlayer)SpawnFor(e.FromPlayerID,e.ReadAs<QaCropRequest>());
        else if(e.Type==ResultMessage&&e.FromPlayerID==Game1.MasterPlayer.UniqueMultiplayerID)
            show(e.ReadAs<string>());
    }

    private void SpawnFor(long id,QaCropRequest request)
    {
        if(!Allowed||!Context.IsMainPlayer)return;
        Farmer? who=Game1.getOnlineFarmers().FirstOrDefault(p=>p.UniqueMultiplayerID==id);
        if(who is null)return;
        string key=id+":"+request.Id;
        if(!spawnResults.TryGetValue(key,out string? result))
        {
            if(who.currentLocation is not Farm farm||who.currentLocation.currentEvent is not null
                ||who.UsingTool||who.isEating||who.passedOut||who.health<=0)
                result="请在农场空地上，结束当前动作后再生成巨大作物。";
            else
            {
                var crop=new GiantCrop("Cauliflower",Vector2.Zero);
                Vector2 tile=InFront(who,crop.width.Value,crop.height.Value);
                if(!GiantCropMover.Valid(farm,crop,tile))
                    result="面前没有完整的 3×3 空地。请移到空旷位置或转个方向；没有覆盖任何东西。";
                else
                {
                    crop.Tile=tile;
                    farm.resourceClumps.Add(crop);
                    result="面前已生成一株原版巨大花椰菜，可以用巨大作物移动工具测试搬运。";
                    monitor.Log($"QA: spawned Cauliflower for player {id} at {farm.NameOrUniqueName} ({tile.X},{tile.Y}).",LogLevel.Info);
                }
            }
            spawnResults[key]=result;
        }
        if(id==Game1.player.UniqueMultiplayerID)show(result);
        else helper.Multiplayer.SendMessage(result,ResultMessage,[AnniversaryContent.ModId],[id]);
    }

    private static Vector2 InFront(Farmer who,int width,int height)
    {
        Rectangle bounds=who.GetBoundingBox();
        Point tile=who.TilePoint;
        return who.FacingDirection switch
        {
            0=>new Vector2(tile.X-width/2,(bounds.Top-1)/64-height+1),
            1=>new Vector2((bounds.Right+63)/64,tile.Y-height/2),
            2=>new Vector2(tile.X-width/2,(bounds.Bottom+63)/64),
            _=>new Vector2((bounds.Left-1)/64-width+1,tile.Y-height/2)
        };
    }
}

internal sealed class QaCropRequest
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
}
