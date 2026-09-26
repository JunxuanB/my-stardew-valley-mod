using System.Diagnostics;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Menus;

namespace Welcome.Anniversary;

/// <summary>The host commits a next-day reservation only after the online group agrees.</summary>
internal sealed class FestivalSchedule
{
    private const string Reservation=AnniversaryContent.ModId+"/NextAnniversary";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly PerScreen<ApprovalScreen> screens=new(()=>new());
    private readonly HashSet<long> agreed=[];
    private ScheduleProposal? pending;
    private double expires;
    private int qaDate=-1;
    private static Action? requestTomorrow;
    internal FestivalSchedule(IModHelper helper,IMonitor monitor){this.helper=helper;this.monitor=monitor;}
    internal int Reserved=>Context.IsWorldReady&&Game1.MasterPlayer.modData.TryGetValue(Reservation,out string? date)
        &&int.TryParse(date,out int day)?day:-1;
    internal IEnumerable<WorldDate> Dates()
    {
        if(!Context.IsWorldReady)yield break;
        if(Game1.year>=2)yield return new WorldDate(Game1.year,"spring",2);
        // Manual reservations are allowed even in year one; only the annual date is gated.
        if(Reserved>=0)yield return new WorldDate{TotalDays=Reserved};
        if(qaDate>=0&&AnniversaryQaTools.Allowed)yield return new WorldDate{TotalDays=qaDate};
    }
    internal bool IsAsset(string name)=>Dates().Any(d=>name.Equals("Data/Festivals/"+Key(d),StringComparison.OrdinalIgnoreCase));
    internal static string Key(WorldDate date)=>date.SeasonKey+date.DayOfMonth;
    internal void EnableQaDate()
    {
        if(!AnniversaryQaTools.Allowed)return;
        qaDate=new WorldDate(Game1.year,"spring",2).TotalDays;Invalidate();
    }
    internal void Register(Harmony harmony)
    {
        requestTomorrow=Request;
        harmony.Patch(AccessTools.Method(typeof(ChatBox),"runCommand"),prefix:new HarmonyMethod(typeof(FestivalSchedule),nameof(Chat)));
        helper.ConsoleCommands.Add("welcome_yea","征得所有在线玩家同意后预约明天的纪念日，第一年也可使用。",(_,_)=>Request());
        helper.Events.GameLoop.SaveLoaded+=(_,_)=>
        {
            if(Context.IsMainPlayer)RemoveInvalidReservation();
            else helper.Multiplayer.SendMessage(true,"Schedule/SyncRequest",[AnniversaryContent.ModId],[Game1.MasterPlayer.UniqueMultiplayerID]);
            Invalidate();
        };
        helper.Events.GameLoop.DayStarted+=(_,_)=>
        {
            qaDate=-1;
            if(Context.IsMainPlayer)
            {
                CancelProposal("日期已经变化，未完成的纪念日提议已取消。");
                RemoveInvalidReservation();
            }
            Invalidate();
            if(!Context.IsMainPlayer||!Dates().Any(d=>d.TotalDays==Game1.Date.TotalDays))return;
            string? conflict=Conflict(Game1.Date,helper.GameContent.Load<Dictionary<string,string>>("Data/Festivals/FestivalDates"));
            if(conflict is not null)
            {
                TellAll("本届纪念日取消："+conflict);
                if(Reserved==Game1.Date.TotalDays)SetReservation(-1);
                Invalidate();
            }
        };
        helper.Events.GameLoop.DayEnding+=(_,_)=>
        {
            qaDate=-1;
            if(!Context.IsMainPlayer)return;
            CancelProposal("大家准备休息了，未完成的纪念日提议已取消。");
            if(Reserved==Game1.Date.TotalDays)SetReservation(-1);
        };
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>
        {
            screens.Value.Dialogs.Cancel();screens.ResetAllScreens();pending=null;agreed.Clear();qaDate=-1;Invalidate();
        };
        helper.Events.GameLoop.UpdateTicked+=Update;
        helper.Events.Input.ButtonPressed+=(_,e)=>
        {
            if(screens.Value.Dialogs.OwnsCurrent&&e.Button is SButton.Escape or SButton.ControllerB)
            {helper.Input.Suppress(e.Button);screens.Value.Dialogs.Cancel(notify:true);}
        };
        helper.Events.Multiplayer.ModMessageReceived+=Message;
    }
    internal void EditDates(IDictionary<string,string> dates)
    {
        foreach(WorldDate date in Dates())
            if(Conflict(date,dates) is null&&!dates.ContainsKey(Key(date)))dates[Key(date)]=AnniversaryContent.Name;
    }
    private string? Conflict(WorldDate date,IDictionary<string,string> dates)
    {
        if(dates.TryGetValue(Key(date),out string? other)&&other!=AnniversaryContent.Name)return other;
        foreach(var festival in helper.GameContent.Load<Dictionary<string,PassiveFestivalData>>("Data/PassiveFestivals").Values)
            if(festival.Season==date.Season&&date.DayOfMonth>=festival.StartDay&&date.DayOfMonth<=festival.EndDay
                &&(string.IsNullOrEmpty(festival.Condition)||!Context.IsWorldReady||GameStateQuery.CheckConditions(festival.Condition)))
                return "当天已有被动节庆";
        if(!Context.IsWorldReady)return null;
        foreach(Farmer farmer in Game1.getAllFarmers())
        foreach(Friendship friendship in farmer.friendshipData.Values)
            if(friendship.IsEngaged()&&friendship.WeddingDate?.TotalDays==date.TotalDays)return "当天已有婚礼";
        foreach(Friendship friendship in Game1.player.team.friendshipData.Values)
            if(friendship.IsEngaged()&&friendship.WeddingDate?.TotalDays==date.TotalDays)return "当天已有玩家婚礼";
        if(date.TotalDays==Game1.Date.TotalDays&&Game1.weddingsToday.Count>0)return "今天已有婚礼";
        return null;
    }
    private static bool Chat(string commandText)
    {
        if(commandText.Trim().ToLowerInvariant() is not ("yea" or "welcome_yea"))return true;
        requestTomorrow?.Invoke();return false;
    }
    private void Request()
    {
        if(!Context.IsWorldReady){monitor.Log("请先载入存档。",LogLevel.Info);return;}
        if(Context.IsMainPlayer)Propose(Game1.player.UniqueMultiplayerID);
        else helper.Multiplayer.SendMessage(true,"Schedule/Request",[AnniversaryContent.ModId],[Game1.MasterPlayer.UniqueMultiplayerID]);
    }
    private static HashSet<long> Online()=>Game1.getOnlineFarmers().Select(p=>p.UniqueMultiplayerID).ToHashSet();
    private void Propose(long sender)
    {
        Farmer? initiator=Game1.getOnlineFarmers().FirstOrDefault(f=>f.UniqueMultiplayerID==sender);
        if(initiator is null)return;
        WorldDate next=new(Game1.Date){TotalDays=Game1.Date.TotalDays+1};
        string? error=BookingError(next);
        if(error is not null){Tell(sender,error);return;}
        if(pending is not null){Tell(sender,pending.Name+"已经提出了纪念日预约，请先完成这次确认。");return;}
        long[] members=Online().OrderBy(id=>id).ToArray();
        foreach(long id in members.Where(id=>id!=Game1.player.UniqueMultiplayerID))
        {
            var peer=helper.Multiplayer.GetConnectedPlayer(id);
            var mod=peer?.GetMod(AnniversaryContent.ModId);
            if(mod is null||mod.Version.IsOlderThan("0.6.4"))
            {Tell(sender,"请让所有在线玩家安装 Welcome 0.6.4 或更新版本，再发起预约。");return;}
        }
        pending=new ScheduleProposal{Name=initiator.Name,Initiator=sender,Today=Game1.Date.TotalDays,Day=next.TotalDays,Members=members};
        agreed.Clear();agreed.Add(sender);expires=clock.Elapsed.TotalSeconds+120;
        if(members.Length==1){Commit();return;}
        Tell(sender,"已询问其他在线玩家，全部同意后才会预约明天的纪念日。");
        foreach(long id in members.Where(id=>id!=sender))
        {
            if(id==Game1.player.UniqueMultiplayerID)ReceiveProposal(pending);
            else helper.Multiplayer.SendMessage(pending,"Schedule/Proposal",[AnniversaryContent.ModId],[id]);
        }
    }
    private string? BookingError(WorldDate date)
    {
        if(date.TotalDays!=Game1.Date.TotalDays+1)return "日期已经变化，请重新输入 /yea。";
        string? conflict=Conflict(date,helper.GameContent.Load<Dictionary<string,string>>("Data/Festivals/FestivalDates"));
        if(conflict is not null)return "不能预约："+conflict+"。";
        if(Reserved==date.TotalDays||Dates().Any(d=>d.TotalDays==date.TotalDays))return "明天已经安排了纪念日，不会重复举办。";
        return null;
    }
    private void ReceiveProposal(ScheduleProposal proposal)
    {
        if(proposal.Today!=Game1.Date.TotalDays||proposal.Day!=proposal.Today+1
            ||proposal.Initiator==Game1.player.UniqueMultiplayerID||!proposal.Members.Contains(Game1.player.UniqueMultiplayerID))return;
        var screen=screens.Value;
        if(screen.Proposal?.Id==proposal.Id)return;
        screen.Dialogs.Cancel();screen.Proposal=proposal;screen.Shown=false;
    }
    private void Update(object? sender,UpdateTickedEventArgs e)
    {
        if(!Context.IsWorldReady)return;
        var screen=screens.Value;screen.Dialogs.Update();
        if(Context.IsMainPlayer&&pending is not null)
        {
            if(pending.Today!=Game1.Date.TotalDays)CancelProposal("日期已经变化，本次预约提议已取消。");
            else if(!Online().SetEquals(pending.Members))CancelProposal("在线玩家发生变化，本次预约提议已取消；可重新输入 /yea。");
            else if(clock.Elapsed.TotalSeconds>=expires)CancelProposal("确认超时，本次未预约纪念日。");
        }
        if(screen.Proposal is not { } proposal)return;
        if(proposal.Today!=Game1.Date.TotalDays){CloseUi(proposal.Id);return;}
        if(screen.Shown||Game1.currentLocation?.Map is null||Game1.activeClickableMenu is not null||Game1.dialogueUp||Game1.eventUp||Game1.IsChatting
            ||Game1.fadeToBlack||Game1.player.UsingTool||Game1.player.isEating||!Game1.player.CanMove)return;
        screen.Shown=true;
        screen.Dialogs.Ask(proposal.Name+"想在明天举行纪念日，可以吗？",
            [new Response("Yes","可以"),new Response("No","这次先不办")],answer=>
            {
                if(screen.Proposal?.Id!=proposal.Id)return;
                screen.Proposal=null;
                var vote=new ScheduleVote{Id=proposal.Id,Agree=answer=="Yes"};
                if(Context.IsMainPlayer)Vote(Game1.player.UniqueMultiplayerID,vote);
                else helper.Multiplayer.SendMessage(vote,"Schedule/Vote",[AnniversaryContent.ModId],[Game1.MasterPlayer.UniqueMultiplayerID]);
            });
    }
    private void Vote(long sender,ScheduleVote vote)
    {
        if(pending is null||vote.Id!=pending.Id||!pending.Members.Contains(sender)||agreed.Contains(sender))return;
        if(clock.Elapsed.TotalSeconds>=expires){CancelProposal("确认超时，本次未预约纪念日。");return;}
        if(!vote.Agree)
        {CancelProposal((Game1.GetPlayer(sender)?.Name??"一位玩家")+"选择这次先不举办，明天没有新增纪念日。");return;}
        agreed.Add(sender);
        if(agreed.Count==pending.Members.Length)Commit();
    }
    private void Commit()
    {
        if(pending is not { } proposal)return;
        if(proposal.Today!=Game1.Date.TotalDays||!Online().SetEquals(proposal.Members))
        {CancelProposal("日期或在线玩家发生变化，请重新发起纪念日预约。");return;}
        WorldDate next=new(){TotalDays=proposal.Day};
        string? error=BookingError(next);
        if(error is not null){CancelProposal(error);return;}
        CloseProposal();
        SetReservation(next.TotalDays);
        TellAll($"已预约 {next.Localize()} 的纪念日。9:00–14:00 从巴士站进入小镇。");
    }
    private void CancelProposal(string message)
    {if(pending is null)return;CloseProposal();TellAll(message);}
    private void CloseProposal()
    {
        if(pending is not { } proposal)return;
        pending=null;agreed.Clear();CloseUi(proposal.Id);
        helper.Multiplayer.SendMessage(proposal.Id,"Schedule/Close",[AnniversaryContent.ModId]);
    }
    private void CloseUi(string id)
    {
        var screen=screens.Value;if(screen.Proposal?.Id!=id)return;
        screen.Proposal=null;screen.Shown=false;screen.Dialogs.Cancel();
    }
    private void RemoveInvalidReservation()
    {
        if(Reserved<0)return;
        if(Reserved<Game1.Date.TotalDays)SetReservation(-1);
    }
    private void SetReservation(int day)
    {
        ApplyReservation(day);
        helper.Multiplayer.SendMessage(day,"Schedule/Changed",[AnniversaryContent.ModId]);
    }
    private void ApplyReservation(int day)
    {
        if(day<0)Game1.MasterPlayer.modData.Remove(Reservation);
        else Game1.MasterPlayer.modData[Reservation]=day.ToString();
        Invalidate();
    }
    private void Message(object? sender,ModMessageReceivedEventArgs e)
    {
        if(e.FromModID!=AnniversaryContent.ModId||!Context.IsWorldReady)return;
        if(Context.IsMainPlayer)
        {
            if(e.Type=="Schedule/Request")Propose(e.FromPlayerID);
            else if(e.Type=="Schedule/Vote")Vote(e.FromPlayerID,e.ReadAs<ScheduleVote>());
            else if(e.Type=="Schedule/SyncRequest")
                helper.Multiplayer.SendMessage(Reserved,"Schedule/Changed",[AnniversaryContent.ModId],[e.FromPlayerID]);
        }
        if(e.FromPlayerID!=Game1.MasterPlayer.UniqueMultiplayerID)return;
        switch(e.Type)
        {
            case "Schedule/Proposal":ReceiveProposal(e.ReadAs<ScheduleProposal>());break;
            case "Schedule/Close":CloseUi(e.ReadAs<string>());break;
            case "Schedule/Changed":ApplyReservation(e.ReadAs<int>());break;
            case "Schedule/Reply":Game1.chatBox.addInfoMessage(e.ReadAs<string>());break;
        }
    }
    private void Tell(long id,string text)
    {
        if(id==Game1.player.UniqueMultiplayerID)Game1.chatBox.addInfoMessage(text);
        else helper.Multiplayer.SendMessage(text,"Schedule/Reply",[AnniversaryContent.ModId],[id]);
    }
    private void TellAll(string text){foreach(long id in Online())Tell(id,text);}
    private void Invalidate()=>helper.GameContent.InvalidateCache(asset=>
        asset.NameWithoutLocale.Name.StartsWith("Data/Festivals/",StringComparison.OrdinalIgnoreCase));
    private sealed class ApprovalScreen
    {
        internal readonly NativeFestivalDialogs Dialogs=new();
        internal ScheduleProposal? Proposal;
        internal bool Shown;
    }
}
internal sealed class ScheduleProposal
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="";
    public long Initiator {get;set;}
    public int Today {get;set;}
    public int Day {get;set;}
    public long[] Members {get;set;}=[];
}
internal sealed class ScheduleVote
{
    public string Id {get;set;}="";
    public bool Agree {get;set;}
}
