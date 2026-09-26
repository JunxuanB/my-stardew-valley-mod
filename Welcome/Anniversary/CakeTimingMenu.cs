using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Welcome.Anniversary;

/// <summary>Keep both players on the same cutting board through the result review.</summary>
internal sealed class CakeTimingMenu : IClickableMenu
{
    private readonly Action<float> submit;
    private readonly Action cancel,review;
    private readonly Func<double> clock;
    private readonly Func<FestivalActivity?> activity;
    private readonly Func<long,string> playerName;
    private readonly double start;
    private float? ownCut;
    private bool closed;
    internal string GameId { get; }
    internal CakeTimingMenu(string id,Func<double> clock,Func<FestivalActivity?> activity,
        Func<long,string> playerName,Action<float> submit,Action cancel,Action review)
        : base(Game1.uiViewport.Width/2-340,Game1.uiViewport.Height/2-200,680,400,true)
    {
        GameId=id;this.clock=clock;this.activity=activity;this.playerName=playerName;
        start=clock();this.submit=submit;this.cancel=cancel;this.review=review;
    }
    private float MarkerPosition => (float)((Math.Sin((clock()-start)*2.7)+1)/2);
    private bool Reviewing=>activity() is {Phase:"Result",FirstCut:not null,SecondCut:not null};
    private Rectangle ConfirmBounds=>new(xPositionOnScreen+width/2-72,yPositionOnScreen+height-74,144,52);
    private void Confirm()
    {
        if(Reviewing){Stop();return;}
        if(closed||ownCut.HasValue||activity()?.Phase!="Play")return;
        ownCut=MarkerPosition;submit(ownCut.Value);
    }
    public override void receiveLeftClick(int x,int y,bool playSound=true)
    {if(upperRightCloseButton?.containsPoint(x,y)==true)Stop();else if(!Reviewing||ConfirmBounds.Contains(x,y))Confirm();}
    public override void receiveKeyPress(Keys key)
    {if(Game1.options.doesInputListContain(Game1.options.menuButton,key))Stop();else if(key is Keys.Space or Keys.Enter)Confirm();}
    public override void receiveGamePadButton(Buttons b)
    {if(b==Buttons.B)Stop();else if(b==Buttons.A)Confirm();}
    private void Stop()
    {
        if(closed)return;closed=true;
        bool reviewing=Reviewing;Game1.exitActiveMenu();
        if(reviewing)review();else cancel();
    }
    public override void emergencyShutDown()
    {if(!closed){closed=true;if(Reviewing)review();else cancel();}base.emergencyShutDown();}
    public override void gameWindowSizeChanged(Rectangle oldBounds,Rectangle newBounds)
    {xPositionOnScreen=Game1.uiViewport.Width/2-340;yPositionOnScreen=Game1.uiViewport.Height/2-200;initializeUpperRightCloseButton();}
    public override void draw(SpriteBatch b)
    {
        FestivalActivity? game=activity();
        bool result=Reviewing;
        drawTextureBox(b,xPositionOnScreen,yPositionOnScreen,width,height,Color.White);
        Text(b,result?"一起切蛋糕 · 结果":"一起切蛋糕",35,Game1.dialogueFont);
        Text(b,result?$"两刀都已落下，落点相差 {Math.Abs(game!.FirstCut!.Value-game.SecondCut!.Value):0.00}。":
            ownCut.HasValue?"你的刀已落下，等待搭档……":"按空格 / 点击 / 手柄 A，落刀一次。",94);
        int left=xPositionOnScreen+48,top=yPositionOnScreen+192,barWidth=width-96;
        b.Draw(Game1.staminaRect,new Rectangle(left,top,barWidth,12),new Color(129,77,48));
        int At(float value)=>left+(int)(Math.Clamp(value,0,1)*(barWidth-8));
        // Offset the two cuts vertically too: exactly coincident cuts remain visible.
        if(game?.FirstCut is float first)
            b.Draw(Game1.staminaRect,new Rectangle(At(first)+2,top-30,4,43),Color.Red);
        if(game?.SecondCut is float second)
            b.Draw(Game1.staminaRect,new Rectangle(At(second)+2,top+13,4,30),Color.SteelBlue);
        if(!result&&(!ownCut.HasValue||game?.FirstCutter!=Game1.player.UniqueMultiplayerID))
            b.Draw(Game1.staminaRect,new Rectangle(At(ownCut??MarkerPosition),top+13,8,30),Color.SteelBlue);
        if(game?.FirstCutter is long firstId)Text(b,"红线 · 第一刀："+playerName(firstId),257,color:Color.DarkRed);
        if(game?.SecondCutter is long secondId)Text(b,"蓝线 · 第二刀："+playerName(secondId),286,color:Color.DarkSlateBlue);
        if(result)
        {
            Rectangle button=ConfirmBounds;
            drawTextureBox(b,button.X,button.Y,button.Width,button.Height,Color.White);
            Vector2 size=Game1.smallFont.MeasureString("确认");
            Utility.drawTextWithShadow(b,"确认",Game1.smallFont,new(button.Center.X-size.X/2,button.Center.Y-size.Y/2),Game1.textColor);
        }
        else Text(b,ownCut.HasValue?"落刀位置已固定。Esc / B 取消本次挑战。":"两人的落点越近越好。Esc / B 取消。",338);
        base.draw(b);drawMouse(b);
    }
    private void Text(SpriteBatch b,string text,int y,SpriteFont? font=null,Color? color=null)
        =>Utility.drawTextWithShadow(b,Game1.parseText(text,font??Game1.smallFont,width-80),font??Game1.smallFont,
            new(xPositionOnScreen+40,yPositionOnScreen+y),color??Game1.textColor);
}
