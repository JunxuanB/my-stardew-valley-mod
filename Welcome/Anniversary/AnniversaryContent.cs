using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Shops;

namespace Welcome.Anniversary;

internal sealed class AnniversaryContent(IModHelper helper, FestivalLayout layout, FestivalSchedule schedule)
{
    internal const string ModId = "JunxuanB.Welcome";
    internal const string FestivalKey = "spring2";
    internal const string Name = "纪念日";
    internal const string MapAsset = ModId + "_AnniversaryTown";
    internal const string Xiaowai = ModId + "_Xiaowai";
    internal const string PierreShop = ModId + "_AnniversaryPierre";
    internal const string Portraitless = ModId + "/PortraitlessFestivalGuest";
    internal readonly FestivalMapBuilder Builder = new(helper, layout);

    internal void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/" + Xiaowai))
            e.LoadFromModFile<Texture2D>("assets/xiaowai.png", AssetLoadPriority.Medium);
        else if (e.NameWithoutLocale.IsEquivalentTo("Portraits/" + Xiaowai))
            e.LoadFromModFile<Texture2D>("assets/xiaowai-portrait.png", AssetLoadPriority.Medium);
        else if (e.NameWithoutLocale.IsEquivalentTo("Maps/" + MapAsset))
            e.LoadFrom(Builder.Build, AssetLoadPriority.Low);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Festivals/FestivalDates"))
            e.Edit(asset =>
            {
                IDictionary<string, string> dates = asset.AsDictionary<string, string>().Data;
                schedule.EditDates(dates);
            }, AssetEditPriority.Late);
        else if (schedule.IsAsset(e.NameWithoutLocale.Name.Replace('\\','/')))
            e.LoadFrom(CreateFestival, AssetLoadPriority.Low);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            e.Edit(asset => asset.AsDictionary<string, ShopData>().Data[PierreShop] = new ShopData
            {
                ApplyProfitMargins = false,
                Owners = [new ShopOwnerData { Name = "Pierre", Portrait = "" }],
                Items = [Stock("745", 80, -1), Stock("460", 4000, 1), Stock("395", 240, 5), Stock("233", 200, 5)]
            });
    }

    private static ShopItemData Stock(string id, int price, int count) => new()
    {
        Id = "Anniversary_" + id, ItemId = "(O)" + id, Price = price,
        AvailableStock = count, AvailableStockLimit = LimitedStockMode.Player
    };

    private Dictionary<string, string> CreateFestival()
    {
        LayoutActor alien = layout.Actors.Single(a => a.Name == "Xiaowai");
        var data = new Dictionary<string, string>
        {
            ["name"] = Name,
            ["conditions"] = "Town/900 1400",
            ["locationDisplayName"] = "鹈鹕镇纪念日",
            ["set-up"] = string.Join('/', new[]
            {
                "wedding/-1000 -1000", $"farmer {layout.Entry[0]} {layout.Entry[1]} 1",
                "changeToTemporaryMap " + MapAsset, "loadActors Set-Up",
                $"addTemporaryActor {Xiaowai} 24 32 {alien.At[0]} {alien.At[1]} {alien.Facing} false 小外",
                "playerControl anniversary", "viewport player clamp true", "unfreeze", "globalFadeToClear"
            }),
            ["mainEvent"] = "pause 300/end",
            ["Set-Up_additionalCharacters"] = string.Join('/', layout.Actors.Where(a => a.Name != "Xiaowai"&&Game1.getCharacterFromName(a.Name) is not null)
                .Select(a => $"\"{a.Name}\" {a.At[0]} {a.At[1]} {a.Facing}")),
            [Xiaowai] = "我来地球有什么目的……先记录一下：人类会为一起度过的日子，再一起度过一天。",
            ["Lewis"] = "今天的史莱姆比赛由我主持。广场中间已经留好了位置。",
            ["Gus"] = "空桌子准备好了，蛋糕放在正中间。两个人一起切，记得慢一点。",
            ["Willy"] = "石沿、渔网，还有一池清水。我和罗宾忙了一早上，试试手气吧。",
            ["Caroline"] = "花是大家一起挑的。明天镇子就又恢复原样了。",
            ["Robin"] = "鱼池放在酒吧南边，既不挡门，也不挡长椅。",
            ["Evelyn"] = "有些日子，值得用鲜花记住。",
            ["Abigail"] = "带爱心的史莱姆会不会特别会躲？",
            ["Sam"] = "塞巴斯蒂安还说自己不喜欢热闹。我赌他一会儿就来。",
            ["Penny"] = "今天可以慢慢逛，不用着急。",
            ["Haley"] = "小外，说茄子！……不是让你去找一颗茄子。",
            ["Marnie"] = "这颗草莓真大。我特意给它留了一小块地。"
        };
        foreach(LayoutActor actor in layout.Actors)
            if(!string.IsNullOrEmpty(actor.Dialogue))data[actor.Name]=actor.Dialogue;
        return data;
    }

    internal void EnsureGuests(IMonitor monitor)
    {
        Event? festival=Game1.CurrentEvent;if(festival is null)return;
        foreach(LayoutActor placement in layout.Actors.Where(a=>a.Name!="Xiaowai"))
        {
            string texture=NPC.getTextureNameForCharacter(placement.Name);
            bool hasPortrait=helper.GameContent.DoesAssetExist<Texture2D>(helper.GameContent.ParseAssetName("Portraits/"+texture));
            // Some native festival loaders skip Kent/Leo before their story unlock.
            // These are event-only visitors; no schedules or story flags are changed.
            NPC? guest=festival.getActorByName(placement.Name);
            if(guest is null)
            {
                NPC.TryGetData(placement.Name,out var character);
                Point size=character?.Size??new Point(16,32);
                Texture2D? portrait=hasPortrait?helper.GameContent.Load<Texture2D>("Portraits/"+texture):null;
                guest=new NPC(new AnimatedSprite("Characters/"+texture,0,size.X,size.Y),
                    new Vector2(placement.At[0],placement.At[1])*64,Game1.currentLocation.Name,placement.Facing,
                    placement.Name,portrait,eventActor:true);
                festival.actors.Add(guest);
            }
            guest.Position=new Vector2(placement.At[0],placement.At[1])*64;
            guest.faceDirection(placement.Facing);guest.EventActor=true;
            if(!hasPortrait)
            {
                // A null portrait alone makes NPC.Portrait call ChooseAppearance again.
                // Respect the native explicit-override flag on this event actor only.
                guest.portraitOverridden=true;guest.Portrait=null;
                guest.modData[Portraitless]="true";
            }
            if(guest.Name is "Pierre" or "Emily")guest.CurrentDialogue.Clear();
            else if(festival.TryGetFestivalDialogueForYear(guest,guest.Name,out Dialogue dialogue))guest.setNewDialogue(dialogue);
        }
        monitor.Log($"纪念日来宾已就位：{layout.Actors.Count-1} 位原版角色和小外。",LogLevel.Info);
    }
}
