using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Welcome.Anniversary;

internal sealed class GiantCropMover
{
    private readonly IModHelper helper;
    private GiantCrop? selected;
    private string location="";
    private readonly HashSet<string> handled=[];
    internal GiantCropMover(IModHelper helper)
    {
        this.helper=helper;
        helper.Events.Input.ButtonPressed+=(_,e)=>
        {
            if(!Context.IsWorldReady)return;
            if(selected is not null&&e.Button is SButton.Escape or SButton.ControllerB){selected=null;helper.Input.Suppress(e.Button);return;}
            if(Game1.currentLocation is not Farm farm||Game1.player.ActiveItem?.QualifiedItemId!=RewardCatalog.Mover
                ||Game1.activeClickableMenu is not null||Game1.eventUp){selected=null;return;}
            if(!e.Button.IsActionButton())return;
            helper.Input.Suppress(e.Button);
            Vector2 target=e.Button==SButton.MouseRight?e.Cursor.Tile:Game1.player.GetGrabTile();
            if(Vector2.Distance(Game1.player.Tile,target)>5){Say("靠近目标后再操作。");return;}
            if(selected is null)
            {
                selected=farm.resourceClumps.OfType<GiantCrop>().FirstOrDefault(c=>c.getBoundingBox().Contains((target*64).ToPoint())&&Vanilla(c));
                location=farm.NameOrUniqueName;
                Say(selected is null?"请选择原版巨大作物。":"已选中。移动鼠标查看新位置，右键放下；Esc 取消。");
                return;
            }
            var request=new CropMoveRequest{Location=location,FromX=(int)selected.Tile.X,FromY=(int)selected.Tile.Y,ToX=(int)target.X,ToY=(int)target.Y};
            if(Context.IsMainPlayer)Move(Game1.player.UniqueMultiplayerID,request);
            else helper.Multiplayer.SendMessage(request,"Reward/MoveCrop",[AnniversaryContent.ModId],[Game1.MasterPlayer.UniqueMultiplayerID]);
            selected=null;
        };
        helper.Events.Display.RenderedWorld+=(_,e)=>
        {
            if(selected is null||Game1.currentLocation is not Farm farm)return;
            Vector2 target=helper.Input.GetCursorPosition().Tile;
            Draw(selected.Tile,selected,Color.CornflowerBlue*.35f);
            Draw(target,selected,Valid(farm,selected,target)?Color.LimeGreen*.45f:Color.Red*.45f);
            void Draw(Vector2 tile,GiantCrop crop,Color color)
            {Vector2 at=Game1.GlobalToLocal(tile*64);e.SpriteBatch.Draw(Game1.staminaRect,new Rectangle((int)at.X,(int)at.Y,crop.width.Value*64,crop.height.Value*64),color);}
        };
        helper.Events.Multiplayer.ModMessageReceived+=(_,e)=>
        {
            if(e.FromModID!=AnniversaryContent.ModId||!Context.IsWorldReady)return;
            if(e.Type=="Reward/MoveCrop"&&Context.IsMainPlayer)Move(e.FromPlayerID,e.ReadAs<CropMoveRequest>());
            if(e.Type=="Reward/MoveReply"&&e.FromPlayerID==Game1.MasterPlayer.UniqueMultiplayerID)Say(e.ReadAs<string>());
        };
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>{selected=null;handled.Clear();};
    }
    private static bool Vanilla(GiantCrop crop)=>crop.Id is "Cauliflower" or "Melon" or "Pumpkin" or "QiFruit" or "Powdermelon";
    private void Move(long playerId,CropMoveRequest r)
    {
        if(!handled.Add(playerId+":"+r.Id))return;
        Farmer? who=Game1.getOnlineFarmers().FirstOrDefault(p=>p.UniqueMultiplayerID==playerId);
        if(who?.currentLocation is not Farm farm||farm.NameOrUniqueName!=r.Location)return;
        Item? tool=who.ActiveItem;
        GiantCrop? crop=farm.resourceClumps.OfType<GiantCrop>().FirstOrDefault(c=>c.Tile==new Vector2(r.FromX,r.FromY)&&Vanilla(c));
        Vector2 target=new(r.ToX,r.ToY);
        string result;
        if(tool?.QualifiedItemId!=RewardCatalog.Mover||crop is null||Vector2.Distance(who.Tile,target)>5)result="作物或工具已经变化，移动取消。";
        else if(!Valid(farm,crop,target))result="这里放不下。作物和工具都没有改变。";
        else
        {
            crop.Tile=target;
            if(tool.Stack>1)tool.Stack--;else who.removeItemFromInventory(tool);
            result="巨大作物搬好了！";
        }
        if(playerId==Game1.player.UniqueMultiplayerID)Say(result);
        else helper.Multiplayer.SendMessage(result,"Reward/MoveReply",[AnniversaryContent.ModId],[playerId]);
    }
    internal static bool Valid(Farm farm,GiantCrop crop,Vector2 target)
    {
        Rectangle area=new((int)target.X*64,(int)target.Y*64,crop.width.Value*64,crop.height.Value*64);
        if(farm.resourceClumps.Any(c=>!ReferenceEquals(c,crop)&&c.getBoundingBox().Intersects(area))
            ||farm.largeTerrainFeatures.Any(f=>f.getBoundingBox().Intersects(area))
            ||farm.characters.Any(c=>c.GetBoundingBox().Intersects(area))
            ||Game1.getOnlineFarmers().Any(p=>p.currentLocation==farm&&p.GetBoundingBox().Intersects(area)))return false;
        for(int x=(int)target.X;x<target.X+crop.width.Value;x++)
        for(int y=(int)target.Y;y<target.Y+crop.height.Value;y++)
        {
            Vector2 tile=new(x,y);
            if(!farm.isTileOnMap(tile)||farm.isWaterTile(x,y)||!farm.isTilePassable(new xTile.Dimensions.Location(x,y),Game1.viewport)
                ||farm.objects.ContainsKey(tile)||farm.buildings.Any(b=>b.occupiesTile(tile))
                ||farm.terrainFeatures.TryGetValue(tile,out var feature)&&(feature is not HoeDirt dirt||dirt.crop is not null))return false;
        }
        return true;
    }
    private static void Say(string message)=>Game1.addHUDMessage(new HUDMessage(message,HUDMessage.newQuest_type));
}
internal sealed class CropMoveRequest
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Location {get;set;}="";
    public int FromX {get;set;}
    public int FromY {get;set;}
    public int ToX {get;set;}
    public int ToY {get;set;}
}
