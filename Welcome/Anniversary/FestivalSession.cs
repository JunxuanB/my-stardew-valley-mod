using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools;

namespace Welcome.Anniversary;

/// <summary>Host authority for currency, consent, rounds, gifts and activity rewards.</summary>
internal sealed class FestivalSession
{
    private readonly IModHelper helper;
    private readonly WeddingSurvey survey;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Random random = new();
    private readonly Dictionary<string, Dictionary<long, float>> answers = [];
    private readonly Dictionary<string, double> kissCooldowns = [];
    private readonly HashSet<string> handled = [];
    private readonly Dictionary<string, FestivalReply> replies = [];
    private readonly HashSet<long> joined = [];
    private readonly Dictionary<long,double> lastSeen=[];
    private readonly Dictionary<long,HashSet<string>> heard=[];
    private string visit="";
    private bool entered;
    private double lastPresence;
    private FestivalRequest? pending;
    private FestivalSnapshot? host;
    private double lastTick;
    private double lastSync;
    internal FestivalSnapshot? View { get; private set; }
    internal Action? Changed;
    internal Action<string>? Notice;
    internal Action<FestivalReply>? Replied;
    internal Action<string>? Trace;
    internal Func<Farmer, string, int, FestivalPlayer, string?>? Buy;
    internal double Now => clock.Elapsed.TotalSeconds;
    internal double ReceivedAt { get; private set; }
    internal double ServerNow => (View?.Clock ?? 0) + Now - ReceivedAt;
    internal FestivalPlayer? Me => View?.Players.FirstOrDefault(p => p.Id == Game1.player.UniqueMultiplayerID);
    internal FestivalActivity? MyActivity => View?.Activities.FirstOrDefault(a => a.NeedsPlayer(Game1.player.UniqueMultiplayerID));
    internal string Diagnostics=>$"registered={Me?.Present==true&&Me.Visit==visit}, session={View?.Id??"none"}, participants=[{string.Join(", ",View?.Players.Where(p=>p.Present).Select(p=>p.Name)??[])}]";

    internal FestivalSession(IModHelper helper,WeddingSurvey survey)
    {
        this.helper = helper;this.survey=survey;
        helper.Events.Multiplayer.ModMessageReceived += Message;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => Reset();
    }
    internal bool Present(long id)=>View?.Players.Any(p=>p.Id==id&&p.Present)==true;
    internal Point Position(long id)
    {
        if(id==Game1.player.UniqueMultiplayerID)return Game1.player.TilePoint;
        FestivalPlayer? p=View?.Players.FirstOrDefault(p=>p.Id==id);
        return p is null?new Point(-1000,-1000):new Point(p.X,p.Y);
    }
    internal void Enter()
    {
        if(entered)return;
        int date=WorldDate.Now().TotalDays;
        if (Context.IsMainPlayer && (host is null || host.Date != date))
        {
            Reset();
            host = new FestivalSnapshot { Date = date, BerryRemaining = random.Next(75, 131) };
            lastTick = Now;
        }
        entered=true;visit=Guid.NewGuid().ToString("N");View=null;pending=null;
        Send(new FestivalRequest { Action = "Join" });
    }
    internal void Leave()
    {
        Send(new FestivalRequest { Action = "Leave" });
        entered=false;pending=null;View = null;
    }
    internal void Reset()
    {
        host = null; View = null; handled.Clear(); replies.Clear(); joined.Clear(); lastSeen.Clear();heard.Clear();answers.Clear(); kissCooldowns.Clear();
        entered=false;visit="";pending=null;
    }
    internal void Send(FestivalRequest request)
    {
        if(!entered)return;
        if(request.Action is not ("Join" or "Heartbeat" or "Leave") && (Me?.Present!=true||Me.Visit!=visit))
        {
            pending??=request;
            Send(new FestivalRequest{Action="Join"});
            Notice?.Invoke("正在确认节日入场，马上处理你的操作……");
            return;
        }
        request.Session = View?.Id ?? "";
        request.Visit=visit;request.Date=WorldDate.Now().TotalDays;request.Map=AnniversaryContent.MapAsset;
        request.X=Game1.player.TilePoint.X;request.Y=Game1.player.TilePoint.Y;
        request.Facing=Game1.player.FacingDirection;
        if(request.Action is "Join" or "Heartbeat")lastPresence=Now;
        if (Context.IsMainPlayer) Handle(Game1.player.UniqueMultiplayerID, request);
        else helper.Multiplayer.SendMessage(request, "Festival/Request", [AnniversaryContent.ModId], [Game1.MasterPlayer.UniqueMultiplayerID]);
    }
    private void Message(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != AnniversaryContent.ModId || !Context.IsWorldReady) return;
        if (e.Type == "Festival/Request" && Context.IsMainPlayer) Handle(e.FromPlayerID, e.ReadAs<FestivalRequest>());
        else if (e.FromPlayerID == Game1.MasterPlayer.UniqueMultiplayerID)
        {
            if (e.Type == "Festival/State") Receive(e.ReadAs<FestivalSnapshot>());
            else if (e.Type == "Festival/Reply") ReceiveReply(e.ReadAs<FestivalReply>());
        }
    }
    private void Receive(FestivalSnapshot snapshot)
    {
        if (!AnniversaryModule.IsActive || View?.Id == snapshot.Id && View.Revision > snapshot.Revision) return;
        View = snapshot; ReceivedAt = Now; Changed?.Invoke();
        if(pending is { } next && Me?.Present==true&&Me.Visit==visit){pending=null;Send(next);}
    }
    private void ReceiveReply(FestivalReply reply)
    {if(!string.IsNullOrEmpty(reply.Text)&&reply.Speaker=="")Notice?.Invoke(reply.Text);Replied?.Invoke(reply);}
    private void Sync()
    {
        if (host is null) return;
        host.Clock = Now; host.Revision++;
        if (AnniversaryModule.IsActive) Receive(host);
        helper.Multiplayer.SendMessage(host, "Festival/State", [AnniversaryContent.ModId]);
        lastSync = Now;
    }
    private void Tell(long to, string text, string request = "",bool success=false,string speaker="")
    {
        var reply = new FestivalReply { Request = request, Text = text,Success=success,Speaker=speaker };
        if (request != "") replies[to + ":" + request] = reply;
        if (to == Game1.player.UniqueMultiplayerID) ReceiveReply(reply);
        else helper.Multiplayer.SendMessage(reply, "Festival/Reply", [AnniversaryContent.ModId], [to]);
    }
    private FestivalPlayer Player(long id)
    {
        FestivalPlayer? value = host!.Players.FirstOrDefault(p => p.Id == id);
        if (value is not null) return value;
        value = new FestivalPlayer { Id = id, Name = Game1.GetPlayer(id)?.Name ?? "玩家",NextCatch=RollCatch() };
        host.Players.Add(value); return value;
    }
    private bool Involved(long id) => host!.Activities.Any(a => a.NeedsPlayer(id));
    private bool Near(Farmer farmer, int x, int y, int distance = 4)
    {
        FestivalPlayer p=Player(farmer.UniqueMultiplayerID);
        return Math.Abs(p.X-x)+Math.Abs(p.Y-y)<=distance;
    }
    private void Handle(long sender, FestivalRequest r)
    {
        if (host is null || !Context.IsMainPlayer) return;
        Farmer? who = Game1.getOnlineFarmers().FirstOrDefault(p => p.UniqueMultiplayerID == sender);
        if (who is null) return;
        if (r.Action is "Join" or "Heartbeat")
        {
            // Remote farmer location references do not point at the peer's temporary event map.
            // Only our locally active scene sends this dated, sender-authenticated presence.
            if(!AnniversaryModule.IsActive||r.Map!=AnniversaryContent.MapAsset||r.Date!=host.Date||string.IsNullOrEmpty(r.Visit)
                ||r.X<0||r.X>=130||r.Y<0||r.Y>=110)return;
            FestivalPlayer arrival=Player(sender);
            if(r.Action=="Heartbeat"&&(r.Session!=host.Id||r.Visit!=arrival.Visit))return;
            bool first=joined.Add(sender);
            if(!first&&arrival.Visit!=r.Visit)CancelFor(sender);
            arrival.Visit=r.Visit;arrival.Present=true;arrival.X=r.X;arrival.Y=r.Y;arrival.Facing=r.Facing;lastSeen[sender]=Now;
            if(first)Trace?.Invoke($"Festival join confirmed: player={sender}, scene={r.Map}, date={r.Date}.");
            if(r.Action=="Join")Sync();
            return;
        }
        if (r.Session != host.Id || !joined.Contains(sender)||Player(sender).Visit!=r.Visit)
        {Tell(sender,"节日连接已变化，请重新进入活动。",r.Id);Sync();return;}
        lastSeen[sender]=Now;
        if(r.X>=0&&r.X<130&&r.Y>=0&&r.Y<110){Player(sender).X=r.X;Player(sender).Y=r.Y;Player(sender).Facing=r.Facing;}
        string nonce = sender + ":" + r.Id;
        if (!handled.Add(nonce))
        {
            if (replies.TryGetValue(nonce, out FestivalReply? reply)) Tell(sender, reply.Text,r.Id,reply.Success,reply.Speaker);
            return;
        }
        if (r.Action == "Leave")
        {
            RemovePlayer(sender);Sync();return;
        }
        FestivalPlayer player = Player(sender);
        switch (r.Action)
        {
            case "Invite":
                Farmer? target = Game1.getOnlineFarmers().FirstOrDefault(p => p.UniqueMultiplayerID == r.Target);
                if (r.Kind is not ("Slime" or "Cake" or "Quiz" or "Survey" or "Kiss" or "Photo") || target is null || target == who
                    || !joined.Contains(r.Target))
                { Tell(sender, "对方尚未确认入场，或已经离开节日。", r.Id); break; }
                if(Involved(sender)||Involved(r.Target)){Tell(sender,"你或对方还有未结束的活动。婚礼调查可找小外结束。",r.Id);break;}
                if(host.Activities.Any(a=>a.Kind==r.Kind&&a is not {Kind:"Cake",Phase:"Result"})){Tell(sender,"这个项目正在进行，请等上一组结束。",r.Id);break;}
                if (r.Kind == "Kiss" && !Near(who, Player(r.Target).X, Player(r.Target).Y, 2)) { Tell(sender, "先靠近对方一点。", r.Id); break; }
                host.Activities.Add(new FestivalActivity { Kind = r.Kind, A = sender, B = r.Target, Started = Now, Deadline = Now+30 });
                Tell(sender, "", r.Id,true);
                break;
            case "Accept": case "Decline": case "Cancel":
                FestivalActivity? invite = host.Activities.FirstOrDefault(a => a.Id == r.Game && a.Involves(sender));
                if (invite is null) break;
                if (r.Action != "Accept")
                {
                    // The second cut can reach the host before a peer sees the result.
                    // Closing that stale play screen mustn't erase the other's finished board.
                    if(invite is {Kind:"Cake",Phase:"Result"})ReviewCake(invite,sender,r.Id);
                    else Cancel(invite,"活动已取消，本场不发奖励。");
                    break;
                }
                if (invite.Phase != "Invite" || invite.B != sender) break;
                if(invite.Kind=="Kiss"&&!Near(who,Player(invite.A).X,Player(invite.A).Y,2))
                {Cancel(invite,"离得太远，先靠近对方再试。");break;}
                invite.Phase = invite.Kind == "Slime" ? "Prepare" : "Play";
                invite.Started = Now; invite.Deadline = Now+120;
                invite.Questions = Enumerable.Range(0, EarthSurvey.Questions.Length).OrderBy(_ => random.Next()).Take(3).ToArray();
                if(invite.Kind=="Survey")
                {
                    invite.SurveyObjectives=survey.Targets.OrderBy(_=>random.Next()).Take(3).Select(t=>t.Id).ToArray();
                    invite.Deadline=double.MaxValue;
                }
                if(invite.Kind=="Slime")invite.Deadline=Now+30;
                if (invite.Kind is "Kiss" or "Photo") { invite.Phase = "Pose"; invite.Deadline = Now+3; }
                break;
            case "EndSurvey":
                FestivalActivity? investigation=host.Activities.FirstOrDefault(a=>a.Id==r.Game&&a.Kind=="Survey"&&a.Phase=="Play"&&a.Involves(sender));
                SurveyObjective? investigator=survey.ForNpc(AnniversaryContent.Xiaowai);
                if(investigation is not null&&investigator is not null
                    &&WeddingSurvey.InReach(investigator,new Point(player.X,player.Y),new Point(r.ClickedX,r.ClickedY)))
                    Cancel(investigation,"小外合上了调查本。这次先记到这里，下次可以重新调查。");
                break;
            case "GiftLinus":
                SurveyObjective? visitor=survey.ForNpc("Linus");
                NPC? linus=Game1.getCharacterFromName("Linus")??Game1.CurrentEvent?.getActorByName("Linus");
                if(visitor is null||linus is null||!WeddingSurvey.InReach(visitor,new Point(player.X,player.Y),new Point(r.ClickedX,r.ClickedY)))
                {Tell(sender,"请靠近莱纳斯，再把花亲手交给他。",r.Id);break;}
                if(player.LinusGifts>=2){Tell(sender,"今天你已经给了我两次惊喜。把剩下的花留给你和身边的人吧。",r.Id,speaker:"Linus");break;}
                if(player.Bouquets<1){Tell(sender,"没有花也没关系。你愿意过来和我说说话，我就很高兴了。",r.Id,speaker:"Linus");break;}
                if(!who.friendshipData.TryGetValue("Linus",out Friendship? friendship))
                    who.friendshipData["Linus"]=friendship=new Friendship(0);
                // Exactly one heart, without the friendship-book multiplier on ordinary gifts.
                int cap=(Utility.GetMaximumHeartsForCharacter(linus)+1)*250-1;
                friendship.Points=Math.Min(friendship.Points+250,Math.Max(friendship.Points,cap));
                player.Bouquets--;player.LinusGifts++;
                Tell(sender,player.LinusGifts==1?"我很喜欢，谢谢你。你让我觉得，我也是今天的一份子。":"天哪！你简直是 good good！这两束花，我会好好珍惜的。",r.Id,true,"Linus");
                break;
            case "RepairGlasses":
                SurveyObjective? smith=survey.ForNpc("Clint");
                if(smith is null||!WeddingSurvey.InReach(smith,new Point(player.X,player.Y),new Point(r.ClickedX,r.ClickedY)))
                {Tell(sender,"请靠近克林特，把眼镜亲手交给他。",r.Id);break;}
                if(host.Activities.Any(a=>a.NeedsPlayer(sender)&&(a.Kind is "Survey" or "Quiz")&&a.Phase!="Result"))
                {Tell(sender,"先完成或结束小外的调查，再来找我修眼镜吧。",r.Id,speaker:"Clint");break;}
                if(!heard.TryGetValue(sender,out var smithTalk)||!smithTalk.Contains("Clint"))
                {Tell(sender,"先和克林特聊聊，再问他修眼镜的事。",r.Id);break;}
                string? repairError=GlassesExchange.Repair(who);
                Tell(sender,repairError??"修好了！这副普通眼镜可以戴，也可以拿给艾芙琳看看。",r.Id,repairError is null,"Clint");
                break;
            case "GiftEvelyn":
                SurveyObjective? elder=survey.ForNpc("Evelyn");
                NPC? evelyn=Game1.getCharacterFromName("Evelyn")??Game1.CurrentEvent?.getActorByName("Evelyn");
                if(elder is null||evelyn is null||!WeddingSurvey.InReach(elder,new Point(player.X,player.Y),new Point(r.ClickedX,r.ClickedY)))
                {Tell(sender,"请靠近艾芙琳，再把眼镜交给她。",r.Id);break;}
                if(player.EvelynGifts>=2){Tell(sender,"今天两副眼镜都找到了，亲爱的，谢谢你。",r.Id,speaker:"Evelyn");break;}
                if(!GlassesExchange.Give(who))
                {Tell(sender,"要交给奶奶的是修好的普通眼镜。请放进随身背包；戴着的眼镜需要先取下来。",r.Id,speaker:"Evelyn");break;}
                if(!who.friendshipData.TryGetValue("Evelyn",out Friendship? evelynFriendship))
                    who.friendshipData["Evelyn"]=evelynFriendship=new Friendship(0);
                int evelynCap=(Utility.GetMaximumHeartsForCharacter(evelyn)+1)*250-1;
                evelynFriendship.Points=Math.Min(evelynFriendship.Points+250,Math.Max(evelynFriendship.Points,evelynCap));
                player.EvelynGifts++;
                Tell(sender,player.EvelynGifts==1?"亲爱的谢谢你帮我找到眼镜。":"太感谢你了！我的备用眼睛怎么在这？",r.Id,true,"Evelyn");
                break;
            case "PracticeReady":
                FestivalActivity? practice=host.Activities.FirstOrDefault(a=>a.Id==r.Game&&a.Kind=="Slime"&&a.Phase=="Prepare"&&a.Involves(sender));
                if(practice is null||practice.Submitted.Contains(sender))break;
                practice.Submitted.Add(sender);
                if(practice.Submitted.Count==2)
                {
                    practice.Submitted.Clear();practice.Phase="Practice";
                    if(!SpawnPractice(practice))Cancel(practice,"请在身边留出一点空地，再开始练习。");
                }
                break;
            case "Input":
                FestivalActivity? game = host.Activities.FirstOrDefault(a => a.Id == r.Game && a.Involves(sender));
                if (game is null || r.Round != game.Round || game.Phase is "Invite" or "Prepare" or "Result" or "Reveal") break;
                if(game.Submitted.Contains(sender))break;
                if (game.Kind == "Slime") Catch(game, who, r);
                else if (game.Kind == "Survey")
                {
                    SurveyObjective? goal=survey.Current(game);
                    if(goal is null||r.Kind!=goal.Id){Tell(sender,"这不是本轮线索指向的目标，再和搭档对照一下吧。",r.Id);break;}
                    if(!WeddingSurvey.InReach(goal,new Point(player.X,player.Y),new Point(r.ClickedX,r.ClickedY)))
                    {Tell(sender,"请靠近并右键点击线索指向的物品或村民。",r.Id);break;}
                    if(goal.Npc!=""&&(!heard.TryGetValue(sender,out var conversations)||!conversations.Contains(goal.Npc)))
                    {Tell(sender,"先听这位村民说完话，再判断是不是线索里的人。",r.Id);break;}
                    Tell(sender,"",r.Id,true);
                    NextSurvey(game);
                }
                else if (game.Kind is "Cake" or "Quiz")
                {
                    if (game.Kind == "Cake" && (!float.IsFinite(r.Value) || r.Value < 0 || r.Value > 1)) break;
                    if (game.Kind == "Quiz" && r.Choice is < 0 or > 3) break;
                    if (!answers.TryGetValue(game.Id, out Dictionary<long,float>? values)) answers[game.Id] = values = [];
                    values[sender] = game.Kind == "Cake" ? r.Value : r.Choice;
                    if(game.Kind=="Cake"&&values.Count==1){game.FirstCut=r.Value;game.FirstCutter=sender;}
                    if(game.Kind=="Cake"&&values.Count==2){game.SecondCut=r.Value;game.SecondCutter=sender;}
                    game.Submitted.Add(sender);
                    if (values.Count == 2) ResolveAnswers(game, values);
                }
                break;
            case "ReviewCake":
                FestivalActivity? cake=host.Activities.FirstOrDefault(a=>a.Id==r.Game&&a.Kind=="Cake"&&a.Phase=="Result"&&a.Involves(sender));
                if(cake is not null)ReviewCake(cake,sender,r.Id);
                break;
            case "SurveyTalk":
                SurveyObjective? person=survey.ForNpc(r.Kind);
                if(person is not null&&WeddingSurvey.InReach(person,new Point(player.X,player.Y),new Point(r.ClickedX,r.ClickedY)))
                {
                    if(!heard.TryGetValue(sender,out var conversations))heard[sender]=conversations=[];
                    conversations.Add(person.Npc);
                }
                break;
            case "Berry":
                if (!Near(who,19,73,4)) break;
                if (!host.BerryReady || FormalGames()) { Tell(sender, "草莓还没成熟，或者正在等比赛结束。", r.Id); break; }
                player.Bouquets+=2; player.Strawberries++; host.BerryReady=false; host.BerryRemaining=random.Next(75,131);
                Announce($"{who.Name}采到了巨大草莓，得到 2 束手捧花！"); break;
            case "Fish":
                if(!who.Items.OfType<FishingRod>().Any()||!Near(who,38,78,8)||r.Round!=player.CatchTicket||r.Kind!=player.NextCatch)break;
                player.Fishing++;
                if(player.NextCatch=="(O)166") {who.Money+=10;Tell(sender,"财宝箱里装着 10 金！");}
                else {player.Bouquets++;Tell(sender,"捞到了 1 束手捧花！");}
                player.CatchTicket++;player.NextCatch=RollCatch();
                break;
            case "Buy":
                if (host.Activities.Any(a=>a.NeedsPlayer(sender)&&a is not {Kind:"Survey",Phase:"Play"}))
                {Tell(sender,"先完成或取消当前活动。",r.Id);break;}
                FestivalProduct? product=RewardCatalog.Products.FirstOrDefault(p=>p.Id==r.Kind);
                if(product is null||!Near(who,product.Flowers?24:18,53,6)){Tell(sender,"请到对应摊位柜台前购买。",r.Id);break;}
                string? error = Buy?.Invoke(who,r.Kind,Math.Clamp(r.Choice,1,999),player);
                Tell(sender,error ?? "",r.Id,error is null); break;
            case "PhotoSolo":
                if (!Involved(sender) && Near(who,29,61,5)) host.Activities.Add(new FestivalActivity { Kind="Photo",A=sender,B=sender,Phase="Pose",Started=Now,Deadline=Now+3 });
                break;
            case "QaBouquet":
                if (Game1.MasterPlayer.farmName.Value.StartsWith("AnniversaryQA")) player.Bouquets+=10;
                break;
        }
        Sync();
    }
    private bool FormalGames() => host!.Activities.Any(a => a.Phase is not ("Invite" or "Result") && a.Kind is "Slime" or "Cake" or "Quiz" or "Survey");
    private string RollCatch()=>random.NextDouble()<.60?"(O)458":"(O)166";
    internal void Update()
    {
        if(entered&&AnniversaryModule.IsActive&&Now-lastPresence>=1)
            Send(new FestivalRequest{Action=Me?.Present==true&&Me.Visit==visit?"Heartbeat":"Join"});
        if (!Context.IsMainPlayer || host is null) return;
        double dt=Math.Min(1,Now-lastTick); lastTick=Now;
        foreach(long id in joined.ToArray())
        {
            Farmer? farmer=Game1.getOnlineFarmers().FirstOrDefault(p=>p.UniqueMultiplayerID==id);
            if (farmer is null || Now-lastSeen.GetValueOrDefault(id)>15)RemovePlayer(id);
        }
        if (joined.Count==0) { Reset(); return; }
        if (!FormalGames() && !host.BerryReady)
        {
            host.BerryRemaining-=dt;
            if(host.BerryRemaining<=0) {host.BerryReady=true;Announce("树下的大草莓成熟了！");}
        }
        foreach(FestivalActivity game in host.Activities.ToArray())
        {
            if(game is {Kind:"Survey",Phase:"Play"} or {Kind:"Cake",Phase:"Result"})continue;
            if(game.Kind=="Slime" && game.Phase=="Play")
                foreach(FestivalTarget t in game.Targets)
                {
                    t.X+=t.VelocityX*(float)dt;t.Y+=t.VelocityY*(float)dt;
                    if(t.X<26||t.X>30){t.VelocityX=-t.VelocityX;t.X=Math.Clamp(t.X,26,30);}
                    if(t.Y<65||t.Y>68){t.VelocityY=-t.VelocityY;t.Y=Math.Clamp(t.Y,65,68);}
                }
            if(Now<game.Deadline) continue;
            if(game.Phase=="Result") {host.Activities.Remove(game);answers.Remove(game.Id);}
            else if(game.Phase=="Reveal")
            {
                game.Round++;game.Submitted.Clear();answers.Remove(game.Id);
                if(game.Round>=3) FinishQuiz(game);
                else {game.Question=game.Questions[game.Round];game.Phase="Play";game.Deadline=Now+90;}
            }
            else if(game.Phase=="Pose") FinishPose(game);
            else Cancel(game,"等待超时，活动取消。本场不发奖励。");
        }
        if(Now-lastSync>.2) Sync();
    }
    private void RemovePlayer(long id)
    {
        joined.Remove(id);lastSeen.Remove(id);CancelFor(id);Player(id).Present=false;Player(id).Bouquets=0;
        Trace?.Invoke($"Festival leave: player={id}.");
    }
    private void ResolveAnswers(FestivalActivity game, Dictionary<long,float> values)
    {
        if(game.Kind=="Cake")
        {
            float gap=Math.Abs(values[game.A]-values[game.B]);int reward=gap<=.15f?2:1;
            string evaluation=gap<=.05f?"两刀几乎重合，真是默契十足！":gap<=.15f?"两刀靠得很近，你们配合得真不错！":
                "两刀虽然隔了一点距离，不过一起分享的蛋糕，还是一样甜。";
            game.CakeReward=reward;
            Result(game,evaluation+$"#$b#你的这份奖励是 {reward} 束手捧花。欢迎再来一起切蛋糕！");
            game.Deadline=0; // Result review is acknowledged independently, with no expiry.
        }
        else
        {
            int a=(int)values[game.A],b=(int)values[game.B];if(a==b){game.ScoreA++;game.ScoreB++;}
            var q=EarthSurvey.Questions[game.Questions[game.Round]];
            game.Result=$"{Player(game.A).Name}：{q.Answers[a]}\n{Player(game.B).Name}：{q.Answers[b]}\n"+(a==b?"观察结果一致！":"不同的观察也很有意思。调查继续。");
            game.Phase="Reveal";game.Deadline=Now+5;
        }
    }
    private void SpawnSlimes(FestivalActivity game)
    {
        if(game.Targets.Count!=5)
        {
            Vector2[] starts=[new(26,66),new(30,66),new(28,67),new(26,68),new(30,68)];
            game.Targets=starts.Select((position,i)=>new FestivalTarget{Id=i,X=position.X,Y=position.Y,
                VelocityX=(random.Next(2)==0?-1:1)*.4f,VelocityY=(random.Next(2)==0?-1:1)*.25f}).ToList();
        }
        // Later rounds change the heart, not the slimes' positions.
        int previous=game.Heart;
        game.Heart=Enumerable.Range(0,5).Where(i=>game.Round==0||i!=previous).OrderBy(_=>random.Next()).First();
        game.Started=Now;game.Deadline=Now+90;
    }
    private bool SpawnPractice(FestivalActivity game)
    {
        game.Targets.Clear();
        foreach(long id in new[]{game.A,game.B})
        {
            var player=Player(id);
            Point origin=new(player.X,player.Y);
            Point front=player.Facing switch{0=>new(0,-1),1=>new(1,0),3=>new(-1,0),_=>new(0,1)};
            var offsets=new[]{front,new Point(0,1),new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(1,1),new Point(-1,1)};
            Point? spot=offsets.Select(o=>origin+o).Where(p=>Game1.currentLocation.isTileOnMap(p.ToVector2())
                &&Game1.currentLocation.isTilePassable(p.ToVector2())&&!Game1.currentLocation.isWaterTile(p.X,p.Y)
                &&!host!.Players.Any(f=>f.Present&&f.X==p.X&&f.Y==p.Y)
                &&!host.Npcs.Any(n=>Vector2.Distance(new Vector2(n.ToX,n.ToY),p.ToVector2()*64)<50)
                &&!game.Targets.Any(t=>(int)t.X==p.X&&(int)t.Y==p.Y)).Select(p=>(Point?)p).FirstOrDefault();
            if(!spot.HasValue)return false;
            game.Targets.Add(new FestivalTarget{Id=game.Targets.Count,Owner=id,X=spot.Value.X,Y=spot.Value.Y});
        }
        game.Started=Now;game.Deadline=Now+90;return true;
    }
    private void Catch(FestivalActivity game, Farmer who, FestivalRequest r)
    {
        FestivalTarget? target=game.Targets.FirstOrDefault(t=>t.Id==r.Choice);
        if(target is null || !Near(who,(int)target.X,(int)target.Y,3)) return;
        if(game.Phase=="Practice")
        {
            if(target.Owner!=who.UniqueMultiplayerID)return;
            game.Submitted.Add(who.UniqueMultiplayerID);
            game.Targets.Remove(target);
            if(game.Submitted.Count==2){game.Phase="Play";game.Submitted.Clear();SpawnSlimes(game);}
            return;
        }
        if(r.Choice!=game.Heart){Tell(who.UniqueMultiplayerID,"这只没有爱心，再找找！");return;}
        if(game.A==who.UniqueMultiplayerID)game.ScoreA++;else game.ScoreB++;
        game.Round++;
        if(game.Round<5){SpawnSlimes(game);return;}
        bool aWins=game.ScoreA>game.ScoreB;
        Player(aWins?game.A:game.B).SlimeWins++;
        Player(game.A).Bouquets+=aWins?5:2;Player(game.B).Bouquets+=aWins?2:5;
        Result(game,$"五轮结束：{game.ScoreA} : {game.ScoreB}。胜者获得 5 束，另一位获得 2 束！");
    }
    private void NextSurvey(FestivalActivity game)
    {
        game.Round++;game.Submitted.Clear();game.Started=Now;game.Deadline=double.MaxValue;
        if(game.Round==game.SurveyObjectives.Length)
        {Player(game.A).Bouquets+=3;Player(game.B).Bouquets+=3;Result(game,"三条随机调查完成！每人获得 3 束手捧花。欢迎再来领取新的调查。");}
    }
    private void FinishQuiz(FestivalActivity game)
    {Player(game.A).Bouquets+=game.ScoreA;Player(game.B).Bouquets+=game.ScoreB;Result(game,$"调查完成！共有 {game.ScoreA} 次相同观察，每人获得 {game.ScoreA} 束手捧花。下次可以继续调查。");}
    private void FinishPose(FestivalActivity game)
    {
        if(game.Kind=="Kiss")
        {
            string pair=Math.Min(game.A,game.B)+":"+Math.Max(game.A,game.B);
            Farmer? a=Game1.GetPlayer(game.A),b=Game1.GetPlayer(game.B);
            if(a is null || b is null || !Near(a,Player(game.B).X,Player(game.B).Y,2)){Cancel(game,"离得太远，接吻取消。");return;}
            bool reward=false;
            if(kissCooldowns.GetValueOrDefault(pair)<=Now)
            {
                kissCooldowns[pair]=Now+20; reward=random.NextDouble()<.3;
                if(reward){Player(game.A).Bouquets++;Player(game.B).Bouquets++;Player(game.A).Kisses++;Player(game.B).Kisses++;}
            }
            Result(game,reward?"心意被花束记住了。双方各获得 1 束！":"这一刻也值得记住。");
        }
        else Result(game,"合影完成。花瓣和今天的笑容，都留在这里了。");
    }
    private void Result(FestivalActivity game,string text){game.Phase="Result";game.Result=text;game.Deadline=Now+5;game.Targets.Clear();}
    private void Cancel(FestivalActivity game,string text)
    {host!.Activities.Remove(game);answers.Remove(game.Id);Tell(game.A,text);if(game.B!=game.A)Tell(game.B,text);}
    private void RemoveActivity(FestivalActivity game){host!.Activities.Remove(game);answers.Remove(game.Id);}
    private void ReviewCake(FestivalActivity cake,long sender,string request)
    {
        if(cake.FirstCut is null||cake.SecondCut is null||!cake.Reviewed.Add(sender))return;
        Player(sender).Bouquets+=cake.CakeReward;
        if(cake.Reviewed.Contains(cake.A)&&cake.Reviewed.Contains(cake.B))RemoveActivity(cake);
        Tell(sender,cake.Result,request,true,"Gus");
    }
    private void CancelFor(long id)
    {
        foreach(var game in host!.Activities.Where(a=>a.Involves(id)).ToArray())
        {
            if(game is {Kind:"Cake",Phase:"Result"})
            {
                // A departing participant must not close the other player's result board.
                game.Reviewed.Add(id);
                if(game.Reviewed.Contains(game.A)&&game.Reviewed.Contains(game.B))RemoveActivity(game);
            }
            else Cancel(game,"有玩家离场或断线，活动已取消。");
        }
    }
    private void Announce(string text){foreach(long id in joined)Tell(id,text);}
}
