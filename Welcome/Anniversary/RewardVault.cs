using System.Collections;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Internal;
using StardewValley.Objects;

namespace Welcome.Anniversary;

/// <summary>Custom items live only in mod save data while the vanilla save is serialized.</summary>
internal sealed class RewardVault
{
    private const string Tag=AnniversaryContent.ModId+"/Container";
    private const string SaveKey="anniversary-rewards-v1";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly List<(RewardRecord Record,Item Item)> removed=[];
    private static RewardVault? instance;
    internal RewardVault(IModHelper helper,IMonitor monitor,Harmony harmony)
    {
        this.helper=helper;this.monitor=monitor;instance=this;
        helper.Events.GameLoop.Saving+=(_,_)=>Detach();
        helper.Events.GameLoop.Saved+=(_,_)=>RestoreMemory();
        helper.Events.GameLoop.SaveLoaded+=(_,_)=>
        {
            removed.Clear();
            if(!Context.IsMainPlayer)return;
            foreach(var record in (helper.Data.ReadSaveData<List<RewardRecord>>(SaveKey)??[]).OrderBy(r=>r.Slot))
                Restore(record,Create(record));
        };
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>removed.Clear();
        harmony.Patch(AccessTools.Method(typeof(SaveGame),nameof(SaveGame.Save)),postfix:new HarmonyMethod(typeof(RewardVault),nameof(WrapSave)));
    }
    private static void WrapSave(ref IEnumerator<int> __result)=>__result=Guard(__result);
    private static IEnumerator<int> Guard(IEnumerator<int> original)
    {
        try{while(original.MoveNext())yield return original.Current;}
        finally{original.Dispose();instance?.RestoreMemory();}
    }
    private void Detach()
    {
        if(!Context.IsMainPlayer||removed.Count>0)return;
        // Collect addresses before any collection is compacted.
        List<(ForEachItemContext Context,RewardRecord Record)> pending=[];
        Utility.ForEachItemContext((in ForEachItemContext context)=>
        {
            if(RewardCatalog.Custom(context.Item))pending.Add((context,Address(context)));
            return true;
        });
        helper.Data.WriteSaveData(SaveKey,pending.Select(p=>p.Record).ToList());
        // A fresh traversal removes through the game's own field/list/container handlers.
        var byReference=new Dictionary<Item,RewardRecord>(ReferenceEqualityComparer.Instance);
        foreach(var entry in pending)byReference[entry.Context.Item]=entry.Record;
        try
        {
            Utility.ForEachItemContext((in ForEachItemContext context)=>
            {
                if(byReference.TryGetValue(context.Item,out RewardRecord? record))
                {removed.Add((record,context.Item));context.RemoveItem();}
                return true;
            });
        }
        catch{RestoreMemory();throw;}
    }
    private RewardRecord Address(ForEachItemContext context)
    {
        Item item=context.Item;IList<object> path=context.GetPath();
        Farmer? farmer=path.OfType<Farmer>().FirstOrDefault();
        GameLocation? location=path.OfType<GameLocation>().FirstOrDefault();
        var record=new RewardRecord{ItemId=item.QualifiedItemId,Stack=item.Stack,Quality=item.Quality,
            Farmer=farmer?.UniqueMultiplayerID??Game1.MasterPlayer.UniqueMultiplayerID,
            Location=location?.NameOrUniqueName??"",Data=item.modData.Pairs.ToDictionary(p=>p.Key,p=>p.Value)};
        if(item is Furniture furniture){record.X=(int)furniture.TileLocation.X;record.Y=(int)furniture.TileLocation.Y;record.Rotation=furniture.currentRotation.Value;}
        if(item is Boots boots){record.BootDefense=boots.defenseBonus.Value;record.BootImmunity=boots.immunityBonus.Value;record.BootPrice=boots.price.Value;record.BootStats=boots.appliedBootSheetIndex.Value;}
        Item? container=path.OfType<Item>().LastOrDefault();
        if(container is not null)
        {
            if(!container.modData.ContainsKey(Tag))container.modData[Tag]=Guid.NewGuid().ToString("N");
            record.Container=container.modData[Tag];record.Kind=container is Chest?"Chest":"Held";
            if(container is Chest chest)record.Slot=chest.Items.IndexOf(item);
            if(container is StorageFurniture storage){record.Kind="Dresser";record.Slot=storage.heldItems.IndexOf(item);}
            return record;
        }
        if(farmer is not null)
        {
            if(ReferenceEquals(farmer.hat.Value,item)){record.Kind="Hat";return record;}
            if(ReferenceEquals(farmer.shirtItem.Value,item)){record.Kind="Shirt";return record;}
            if(ReferenceEquals(farmer.boots.Value,item)){record.Kind="Boots";return record;}
            if(farmer.Items.Contains(item)){record.Kind="Inventory";record.Slot=farmer.Items.IndexOf(item);return record;}
            if(farmer.itemsLostLastDeath.Contains(item)){record.Kind="Lost";record.Slot=farmer.itemsLostLastDeath.IndexOf(item);return record;}
            if(ReferenceEquals(farmer.recoveredItem,item)){record.Kind="Recovered";return record;}
        }
        NPC? actor=path.OfType<NPC>().FirstOrDefault();
        if(actor is not null)
        {
            if(!actor.modData.ContainsKey(Tag))actor.modData[Tag]=Guid.NewGuid().ToString("N");
            record.Kind="ActorHat";record.Container=actor.modData[Tag];return record;
        }
        if(location is not null && item is Furniture f && location.furniture.Contains(f))
        {record.Kind="Furniture";return record;}
        foreach(var pair in Game1.player.team.globalInventories.Pairs)
            if(path.Any(p=>ReferenceEquals(p,pair.Value))){record.Kind="Global";record.Container=pair.Key;record.Slot=pair.Value.IndexOf(item);return record;}
        if(Game1.player.team.returnedDonations.Contains(item)){record.Kind="Returned";record.Slot=Game1.player.team.returnedDonations.IndexOf(item);return record;}
        // Unsupported mod containers are kept safely in the vanilla lost-and-found on restore.
        record.Kind="Returned";record.Slot=int.MaxValue;
        monitor.Log("奖励将从特殊容器转移到失物招领："+string.Join(" / ",context.GetDisplayPath()),LogLevel.Warn);
        return record;
    }
    private static Item Create(RewardRecord r)
    {
        Item item=r.ItemId.StartsWith("(F)",StringComparison.Ordinal)
            ? Furniture.GetFurnitureInstance(r.ItemId[3..],new Vector2(r.X,r.Y))
            : ItemRegistry.Create(r.ItemId,r.Stack,r.Quality);
        foreach(var p in r.Data)item.modData[p.Key]=p.Value;
        if(item is Boots boots&&r.BootDefense.HasValue)
        {boots.defenseBonus.Value=r.BootDefense.Value;boots.immunityBonus.Value=r.BootImmunity??0;boots.price.Value=r.BootPrice??0;boots.appliedBootSheetIndex.Value=r.BootStats;}
        if(item is Furniture f)
        {
            f.TileLocation=new Vector2(r.X,r.Y);
            for(int i=0;i<r.Rotation;i++)f.rotate();
            f.updateDrawPosition();
        }
        return item;
    }
    private void RestoreMemory()
    {
        if(removed.Count==0)return;
        var items=removed.OrderBy(p=>p.Record.Slot).ToArray();removed.Clear();
        foreach(var pair in items)Restore(pair.Record,pair.Item);
    }
    private void Restore(RewardRecord r,Item item)
    {
        Farmer farmer=Game1.GetPlayer(r.Farmer)??Game1.MasterPlayer;
        if(r.Kind=="Inventory"&&r.Slot>=0&&r.Slot<farmer.Items.Count&&farmer.Items[r.Slot] is null){farmer.Items[r.Slot]=item;return;}
        if(r.Kind=="Hat"&&farmer.hat.Value is null){farmer.hat.Value=(Hat)item;return;}
        if(r.Kind=="Shirt"&&farmer.shirtItem.Value is null){farmer.shirtItem.Value=(Clothing)item;item.onEquip(farmer);return;}
        if(r.Kind=="Boots"&&farmer.boots.Value is null){farmer.boots.Value=(Boots)item;item.onEquip(farmer);return;}
        if(r.Kind=="Recovered"&&farmer.recoveredItem is null){farmer.recoveredItem=item;return;}
        if(r.Kind=="Lost"){farmer.itemsLostLastDeath.Insert(Math.Min(r.Slot,farmer.itemsLostLastDeath.Count),item);return;}
        if(r.Kind=="Furniture"&&Game1.getLocationFromName(r.Location) is { } location&&item is Furniture furniture)
        {location.furniture.Add(furniture);return;}
        if(r.Kind=="Global")
        {
            if(!Game1.player.team.globalInventories.TryGetValue(r.Container,out var inventory))
                Game1.player.team.globalInventories[r.Container]=inventory=new StardewValley.Inventories.Inventory();
            inventory.Insert(Math.Min(r.Slot,inventory.Count),item);return;
        }
        if(r.Kind=="ActorHat")
        {
            bool done=false;
            Utility.ForEachLocation(location=>
            {
                foreach(NPC actor in location.characters.Where(n=>n.modData.TryGetValue(Tag,out string? tag)&&tag==r.Container))
                {
                    var field=AccessTools.Field(actor.GetType(),"hat")?.GetValue(actor);
                    var property=field?.GetType().GetProperty("Value");
                    if(property is not null&&property.GetValue(field) is null){property.SetValue(field,item);done=true;}
                }
                return !done;
            });
            if(done)return;
        }
        if(r.Kind is "Chest" or "Held" or "Dresser")
        {
            Item? container=null;
            Utility.ForEachItem(i=>{if(i.modData.TryGetValue(Tag,out string? id)&&id==r.Container){container=i;return false;}return true;});
            if(container is Chest chest){chest.Items.Insert(Math.Min(Math.Max(0,r.Slot),chest.Items.Count),item);return;}
            if(container is StorageFurniture storage){storage.heldItems.Insert(Math.Min(Math.Max(0,r.Slot),storage.heldItems.Count),item);return;}
            if(container is StardewValley.Object obj&&item is StardewValley.Object held&&obj.heldObject.Value is null){obj.heldObject.Value=held;return;}
        }
        Game1.player.team.returnedDonations.Add(item);
    }
}
internal sealed class RewardRecord
{
    public string ItemId {get;set;}="";
    public int Stack {get;set;}=1;
    public int Quality {get;set;}
    public string Kind {get;set;}="";
    public long Farmer {get;set;}
    public string Location {get;set;}="";
    public string Container {get;set;}="";
    public int Slot {get;set;}
    public int X {get;set;}
    public int Y {get;set;}
    public int Rotation {get;set;}
    public int? BootDefense {get;set;}
    public int? BootImmunity {get;set;}
    public int? BootPrice {get;set;}
    public string? BootStats {get;set;}
    public Dictionary<string,string> Data {get;set;}=[];
}
