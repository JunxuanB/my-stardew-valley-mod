using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Tools;
using StardewValley.TerrainFeatures;
using StardewValley.Internal;

namespace Welcome.Anniversary;

/// <summary>Coordinates the native festival scene with scheduling, activities and rewards.</summary>
internal sealed class AnniversaryModule
{
    private const string RodTag = AnniversaryContent.ModId + "/PreviewRod";
    private const string Currency = AnniversaryContent.ModId + "/Bouquets";
    private const string EnterRequest = "Preview/EnterRequest";
    private const string EnterAll = "Preview/EnterAll";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly FestivalLayout layout;
    private readonly AnniversaryContent content;
    private readonly FestivalSchedule schedule;
    private readonly FestivalSession session;
    private readonly WeddingSurvey survey;
    private readonly AnniversaryQaTools qaTools;
    private readonly PerScreen<ScreenState> screens = new(() => new());
    private ScreenState State => screens.Value;

    internal static bool IsActive => Context.IsWorldReady && Game1.CurrentEvent?.isFestival == true
        && Game1.currentLocation?.Map?.Properties.ContainsKey(FestivalMapBuilder.Marker) == true;
    private static bool QaAllowed => AnniversaryQaTools.Allowed;

    internal AnniversaryModule(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
        layout = helper.Data.ReadJsonFile<FestivalLayout>("assets/festival-layout.json")
            ?? throw new InvalidOperationException("Missing anniversary layout.");
        schedule = new FestivalSchedule(helper,monitor);
        survey=new WeddingSurvey(layout);
        session = new FestivalSession(helper,survey);
        qaTools=new AnniversaryQaTools(helper,monitor,text=>State.Dialogs.Say(text));
        content = new AnniversaryContent(helper, layout, schedule);
        session.Buy=RewardCatalog.Purchase;
        session.Notice=text=>
        {
            if(!string.IsNullOrEmpty(text))Game1.addHUDMessage(new HUDMessage(text,HUDMessage.newQuest_type));
        };
        session.Replied=reply=>
        {
            if(Game1.activeClickableMenu is FestivalShopMenu shop)shop.Acknowledge(reply);
            if(IsActive&&reply.Speaker!=""&&reply.Text!="")State.Dialogs.Say(reply.Text,Actor(reply.Speaker));
        };
        session.Trace=text=>monitor.Log(text,LogLevel.Trace);
        session.Changed=()=>
        {
            State.Bouquets.Value=session.Me?.Bouquets??0;
            if(Game1.activeClickableMenu is FestivalShopMenu shop)shop.Refresh();
        };
    }

    internal void Register()
    {
        helper.Events.Content.AssetRequested += content.OnAssetRequested;
        helper.Events.GameLoop.UpdateTicked += Update;
        helper.Events.Input.ButtonPressed += Button;
        helper.Events.Input.MouseWheelScrolled += ScrollToolbar;
        helper.Events.Display.RenderedHud += RenderHud;
        helper.Events.Multiplayer.ModMessageReceived += Message;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => { State.Dialogs.Cancel(); Cleanup(); screens.ResetAllScreens(); };
        helper.ConsoleCommands.Add("anniversary_qa", "手动验收：festival / menu / crop / passout / defeat / status；只用于 AnniversaryQA 农场。", QaCommand);
        var harmony=new Harmony(AnniversaryContent.ModId + ".Anniversary");
        FestivalPatches.Register(harmony);
        XiaowaiArt.Register(helper,harmony);
        XiaowaiCostume.Register(harmony);
        schedule.Register(harmony);
        RewardCatalog.Register(helper);
        FestivalShopMenu.Register(harmony);
        _ = new RewardVault(helper,monitor,harmony);
        _ = new MedicalCards(helper,harmony);
        _ = new HomeXiaowai(helper,harmony);
        _ = new GiantCropMover(helper);
        FestivalPatches.NextCatch=()=>session.Me is {Present:true,NextCatch:"(O)166" or "(O)458"} p?(p.NextCatch,p.CatchTicket):null;
        FestivalPatches.FishCaught=(item,ticket)=>session.Send(new FestivalRequest{Action="Fish",Kind=item,Round=ticket});
        FestivalPatches.ActivityLocksMovement=()=>session.MyActivity?.Phase is "Prepare" or "Practice" or "Pose";
        FestivalPatches.DrawPond = b => { if (IsActive) {State.Pond?.draw(b);DrawBerry(b);State.Presentation?.DrawWorld(b);} };
        FestivalPatches.DrawFestivalActor=(npc,b,alpha)=>State.Ambience?.DrawActor(npc,b,alpha)==true;
    }

    private void Update(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady||Game1.currentLocation?.Map is null) return;
        State.Dialogs.Update();
        if (State.WarpTicks > 0 && --State.WarpTicks == 0 && !IsActive)
        {
            Game1.currentSeason = "spring";
            Game1.dayOfMonth = 2;
            Game1.timeOfDay = 900;
            Game1.warpFarmer("Town", layout.Entry[0], layout.Entry[1], false);
        }
        if (IsActive && !State.Entered)
        {
            State.Entered = true;
            Game1.displayHUD = true;
            Game1.displayFarmer = true;
            Game1.player.ignoreCollisions = false;
            Game1.player.forceCanMove();
            if (!Game1.onScreenMenus.OfType<Toolbar>().Any()) Game1.onScreenMenus.Add(new Toolbar());
            ClearFestivalBushes();
            AddDecor();
            content.EnsureGuests(monitor);
            // Rendering-only native building: never put it into a persistent location/building list.
            State.Pond = new FishPond(new Vector2(layout.Pond[0], layout.Pond[1]));
            State.Pond.daysOfConstructionLeft.Value = 0;
            State.Pond.daysUntilUpgrade.Value = 0;
            State.Pond.nettingStyle.Value = 0;
            Game1.specialCurrencyDisplay.Register(Currency, State.Bouquets, _ => { }, (batch, position) =>
            {
                var item = ItemRegistry.GetDataOrErrorItem("(O)458");
                batch.Draw(item.GetTexture(), position, item.GetSourceRect(), Color.White, 0, Vector2.Zero, 4, SpriteEffects.None, 1);
            });
            Game1.specialCurrencyDisplay.ShowCurrency(Currency, () => IsActive);
            State.Presentation=new FestivalPresentation(session,State.Dialogs,survey);
            State.Survey=new SurveyInteractions(session,survey,State.Dialogs);
            State.Ambience=new FestivalAmbience(layout,session);
            session.Enter();
            monitor.Log("纪念日已开始。活动、积分和限购由主机结算。", LogLevel.Info);
        }
        else if (!IsActive && State.Entered) Cleanup();
        session.Update();
        if (IsActive)
        {
            Game1.displayHUD = true;
            if(session.View is null&&e.IsMultipleOf(120))session.Enter();
            State.Presentation?.Update();
            State.Ambience?.Update();
        }
    }

    private void AddDecor()
    {
        foreach (LayoutSprite sprite in layout.Sprites)
        {
            int[] r = sprite.Source;
            if(sprite.Texture=="Maps/springobjects"&&r[0]==256&&r[1]==256)
            {State.Berry=sprite;continue;}
            float depth = (sprite.SortY ?? (sprite.At[1] + r[3] * sprite.Scale)) * 4f / 10000f;
            var decor = new TemporaryAnimatedSprite(
                content.Builder.ResolveTexture(sprite.Texture), new Rectangle(r[0], r[1], r[2], r[3]),
                999999, 1, 999999, new Vector2(sprite.At[0], sprite.At[1]) * 4,
                false, false, depth, 0, Color.White, sprite.Scale * 4, 0, 0, 0);
            Game1.currentLocation.TemporarySprites.Add(decor);
        }
    }

    private void DrawBerry(SpriteBatch batch)
    {
        // Alpha-zero temporary sprites are removed by the game. This prop is drawn
        // directly from the host's maturity flag, so every regrowth can become visible.
        if(session.View?.BerryReady!=true||State.Berry is not { } sprite)return;
        var berry=ItemRegistry.GetDataOrErrorItem("(O)400");
        Vector2 position=new(sprite.At[0]*4,sprite.At[1]*4);
        float depth=(position.Y+berry.GetSourceRect().Height*sprite.Scale*4)/10000f;
        batch.Draw(berry.GetTexture(),Game1.GlobalToLocal(position),berry.GetSourceRect(),Color.White,0,Vector2.Zero,sprite.Scale*4,SpriteEffects.None,depth);
    }

    private void ClearFestivalBushes()
    {
        // Town populates bushes when the temporary location is entered, after the
        // map asset is built. Only remove bushes around the festival installations.
        GameLocation location = Game1.currentLocation;
        Rectangle[] areas = layout.ClearBushes.Select(a =>
            new Rectangle(a[0] * 64, a[1] * 64, a[2] * 64, a[3] * 64)).ToArray();
        location.largeTerrainFeatures.RemoveWhere(feature => feature is Bush
            && areas.Any(area => area.Intersects(feature.getBoundingBox())));
        foreach (var pair in location.terrainFeatures.Pairs.ToArray())
        {
            bool treeInDisplay = pair.Value is Tree && layout.ClearTrees.Any(a =>
                new Rectangle(a[0], a[1], a[2], a[3]).Contains(pair.Key.ToPoint()));
            if (treeInDisplay || pair.Value is Bush && areas.Any(area => area.Intersects(pair.Value.getBoundingBox())))
                location.terrainFeatures.Remove(pair.Key);
        }
    }

    private void Cleanup()
    {
        if(State.Entered){session.Leave();State.Presentation?.Reset();}
        if (Context.IsWorldReady)
        {
            string owner=Game1.player.UniqueMultiplayerID.ToString();
            Utility.ForEachItemContext((in ForEachItemContext context)=>
            {
                if(context.Item.modData.TryGetValue(RodTag,out string? borrower)&&borrower==owner)context.RemoveItem();
                return true;
            });
            Game1.specialCurrencyDisplay?.Unregister(Currency);
        }
        State.Entered = false;
        State.Pond = null;
        State.Presentation=null;
        State.Survey=null;
        State.Ambience=null;
        State.Berry=null;
        State.Bouquets.Value = 0;
        State.WarpTicks = 0;
    }

    private void Button(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation is null || Game1.fadeToBlack || Game1.player.isEating) return;
        if (State.Dialogs.OwnsCurrent && e.Button is SButton.Escape or SButton.ControllerB)
        {
            helper.Input.Suppress(e.Button);
            State.Dialogs.Cancel(notify:true);
            if(session.MyActivity?.Kind=="Slime")State.Presentation?.CancelCurrent();
            return;
        }
        if(IsActive && e.Button is SButton.Escape or SButton.ControllerB && session.MyActivity is {Phase:not "Result"} active
            &&active is not {Kind:"Survey",Phase:"Play"})
        {
            helper.Input.Suppress(e.Button);
            State.Presentation?.CancelCurrent();
            if(Game1.activeClickableMenu is CakeTimingMenu)Game1.exitActiveMenu();
            return;
        }
        if (e.Button == SButton.F8 && QaAllowed && Game1.activeClickableMenu is null && !Game1.player.UsingTool)
        {
            helper.Input.Suppress(e.Button);
            QaMenu();
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.player.UsingTool || Game1.fadeToBlack) return;
        if (e.Button == SButton.MouseLeft && State.QaButton.Contains((int)e.Cursor.GetScaledScreenPixels().X, (int)e.Cursor.GetScaledScreenPixels().Y))
        {
            helper.Input.Suppress(e.Button);
            QaMenu();
            return;
        }
        if (!IsActive || !e.Button.IsActionButton()) return;
        Point target = e.Button == SButton.MouseRight ? e.Cursor.GrabTile.ToPoint() : Game1.player.GetGrabTile().ToPoint();
        Point activityTarget=e.Button==SButton.MouseRight?e.Cursor.Tile.ToPoint():target;
        if(State.Presentation?.Action(activityTarget)==true){helper.Input.Suppress(e.Button);return;}
        Point playerTile=Game1.player.TilePoint;
        Rectangle clickedArea=new(activityTarget.X*64,activityTarget.Y*64,64,64);
        NPC? clickedNpc=layout.Actors.Select(a=>Game1.CurrentEvent?.getActorByName(a.Name=="Xiaowai"?AnniversaryContent.Xiaowai:a.Name))
            .FirstOrDefault(n=>n is not null&&(n.GetBoundingBox().Intersects(clickedArea)
                ||n.TilePoint.X==activityTarget.X&&(n.TilePoint.Y==activityTarget.Y||n.TilePoint.Y-1==activityTarget.Y)));
        if(clickedNpc is not null)
        {
            int distance=Math.Abs(playerTile.X-clickedNpc.TilePoint.X)+Math.Abs(playerTile.Y-clickedNpc.TilePoint.Y);
            if(distance<=2)
            {
                if(clickedNpc.Name==AnniversaryContent.Xiaowai){Handle("Xiaowai");helper.Input.Suppress(e.Button);return;}
                if(clickedNpc.Name is "Pierre" or "Emily"){Handle(clickedNpc.Name);helper.Input.Suppress(e.Button);return;}
                if(State.Survey?.Talk(clickedNpc)==true){helper.Input.Suppress(e.Button);return;}
                if(Handle(clickedNpc.Name)){helper.Input.Suppress(e.Button);return;}
            }
        }
        if(State.Survey?.Decoration(activityTarget)==true){helper.Input.Suppress(e.Button);return;}
        Point player = Game1.player.TilePoint;
        if (Math.Abs(player.X - target.X) + Math.Abs(player.Y - target.Y) > 2) return;
        FestivalPlayer? partner=session.View?.Players.FirstOrDefault(p=>p.Id!=Game1.player.UniqueMultiplayerID&&p.Present
            &&p.X==target.X&&(p.Y==target.Y||p.Y-1==target.Y));
        if(partner is not null)
        {helper.Input.Suppress(e.Button);session.Send(new FestivalRequest{Action="Invite",Kind="Kiss",Target=partner.Id});return;}
        string? interaction = null;
        foreach (LayoutInteraction item in layout.Interactions)
        {
            int[] a = item.Area;
            if (new Rectangle(a[0], a[1], a[2], a[3]).Contains(target)) { interaction = item.Id; break; }
        }
        foreach (LayoutActor actor in layout.Actors)
        {
            if (target.X == actor.At[0] && (target.Y == actor.At[1] || target.Y == actor.At[1]-1))
                interaction ??= actor.Name;
        }
        if(interaction is null&&new Rectangle(26,59,6,3).Contains(target))interaction="Photo";
        if (interaction is null) return;
        if (!Handle(interaction)) return;
        helper.Input.Suppress(e.Button);
    }

    private void ScrollToolbar(object? sender,MouseWheelScrolledEventArgs e)
    {
        if(!IsActive||!Game1.eventUp||Game1.activeClickableMenu is not null||Game1.dialogueUp||Game1.IsChatting
            ||Game1.fadeToBlack||Game1.player.UsingTool||Game1.player.isEating||!Game1.player.CanMove||!Game1.player.Items.HasAny())return;
        // The game's input loop skips pressSwitchToolButton whenever eventUp is true.
        // Invoke the native switch once on the SMAPI wheel event; menus keep their own scrolling.
        int before=Game1.player.CurrentToolIndex;
        Game1.pressSwitchToolButton();
        if(Game1.player.CurrentToolIndex!=before||e.Delta==0)return;
        // Some input providers have already advanced the mouse snapshot by this event.
        int direction=(e.Delta>0?-1:1)*(Game1.options.invertScrollDirection?-1:1);
        for(int step=1;step<=12;step++)
        {
            int index=(before+direction*step+120)%12;
            if(index>=Game1.player.Items.Count||Game1.player.Items[index] is null)continue;
            Game1.player.CurrentToolIndex=index;
            if(index!=before)
            {
                Game1.playSound("toolSwap");
                if(Game1.player.ActiveObject is not null)Game1.player.showCarrying();else Game1.player.showNotCarrying();
            }
            break;
        }
    }

    private bool Handle(string id)
    {
        switch (id)
        {
            case "Pierre":
                Game1.activeClickableMenu = new FestivalShopMenu(session,false);
                break;
            case "Emily":
                Game1.activeClickableMenu = new FestivalShopMenu(session,true);
                break;
            case "Willy":
                State.Dialogs.Ask("池子准备好了，要借一根鱼竿试试吗？", Choices(("Rod", "借一根竹竿"), ("Cancel", "暂时不用")),
                    answer => { if (answer == "Rod") BorrowRod(); }, Actor("Willy"));
                break;
            case "Cake": case "Gus":
                State.Dialogs.Ask("蛋糕就在这里。你想怎么享用？", Choices(("Pair", "邀请一位玩家切蛋糕"), ("Eat", "吃一小块蛋糕"), ("Cancel", "取消")), answer =>
                {
                    if (answer == "Pair") Participants("Cake");
                    else if (answer == "Eat") EatCake();
                }, Actor("Gus"));
                break;
            case "Lewis":
                State.Dialogs.Ask("欢迎！要来一场爱心史莱姆比赛吗？", Choices(("Slime", "爱心史莱姆比赛"), ("Cancel", "取消")), answer =>
                { if (answer == "Slime") Participants("Slime"); }, Actor("Lewis"));
                break;
            case "Xiaowai":
                if(session.MyActivity is {Kind:"Survey",Phase:"Play"} investigation)
                {
                    string idToEnd=investigation.Id;
                    Point at=Actor(AnniversaryContent.Xiaowai)!.TilePoint;
                    State.Dialogs.Ask("我来地球有什么目的……继续观察，还是先把调查本合上？",
                        Choices(("Continue","继续调查"),("End","结束这次调查")),answer=>
                        {if(answer=="End")session.Send(new FestivalRequest{Action="EndSurvey",Game=idToEnd,ClickedX=at.X,ClickedY=at.Y});},Actor(AnniversaryContent.Xiaowai));
                    break;
                }
                State.Dialogs.Ask("我来地球有什么目的……人类把两个人选同一个答案叫作默契。值得调查。", Choices(("Quiz", "小外的地球调查"), ("Survey", "外星婚礼调查"), ("Cancel", "取消")), answer =>
                { if (answer == "Quiz") Participants("Quiz"); else if (answer == "Survey") Participants("Survey"); }, Actor(AnniversaryContent.Xiaowai));
                break;
            case "Strawberry":
                session.Send(new FestivalRequest{Action="Berry"});
                break;
            case "Photo":
                State.Dialogs.Ask("在花拱下留张合影？",Choices(("Solo","单人合影"),("Pair","双人合影"),("Cancel","取消")),a=>
                {if(a=="Solo")session.Send(new FestivalRequest{Action="PhotoSolo"});else if(a=="Pair")Participants("Photo");});break;
            default: return false;
        }
        return true;
    }

    private static NPC? Actor(string name) => Game1.CurrentEvent?.getActorByName(name);
    private static Response[] Choices(params (string Key, string Label)[] values) => values.Select(v => new Response(v.Key, v.Label)).ToArray();

    private static void EatCake()
    {
        if (!IsActive || Game1.player.isEating || Game1.player.UsingTool) return;
        // Exact vanilla fair-burger path, with Pink Cake instead. It drives the
        // networked eating animation, sound, belly rub and recovery on completion.
        // The serving never enters inventory and doesn't consume the held item.
        Game1.player.eatObject(ItemRegistry.Create<StardewValley.Object>("(O)221"), overrideFullness: true);
    }

    private void Participants(string activity)
    {
        List<FestivalPlayer> candidates = session.View?.Players.Where(p=>p.Present&&p.Id!=Game1.player.UniqueMultiplayerID).ToList()??[];
        if (candidates.Count == 0) { State.Dialogs.Say("场内还没有另一位玩家。搭档加入后再来吧。"); return; }
        List<Response> options = candidates.Select(p => new Response("Player:" + p.Id, p.Name)).ToList();
        options.Add(new Response("Cancel", "取消"));
        State.Dialogs.Ask("邀请谁参加" + EarthSurvey.Name(activity) + "？", options, answer =>
        {
            if (answer == "Cancel") return;
            if (!answer.StartsWith("Player:") || !long.TryParse(answer[7..], out long id)) return;
            if(!session.Present(id))State.Dialogs.Say("对方已经离开，已取消。");
            else session.Send(new FestivalRequest{Action="Invite",Kind=activity,Target=id});
        });
    }

    private void BorrowRod()
    {
        if (Game1.player.Items.OfType<FishingRod>().Any()) { State.Dialogs.Say("你的背包里已经有鱼竿了。选中它，就可以在池边试竿。"); return; }
        Item rod = ItemRegistry.Create("(T)BambooPole");
        rod.modData[RodTag] = Game1.player.UniqueMultiplayerID.ToString();
        if (!Game1.player.addItemToInventoryBool(rod)) { State.Dialogs.Say("背包满了。留出一格再来借吧。"); return; }
        Game1.player.CurrentToolIndex = Game1.player.Items.IndexOf(rod);
        State.Dialogs.Say("这根竹竿借你，游园结束时归还。池里藏着金币和手捧花，试试手气吧。");
    }

    private void QaMenu()
    {
        State.Dialogs.Ask("纪念日 · 手动验收", IsActive
            ? Choices(("Rod", "借用鱼竿"), ("Bouquet", "测试手捧花 +10"), ("Stop", "取消当前活动"), ("Cancel", "取消"))
            : Choices(("Enter", "召集全员进入纪念日"), ("Crop", "面前生成巨大作物"), ("Rescue", "测试昏倒 / 战败（医疗卡）"), ("Cancel", "取消")), answer =>
            {
                if (answer == "Enter") RequestEntry();
                else if (answer == "Rod") BorrowRod();
                else if (answer == "Bouquet") session.Send(new FestivalRequest{Action="QaBouquet"});
                else if (answer == "Stop") State.Presentation?.CancelCurrent();
                else if(answer=="Crop")qaTools.SpawnCrop();
                else if(answer=="Rescue")QaRescueMenu();
            });
    }

    private void QaRescueMenu()
    {
        if(!QaAllowed)return;
        string text=$"随身背包：月亮卡 {AnniversaryQaTools.Count(RewardCatalog.Moon)} 张，火星卡 {AnniversaryQaTools.Count(RewardCatalog.Mars)} 张。\n昏倒会进入原版睡眠流程；战败会进入原版救援。是否携卡决定保护结果。";
        State.Dialogs.Ask(text,Choices(("passout","触发昏倒（月亮卡）"),("defeat","生命归零战败（火星卡）"),("Cancel","取消")),
            answer=>{if(answer is "passout" or "defeat")qaTools.Rescue(answer);});
    }

    private void RenderHud(object? sender, RenderedHudEventArgs e)
    {
        State.QaButton = Rectangle.Empty;
        if(IsActive)State.Presentation?.DrawHud(e.SpriteBatch);
        // Game1.drawHUD returns early for every festival. Draw the actual Toolbar here;
        // keep it in onScreenMenus so vanilla slot clicks, scrolling and hotkeys still work.
        if (IsActive && Game1.displayHUD && !Game1.game1.takingMapScreenshot)
        {
            Toolbar? toolbar = Game1.onScreenMenus.OfType<Toolbar>().FirstOrDefault();
            toolbar?.update(Game1.currentGameTime);
            toolbar?.draw(e.SpriteBatch);
        }
        if (!QaAllowed || Game1.activeClickableMenu is not null || Game1.fadeToBlack) return;
        State.QaButton = new Rectangle(Game1.uiViewport.Width / 2 - 104, 8, 208, 52);
        IClickableMenu.drawTextureBox(e.SpriteBatch, State.QaButton.X, State.QaButton.Y, 208, 52, Color.White);
        Utility.drawTextWithShadow(e.SpriteBatch, "QA 菜单  [F8]", Game1.smallFont, new Vector2(State.QaButton.X + 22, 18), Game1.textColor);
    }

    private void RequestEntry()
    {
        if (!QaAllowed || IsActive || State.WarpTicks > 0) return;
        if (!Context.IsMainPlayer)
        {
            helper.Multiplayer.SendMessage(true, EnterRequest, [AnniversaryContent.ModId], [Game1.MasterPlayer.UniqueMultiplayerID]);
            State.Dialogs.Say("已请房主召集大家。");
            return;
        }
        schedule.EnableQaDate();
        var dates = helper.GameContent.Load<Dictionary<string, string>>("Data/Festivals/FestivalDates");
        if (dates.GetValueOrDefault(AnniversaryContent.FestivalKey) != AnniversaryContent.Name || Game1.weddingsToday.Count > 0)
        { State.Dialogs.Say("测试日期与其他节日或婚礼冲突，未修改日期。"); return; }
        helper.Multiplayer.SendMessage(true, EnterAll, [AnniversaryContent.ModId]);
        QueueEntry();
    }

    private void QueueEntry()
    {
        if (!QaAllowed || IsActive) return;
        schedule.EnableQaDate();
        State.Dialogs.Cancel();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 2;
        Game1.timeOfDay = 900;
        helper.GameContent.InvalidateCache("Maps/" + AnniversaryContent.MapAsset);
        State.WarpTicks = 30;
    }

    private void Message(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != AnniversaryContent.ModId || !QaAllowed) return;
        if (e.Type == EnterRequest && Context.IsMainPlayer) RequestEntry();
        else if (e.FromPlayerID == Game1.MasterPlayer.UniqueMultiplayerID)
        {
            if (e.Type == EnterAll) QueueEntry();
        }
    }

    private void QaCommand(string command, string[] args)
    {
        if (!QaAllowed) { monitor.Log("请载入农场名以 AnniversaryQA 开头的专用测试存档。", LogLevel.Warn); return; }
        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "festival": RequestEntry(); break;
            case "menu": if (Game1.activeClickableMenu is null) QaMenu(); break;
            case "crop":qaTools.SpawnCrop();break;
            case "passout":qaTools.Rescue("passout");break;
            case "defeat":qaTools.Rescue("defeat");break;
            default: monitor.Log($"纪念日：active={IsActive}, location={Game1.currentLocation?.Name}, tile={Game1.player.Tile}, {session.Diagnostics}", LogLevel.Info); break;
        }
    }

    private sealed class ScreenState
    {
        internal readonly NativeFestivalDialogs Dialogs = new();
        internal readonly NetIntDelta Bouquets = new();
        internal FishPond? Pond;
        internal FestivalPresentation? Presentation;
        internal SurveyInteractions? Survey;
        internal FestivalAmbience? Ambience;
        internal LayoutSprite? Berry;
        internal bool Entered;
        internal int WarpTicks;
        internal Rectangle QaButton;
    }
}
