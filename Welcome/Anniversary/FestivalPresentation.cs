using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Welcome.Anniversary;

internal sealed class FestivalPresentation(FestivalSession session,NativeFestivalDialogs dialogs,WeddingSurvey survey)
{
    private readonly HashSet<string> shown=[];
    private string? ownedGame;
    private int ownedRound=-1;
    private string? lockedGame;
    private string? oldSession;
    private FestivalRequest? pendingCakeRequest;
    internal void Update()
    {
        FestivalSnapshot? view=session.View;if(view is null)return;
        if(oldSession!=view.Id){shown.Clear();oldSession=view.Id;}
        if(pendingCakeRequest is { } request&&Game1.activeClickableMenu is null)
        {
            pendingCakeRequest=null;
            session.Send(request);
            return;
        }
        FestivalActivity? game=session.MyActivity;
        bool cakeResult=game is {Kind:"Cake",Phase:"Result",FirstCut:not null,SecondCut:not null};
        if(ownedGame is not null && (game?.Id!=ownedGame || game.Round!=ownedRound || (game.Phase is "Result" or "Reveal")&&!cakeResult))
        {
            if(dialogs.OwnsCurrent)dialogs.Cancel();
            if(Game1.activeClickableMenu is CakeTimingMenu cake && cake.GameId==ownedGame)Game1.exitActiveMenu();
            ownedGame=null;
        }
        bool locked=game?.Phase is "Prepare" or "Practice" or "Pose";
        if(locked)
        {
            string lockKey=game!.Id;
            if(lockedGame!=lockKey)Game1.player.Halt();
            if(game?.Phase=="Practice"&&shown.Add(game.Id+":practice-facing")&&game.Targets.FirstOrDefault(t=>t.Owner==Game1.player.UniqueMultiplayerID) is { } practice)
            {
                Vector2 delta=new Vector2(practice.X,practice.Y)-Game1.player.Tile;
                Game1.player.faceDirection(Math.Abs(delta.X)>Math.Abs(delta.Y)?delta.X>0?1:3:delta.Y>0?2:0);
            }
            Game1.player.CanMove=false;lockedGame=lockKey;
        }
        else if(lockedGame is not null)
        {if(!Game1.player.isEating&&!Game1.player.UsingTool)Game1.player.forceCanMove();lockedGame=null;}
        if(Game1.activeClickableMenu is not null || Game1.player.isEating || Game1.player.UsingTool)return;
        if(game is null)return;
        string key=game.Id+":"+game.Phase+":"+game.Round;
        if(game is {Kind:"Slime",Phase:"Prepare"}&&shown.Add(key))
        {
            // One shared starting line before practice; never relocate players during play.
            Game1.player.Position=new Vector2(game.A==Game1.player.UniqueMultiplayerID?27:29,70)*64;
            Game1.player.Halt();Game1.player.faceDirection(0);Game1.player.CanMove=false;
            session.Send(new FestivalRequest{Action="PracticeReady",Game=game.Id});return;
        }
        if(game.Phase=="Invite" && game.B==Game1.player.UniqueMultiplayerID && shown.Add(key))
        {
            ownedGame=game.Id;ownedRound=game.Round;
            string name=view.Players.FirstOrDefault(p=>p.Id==game.A)?.Name??"另一位玩家";
            dialogs.Ask($"{name}邀请你参加{EarthSurvey.Name(game.Kind)}。",Responses(("Yes","接受"),("Cancel","暂时不参加")),
                answer=>session.Send(new FestivalRequest{Action=answer=="Yes"?"Accept":"Decline",Game=game.Id}));
        }
        if(game.Phase=="Play"&&!game.Submitted.Contains(Game1.player.UniqueMultiplayerID)&&shown.Add(key))
        {
            ownedGame=game.Id;ownedRound=game.Round;
            if(game.Kind=="Cake")Game1.activeClickableMenu=new CakeTimingMenu(game.Id,()=>session.Now,
                ()=>session.MyActivity is {Kind:"Cake"} current&&current.Id==game.Id?current:null,
                id=>session.View?.Players.FirstOrDefault(p=>p.Id==id)?.Name??"玩家",
                value=>session.Send(new FestivalRequest{Action="Input",Game=game.Id,Round=game.Round,Value=value}),()=>Cancel(game),
                ()=>QueueCakeRequest(game,"ReviewCake"));
            else if(game.Kind=="Quiz")
            {
                EarthQuestion q=EarthSurvey.Questions[game.Questions[game.Round]];
                List<Response> responses=q.Answers.Select((a,i)=>new Response(i.ToString(),a)).ToList();responses.Add(new("Cancel","取消本次调查"));
                dialogs.Ask(q.Question,responses,answer=>
                {if(int.TryParse(answer,out int index))session.Send(new FestivalRequest{Action="Input",Game=game.Id,Round=game.Round,Choice=index});else Cancel(game);});
            }
        }
        if(game.Phase is "Result" or "Reveal" && !cakeResult && shown.Add(key))dialogs.Say(game.Result);
        if(game.Phase=="Pose"&&shown.Add(key))
        {
            if(game.Kind=="Kiss")
            {
                long other=game.A==Game1.player.UniqueMultiplayerID?game.B:game.A;
                if(session.Present(other))
                {
                    bool left=Game1.player.TilePoint.X>session.Position(other).X;
                    Game1.player.faceDirection(left?3:1);
                    // Same native kiss frames, without the vanilla marriage/event gate or achievements.
                    Game1.player.FarmerSprite.animateOnce(new FarmerSprite.AnimationFrame[]
                    {new(101,1000,0,false,left),new(6,1,false,left,farmer=>farmer.Halt())});
                }
            }
            else
            {
                Game1.player.Position=new Vector2(game.A==Game1.player.UniqueMultiplayerID?28:30,61)*64;
                Game1.player.faceDirection(2);Game1.player.doEmote(20);
                for(int i=0;i<20;i++)Petal(new Vector2((26+Game1.random.Next(6))*64,59*64));
                if(game.Id[0]%3==0)
                    Game1.currentLocation.TemporarySprites.Add(new TemporaryAnimatedSprite(XiaowaiArt.WalkAsset,
                        XiaowaiArt.Frame(0,0),3000,1,0,new Vector2(31*64,60*64),false,false,.42f,0,Color.White,XiaowaiArt.DrawScale,0,0,0));
            }
        }
    }
    internal void CancelCurrent(){if(session.MyActivity is { } g)Cancel(g);}
    private void Cancel(FestivalActivity game)
    {
        if(game.Kind=="Cake")QueueCakeRequest(game,"Cancel");
        else session.Send(new FestivalRequest{Action="Cancel",Game=game.Id});
    }
    private void QueueCakeRequest(FestivalActivity game,string action)
    {ownedGame=null;ownedRound=-1;pendingCakeRequest=new FestivalRequest{Action=action,Game=game.Id};}
    private static Response[] Responses(params (string Key,string Text)[] a)=>a.Select(v=>new Response(v.Key,v.Text)).ToArray();
    internal bool Action(Point tile)
    {
        FestivalActivity? game=session.MyActivity;if(game is null)return false;
        if(game.Kind=="Slime"&&game.Phase is "Play" or "Practice")
        {
            FestivalTarget? target=game.Targets.OrderBy(t=>Vector2.Distance(new(t.X,t.Y),tile.ToVector2())).FirstOrDefault();
            if(target is not null&&Vector2.Distance(new(target.X,target.Y),tile.ToVector2())<=1.8)
                session.Send(new FestivalRequest{Action="Input",Game=game.Id,Round=game.Round,Choice=target.Id});
            return true;
        }
        return false;
    }
    internal void DrawWorld(SpriteBatch b)
    {
        if(session.View is not { } view)return;
        Texture2D slime=Game1.content.Load<Texture2D>("Characters/Monsters/Green Slime");
        foreach(var game in view.Activities.Where(a=>a.Kind=="Slime"&&a.Phase is "Play" or "Practice"))
        foreach(var t in game.Targets)
        {
            Vector2 at=Game1.GlobalToLocal(new Vector2(t.X,t.Y)*64);
            b.Draw(slime,at,new Rectangle(((int)(session.ServerNow*6)%4)*16,0,16,24),Color.White,0,Vector2.Zero,4,SpriteEffects.None,(t.Y+1)*64/10000);
            if(game.Phase=="Play"&&t.Id==game.Heart)
                b.Draw(Game1.emoteSpriteSheet,at+new Vector2(8,-42),Game1.getSourceRectForStandardTileSheet(Game1.emoteSpriteSheet,20,16,16),
                    Color.White,0,Vector2.Zero,3,SpriteEffects.None,1);
        }
    }
    internal void DrawHud(SpriteBatch b)
    {
        if(session.View is not { } view)return;
        FestivalActivity? game=session.MyActivity;
        string text="";
        if(game is not null)
        {
            text=game.Phase switch
            {
                "Practice"=>game.Submitted.Contains(Game1.player.UniqueMultiplayerID)?"已完成练习，等待搭档。":"刘易斯：右键抓住面前的史莱姆。Esc / B 取消",
                "Play" when game.Kind=="Slime"=>$"爱心史莱姆 · 第 {game.Round+1}/5 轮   {game.ScoreA} : {game.ScoreB} · 右键抓爱心目标 / Esc 取消",
                "Play" when game.Kind=="Survey"=>SurveyStatus(game),
                _=>""
            };
        }
        if(text=="")return;
        text=Game1.parseText(text,Game1.smallFont,Math.Min(900,Game1.uiViewport.Width-72));
        var size=Game1.smallFont.MeasureString(text);int w=Math.Min((int)size.X+40,Game1.uiViewport.Width-32);
        IClickableMenu.drawTextureBox(b,(Game1.uiViewport.Width-w)/2,80,w,(int)size.Y+28,Color.White);
        Utility.drawTextWithShadow(b,text,Game1.smallFont,new((Game1.uiViewport.Width-w)/2+20,94),Game1.textColor);
    }
    private string SurveyStatus(FestivalActivity game)
    {
        long me=Game1.player.UniqueMultiplayerID;
        SurveyObjective? target=survey.Current(game);
        string clue=target is null?"":game.A==me?target.ClueA:target.ClueB;
        return $"婚礼调查 · {game.Round+1}/3\n{clue}";
    }
    private static void Petal(Vector2 position)
    {
        Game1.currentLocation.TemporarySprites.Add(new TemporaryAnimatedSprite("Maps/Festivals",new Rectangle(80,32,16,16),120,1,40,position,false,false,.9f,.005f,Color.Pink,1,0,0,.04f)
        {motion=new Vector2((float)Game1.random.NextDouble()- .5f,1f)});
    }
    internal void Reset(){shown.Clear();ownedGame=null;pendingCakeRequest=null;if(lockedGame is not null)Game1.player.forceCanMove();lockedGame=null;}
}
