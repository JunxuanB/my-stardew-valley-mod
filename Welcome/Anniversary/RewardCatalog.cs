using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shirts;

namespace Welcome.Anniversary;

internal sealed record FestivalProduct(string Id,string Name,int Price,int Limit,bool Flowers);
internal static class RewardCatalog
{
    internal const string Prefix = AnniversaryContent.ModId + "_";
    internal const string Hat = "(H)"+Prefix+"Antennae";
    internal const string Glasses = "(H)"+Prefix+"ReadingGlasses";
    internal const string BrokenGlasses = "(O)170";
    internal const string Nest = "(F)"+Prefix+"Nest";
    internal const string Moon = "(O)"+Prefix+"MoonCard";
    internal const string Mars = "(O)"+Prefix+"MarsCard";
    internal const string Mover = "(O)"+Prefix+"CropMover";
    internal const string Head = "(H)"+Prefix+"XiaowaiHead";
    internal const string Shirt = "(S)"+Prefix+"XiaowaiSuit";
    internal const string Shoes = "(B)"+Prefix+"XiaowaiShoes";
    internal const string StickerHello="(F)"+Prefix+"StickerHello";
    internal const string StickerSleep="(F)"+Prefix+"StickerSleep";
    internal const string StickerSurvey="(F)"+Prefix+"StickerSurvey";
    internal const string EmilyShop = Prefix+"AnniversaryEmily";
    internal static readonly FestivalProduct[] Products =
    [new("(O)745","草莓种子",80,-1,false),new("(O)460","美人鱼吊坠",4000,1,false),
     new("(O)395","咖啡",240,5,false),new("(O)233","冰淇淋",200,5,false),new(Glasses,"普通眼镜",800,1,false),
     new(Hat,"小外的触角",5,1,true),new(Nest,"小外的小窝",8,1,true),
     new(Moon,"月亮医疗卡",3,20,true),new(Mars,"火星医疗卡",4,10,true),
     new(Mover,"巨大作物移动工具",4,3,true),new("(O)458","原版花束",2,2,true),new(BrokenGlasses,"破损眼镜",4,1,true),
     new(Head,"小外头套",6,1,true),new(Shirt,"小外连体衣",6,1,true),new(Shoes,"小外软鞋",4,1,true),
     new(StickerHello,"小外贴画·你好",3,2,true),new(StickerSleep,"小外贴画·晚安",3,2,true),new(StickerSurvey,"小外贴画·地球调查",3,2,true)];
    internal static bool Custom(Item item)=>Products.Any(p=>p.Id==item.QualifiedItemId&&p.Id.Contains(Prefix,StringComparison.Ordinal));
    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested+=(_,e)=>
        {
            if(e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))e.Edit(asset=>
            {
                var data=asset.AsDictionary<string,ObjectData>().Data;
                string[] ids=["MoonCard","MarsCard","CropMover"];
                string[] names=["月亮医疗卡","火星医疗卡","巨大作物移动工具"];
                string[] descriptions=["放在随身背包里。户外昏倒需要付费时消耗一张，由小外送你回家。", "放在随身背包里。战败救援时消耗一张，保护本次金币和物品。", "在农场手持后右键选中原版巨大作物，再右键选择新位置。成功后消耗；Esc 取消。"];
                for(int i=0;i<ids.Length;i++)data[Prefix+ids[i]]=new ObjectData{Name=ids[i],DisplayName=names[i],Description=descriptions[i],Type="Basic",Category=-2,Price=0,Edibility=-300,
                    Texture=helper.ModContent.GetInternalAssetName("assets/reward-items.png").Name,SpriteIndex=i,CanBeGivenAsGift=false,CanBeTrashed=true};
            });
            if(e.NameWithoutLocale.IsEquivalentTo("Data/hats"))e.Edit(asset=>
            {
                var hats=asset.AsDictionary<string,string>().Data;
                hats[Prefix+"Antennae"]="Xiaowai Antennae/戴上它，就能加入小外的地球调查。/false/true//小外的触角/0/"+Asset("antennae.png");
                hats[Prefix+"XiaowaiHead"]="Xiaowai Head/戴上小外的脸。搭配小外连体衣和软鞋，整个人变成小外。/hide/true//小外头套/0/"+Asset("xiaowai-head.png");
                hats[Prefix+"ReadingGlasses"]="Reading Glasses/一副修好的普通眼镜。可以戴在帽子栏，也可以放进背包交给艾芙琳。/true/true//普通眼镜/0/"+Asset("reading-glasses.png");
            });
            if(e.NameWithoutLocale.IsEquivalentTo("Data/Shirts"))e.Edit(asset=>asset.AsDictionary<string,ShirtData>().Data[Prefix+"XiaowaiSuit"]=new ShirtData
            {
                Name="Xiaowai Suit",DisplayName="小外连体衣",Description="柔软的绿色衣服，背着小外的小背包。穿齐头套、连体衣和软鞋，就变成小外。",
                Texture=Asset("xiaowai-shirt.png"),SpriteIndex=0,Price=0,CanBeDyed=false,HasSleeves=true,CanChooseDuringCharacterCustomization=false
            });
            if(e.NameWithoutLocale.IsEquivalentTo("Data/Boots"))e.Edit(asset=>asset.AsDictionary<string,string>().Data[Prefix+"XiaowaiShoes"]=
                "Xiaowai Shoes/软软的小外脚掌。与头套和连体衣一起穿戴，成为完整的小外。/0/0/0/0/小外软鞋/"+Asset("xiaowai-shoe-colors.png")+"/0/"+Asset("xiaowai-shoes.png"));
            if(e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))e.Edit(asset=>
            {
                var furniture=asset.AsDictionary<string,string>().Data;
                furniture[Prefix+"Nest"]="Xiaowai Nest/decor/2 2/2 1/1/0/0/小外的小窝/0/"+Asset("nest.png");
                string[] ids=["StickerHello","StickerSleep","StickerSurvey"];
                string[] names=["小外贴画·你好","小外贴画·晚安","小外贴画·地球调查"];
                for(int i=0;i<ids.Length;i++)furniture[Prefix+ids[i]]=$"{ids[i]}/painting/2 3/2 3/1/0/0/{names[i]}/{i*2}/"+Asset("xiaowai-stickers.png");
            });
        };
        string Asset(string filename)=>helper.ModContent.GetInternalAssetName("assets/"+filename).Name.Replace('/','\\');
    }
    internal static string? Purchase(Farmer who,string id,int quantity,FestivalPlayer record)
    {
        FestivalProduct? product=Products.FirstOrDefault(p=>p.Id==id);
        if(product is null)return "这件商品没有上架。";
        int purchased=record.Purchases.GetValueOrDefault(id);
        if(quantity<1||quantity>999||product.Limit>=0&&purchased+quantity>product.Limit)return "已达到本届的个人限购数量。";
        int total=checked(product.Price*quantity);
        if(product.Flowers?record.Bouquets<total:who.Money<total)return product.Flowers?"手捧花还不够。":"金币还不够。";
        Item item=ItemRegistry.Create(id,quantity);
        int capacity=0;
        foreach(Item? slot in who.Items)
            capacity+=slot is null?item.maximumStackSize():slot.canStackWith(item)?Math.Max(0,slot.maximumStackSize()-slot.Stack):0;
        if(capacity<item.Stack)return "背包放不下整批商品，没有扣款。";
        if(!who.couldInventoryAcceptThisItem(item))return "背包空间不足，没有扣款。";
        if(!who.addItemToInventoryBool(item))return "背包空间发生变化，请留出空位再试。";
        if(product.Flowers)record.Bouquets-=total;else who.Money-=total;
        record.Purchases[id]=purchased+quantity;
        return null;
    }
}
