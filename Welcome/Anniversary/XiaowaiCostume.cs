using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Welcome.Anniversary;

/// <summary>Three real equipment pieces opt into the approved Xiaowai art, without changing the farmer.</summary>
internal static class XiaowaiCostume
{
    private static readonly ConditionalWeakTable<Farmer,WalkState> walks=new();
    internal static bool Equipped(Farmer who)=>who.hat.Value?.QualifiedItemId==RewardCatalog.Head
        &&who.shirtItem.Value?.QualifiedItemId==RewardCatalog.Shirt&&who.boots.Value?.QualifiedItemId==RewardCatalog.Shoes;

    internal static void Register(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(FarmerRenderer),"draw",
            [typeof(SpriteBatch),typeof(FarmerSprite.AnimationFrame),typeof(int),typeof(Rectangle),typeof(Vector2),typeof(Vector2),typeof(float),typeof(int),typeof(Color),typeof(float),typeof(float),typeof(Farmer)]),
            prefix:new HarmonyMethod(typeof(XiaowaiCostume),nameof(Draw)));
        harmony.Patch(AccessTools.Method(typeof(FarmerRenderer),"drawMiniPortrat"),prefix:new HarmonyMethod(typeof(XiaowaiCostume),nameof(MiniPortrait)));
    }

    private static bool Draw(SpriteBatch b,FarmerSprite.AnimationFrame animationFrame,Rectangle sourceRect,Vector2 position,
        Vector2 origin,float layerDepth,int facingDirection,Color overrideColor,float rotation,float scale,Farmer who)
    {
        if(!Equipped(who))return true;
        WalkState state=walks.GetValue(who,_=>new WalkState{Position=who.Position});
        if(!FarmerRenderer.isDrawingForUI)
        {
            float moved=Vector2.Distance(state.Position,who.Position)/64;
            if(moved<2)state.Distance+=moved;else state.Distance=0;
            if(moved>.001f)state.LastMoved=Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            state.Position=who.Position;
            state.Moving=who.isMoving()||Game1.currentGameTime.TotalGameTime.TotalMilliseconds-state.LastMoved<120;
            if(!state.Moving)state.Distance=0;
        }
        int row=facingDirection==2?0:facingDirection==1?1:facingDirection==0?2:3;
        int frame=state.Moving&&!FarmerRenderer.isDrawingForUI?XiaowaiArt.WalkFrame(state.Distance,row):0;
        Texture2D texture=XiaowaiArt.Walk;
        Rectangle art=XiaowaiArt.Frame(frame,row);
        if(!FarmerRenderer.isDrawingForUI&&(who.IsSitting()||who.isInBed.Value))
        {texture=XiaowaiArt.Poses;art=new Rectangle((who.isInBed.Value?4:2)*XiaowaiArt.PoseSize,0,XiaowaiArt.PoseSize,XiaowaiArt.PoseSize);}
        Vector2 offset=new(animationFrame.xOffset*4,animationFrame.positionOffset*4);
        int humanHeight=sourceRect.Height*4;
        if(!FarmerRenderer.isDrawingForUI&&who.swimming.Value)
        {art.Height/=2;humanHeight/=2;position.Y+=64;}
        Vector2 adjustment=new((art.Width-sourceRect.Width*4)/2f,art.Height-humanHeight);
        b.Draw(texture,position+origin+offset-adjustment*scale,art,overrideColor,rotation,origin*4,scale,SpriteEffects.None,layerDepth);
        return false;
    }

    private static bool MiniPortrait(SpriteBatch b,Vector2 position,float layerDepth,float scale,Farmer who,float alpha)
    {
        if(!Equipped(who))return true;
        b.Draw(XiaowaiArt.Walk,position,new Rectangle(0,0,96,96),Color.White*alpha,0,Vector2.Zero,scale/6f,SpriteEffects.None,layerDepth);
        return false;
    }
    private sealed class WalkState{internal Vector2 Position;internal float Distance;internal double LastMoved=-1000;internal bool Moving;}
}
