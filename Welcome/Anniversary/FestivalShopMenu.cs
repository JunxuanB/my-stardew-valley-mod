using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Menus;

namespace Welcome.Anniversary;

/// <summary>The original festival shop UI; purchases are executed once by the host.</summary>
internal sealed class FestivalShopMenu : ShopMenu
{
    private readonly FestivalSession session;
    private readonly bool flowers;
    private double pendingUntil;
    private FestivalRequest? pendingPurchase;
    internal FestivalShopMenu(FestivalSession session,bool flowers)
        : base(flowers?RewardCatalog.EmilyShop:AnniversaryContent.PierreShop,Stock(session,flowers))
    {
        this.session=session;this.flowers=flowers;
        categoriesToSellHere.Clear();tagsToSellHere.Clear();
        canPurchaseCheck=index=>index>=0&&index<forSale.Count&&itemPriceAndStock[forSale[index]].Stock>0
            &&(flowers?(session.Me?.Bouquets??0):Game1.player.Money)>=itemPriceAndStock[forSale[index]].Price;
    }
    private static Dictionary<ISalable,ItemStockInformation> Stock(FestivalSession session,bool flowers)
    {
        var stock=new Dictionary<ISalable,ItemStockInformation>();
        foreach(var product in RewardCatalog.Products.Where(p=>p.Flowers==flowers))
        {
            int count=product.Limit<0?infiniteStock:Math.Max(0,product.Limit-(session.Me?.Purchases.GetValueOrDefault(product.Id)??0));
            if(count==0)continue;
            stock[ItemRegistry.Create(product.Id)]=new(product.Price,count,stockMode:LimitedStockMode.Player);
        }
        return stock;
    }
    internal void Refresh()
    {
        bool removed=false;
        foreach(var item in itemPriceAndStock.Keys.ToArray())
        {
            if(item is not Item concrete)continue;
            var product=RewardCatalog.Products.First(p=>p.Id==concrete.QualifiedItemId);
            var stock=itemPriceAndStock[item];
            stock.Stock=product.Limit<0?infiniteStock:Math.Max(0,product.Limit-(session.Me?.Purchases.GetValueOrDefault(product.Id)??0));
            if(stock.Stock==0){forSale.Remove(item);itemPriceAndStock.Remove(item);removed=true;}
            else itemPriceAndStock[item]=stock;
        }
        currentItemIndex=Math.Clamp(currentItemIndex,0,Math.Max(0,forSale.Count-forSaleButtons.Count));
        if(removed)updateSaleButtonNeighbors();
    }
    internal void Acknowledge(FestivalReply reply)
    {
        if(pendingPurchase?.Id!=reply.Request)return;
        pendingPurchase=null;pendingUntil=0;
        if(reply.Success)Game1.playSound("purchase");
    }
    public override void update(GameTime time)
    {
        base.update(time);
        if(pendingPurchase is not null&&session.Now>=pendingUntil)
        {
            pendingUntil=session.Now+3;
            session.Send(pendingPurchase); // retry the same operation ID, never a second charge
        }
    }
    internal static void Register(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(ShopMenu),"tryToPurchaseItem"),prefix:new HarmonyMethod(typeof(FestivalShopMenu),nameof(Purchase)));
        harmony.Patch(AccessTools.Method(typeof(ShopMenu),nameof(getPlayerCurrencyAmount)),prefix:new HarmonyMethod(typeof(FestivalShopMenu),nameof(Balance)));
        harmony.Patch(AccessTools.Method(typeof(ShopMenu),nameof(drawCurrency)),prefix:new HarmonyMethod(typeof(FestivalShopMenu),nameof(Currency)));
    }
    private static bool Purchase(ShopMenu __instance,ISalable item,int stockToBuy,ref bool __result)
    {
        if(__instance is not FestivalShopMenu menu)return true;
        __result=false;
        if(item is Item concrete&&menu.pendingPurchase is null)
        {
            menu.pendingUntil=menu.session.Now+3;
            menu.pendingPurchase=new FestivalRequest{Action="Buy",Kind=concrete.QualifiedItemId,Choice=stockToBuy};
            menu.session.Send(menu.pendingPurchase);
        }
        return false;
    }
    private static bool Balance(ref int __result)
    {
        if(Game1.activeClickableMenu is not FestivalShopMenu{flowers:true} menu)return true;
        __result=menu.session.Me?.Bouquets??0;return false;
    }
    private static bool Currency(ShopMenu __instance,SpriteBatch b)
    {
        // The normal upper-left bouquet counter is the single source of the balance.
        return __instance is not FestivalShopMenu{flowers:true};
    }
    public override void draw(SpriteBatch b)
    {
        base.draw(b);
        if(!flowers)return;
        var bouquet=ItemRegistry.GetDataOrErrorItem("(O)458");
        for(int i=0;i<forSaleButtons.Count&&currentItemIndex+i<forSale.Count;i++)
        {
            Rectangle bounds=forSaleButtons[i].bounds;
            b.Draw(Game1.staminaRect,new Rectangle(bounds.Right-57,bounds.Y+30,45,48),new Color(255,231,166));
            b.Draw(bouquet.GetTexture(),new Vector2(bounds.Right-57,bounds.Y+30),bouquet.GetSourceRect(),Color.White,0,Vector2.Zero,3,SpriteEffects.None,1);
        }
        drawMouse(b);
    }
}
