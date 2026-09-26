using Microsoft.Xna.Framework;
using StardewValley;

namespace Welcome.Anniversary;

internal sealed class SurveyInteractions(FestivalSession session,WeddingSurvey survey,NativeFestivalDialogs dialogs)
{
    private readonly Dictionary<string,string> heard=[];

    internal bool Decoration(Point clicked)
    {
        SurveyObjective? prop=survey.Decoration(clicked);
        if(prop is null||!WeddingSurvey.InReach(prop,Game1.player.TilePoint,clicked))return false;
        if(session.MyActivity is not {Kind:"Survey",Phase:"Play"} game)
        {
            if(prop.Id is "cake" or "arch")return false;
            dialogs.Say(prop.Observation);return true;
        }
        Confirm(game,prop,clicked,prop.Observation,"就是这里！",null);
        return true;
    }

    internal bool Talk(NPC npc)
    {
        if(npc.Name is "Pierre" or "Emily")return false;
        SurveyObjective? person=survey.ForNpc(npc.Name);
        if(person is null)return false;
        bool investigating=session.MyActivity is {Kind:"Survey",Phase:"Play"};
        if(!investigating&&npc.Name is "Pierre" or "Emily" or "Willy" or "Lewis" or "Gus")return false;
        if(npc.Name==AnniversaryContent.Xiaowai)return false;
        Point at=npc.TilePoint;
        if(!WeddingSurvey.InReach(person,Game1.player.TilePoint,at))return false;
        bool alreadyHeard=heard.TryGetValue(npc.Name,out string? quote);
        if(!alreadyHeard)
        {
            if(Game1.CurrentEvent?.TryGetFestivalDataForYear(npc.Name,out quote)!=true||string.IsNullOrEmpty(quote))return false;
            heard[npc.Name]=quote;
            session.Send(new FestivalRequest{Action="SurveyTalk",Kind=npc.Name,ClickedX=at.X,ClickedY=at.Y});
            if(npc.Name is "Linus" or "Evelyn"){TalkGift(npc,person,quote,false);return true;}
            if(Game1.CurrentEvent.TryGetFestivalDialogueForYear(npc,npc.Name,out Dialogue dialogue))dialogs.Talk(npc,dialogue);
            else dialogs.Say(quote);
            return true;
        }
        if(npc.Name is "Linus" or "Evelyn"){TalkGift(npc,person,quote!,investigating);return true;}
        if(npc.Name=="Clint"&&!investigating){TalkClint(npc);return true;}
        if(session.MyActivity is {Kind:"Survey",Phase:"Play"} game)
        {
            // Re-register the conversation after a reconnect, before the later answer.
            session.Send(new FestivalRequest{Action="SurveyTalk",Kind=npc.Name,ClickedX=at.X,ClickedY=at.Y});
            Confirm(game,person,at,"“"+quote+"”","就是你！",npc);
        }
        else if(Game1.CurrentEvent?.TryGetFestivalDialogueForYear(npc,npc.Name,out Dialogue repeated)==true)dialogs.Talk(npc,repeated);
        else dialogs.Say(quote!);
        return true;
    }

    private void TalkGift(NPC npc,SurveyObjective person,string quote,bool canConfirm)
    {
        Point at=npc.TilePoint;
        session.Send(new FestivalRequest{Action="SurveyTalk",Kind=npc.Name,ClickedX=at.X,ClickedY=at.Y});
        FestivalActivity? game=canConfirm?session.MyActivity:null;
        string? gameId=game?.Id;int round=game?.Round??0;
        var options=new List<Response>();
        bool linus=npc.Name=="Linus";
        if(linus&&session.Me is {Bouquets:>0,LinusGifts:<2})options.Add(new Response("Gift","送他一束手捧花（1 束）"));
        if(!linus&&session.Me is {EvelynGifts:<2}&&GlassesExchange.Carries(Game1.player,RewardCatalog.Glasses))
            options.Add(new Response("Gift","把普通眼镜交给奶奶"));
        if(gameId is not null)options.Add(new Response("Confirm","就是你！"));
        if(options.Count==0){dialogs.Say(quote,npc);return;}
        options.Add(new Response("Cancel","取消"));
        dialogs.Ask("“"+quote+"”",options,answer=>
        {
            if(answer=="Gift")session.Send(new FestivalRequest{Action=linus?"GiftLinus":"GiftEvelyn",ClickedX=at.X,ClickedY=at.Y});
            else if(answer=="Confirm"&&gameId is not null)Submit(gameId,round,person,at);
        },npc);
    }

    private void TalkClint(NPC npc)
    {
        Point at=npc.TilePoint;
        session.Send(new FestivalRequest{Action="SurveyTalk",Kind=npc.Name,ClickedX=at.X,ClickedY=at.Y});
        bool hasBroken=GlassesExchange.Carries(Game1.player,RewardCatalog.BrokenGlasses);
        var options=new List<Response>();
        if(hasBroken)options.Add(new Response("Repair","修好一副眼镜"));
        options.Add(new Response("Cancel",hasBroken?"暂时不用":"找到眼镜再来"));
        string prompt="要不要修眼镜？一副破损的眼镜换一副普通眼镜，这次不收修理费。";
        if(!hasBroken)prompt+="你可以在艾米丽那里用 4 束花买破损眼镜，或到皮埃尔那里花 800 金买修好的。";
        dialogs.Ask(prompt,options,answer=>
        {if(answer=="Repair")session.Send(new FestivalRequest{Action="RepairGlasses",ClickedX=at.X,ClickedY=at.Y});},npc);
    }

    private void Confirm(FestivalActivity game,SurveyObjective candidate,Point clicked,string prompt,string label,NPC? speaker)
    {
        string gameId=game.Id;int round=game.Round;
        dialogs.Ask(prompt,[new Response("Confirm",label),new Response("Cancel","取消")],answer=>
        {if(answer=="Confirm")Submit(gameId,round,candidate,clicked);},speaker);
    }
    private void Submit(string gameId,int round,SurveyObjective candidate,Point clicked)
    {
        if(session.MyActivity is not { } current||current.Id!=gameId||current.Round!=round||current.Phase!="Play")
        {dialogs.Say("调查线索已经更新，请按当前线索继续。");return;}
        session.Send(new FestivalRequest{Action="Input",Game=gameId,Round=round,Kind=candidate.Id,ClickedX=clicked.X,ClickedY=clicked.Y});
    }
}
