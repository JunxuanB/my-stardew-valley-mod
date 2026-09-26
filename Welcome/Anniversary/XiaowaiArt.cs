using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Welcome.Anniversary;

/// <summary>Detailed reference art at the original world size; native NPC geometry is unchanged.</summary>
internal static class XiaowaiArt
{
    internal const int FrameWidth=96;
    internal const int FrameHeight=128;
    internal const int PoseSize=160;
    internal const float DrawScale=1f;
    private static IModHelper? helper;
    private static Texture2D? walk;
    private static Texture2D? poses;
    internal static Texture2D Walk=>walk??=helper!.ModContent.Load<Texture2D>("assets/xiaowai-detail.png");
    internal static Texture2D Poses=>poses??=helper!.ModContent.Load<Texture2D>("assets/xiaowai-home-detail.png");
    internal static string WalkAsset=>helper!.ModContent.GetInternalAssetName("assets/xiaowai-detail.png").Name;
    internal static Rectangle Frame(int column,int row)=>new(column*FrameWidth,row*FrameHeight,FrameWidth,FrameHeight);
    internal static int WalkFrame(float distance,int row)
    {
        // Contact -> passing -> opposite contact -> passing. Distance is in tiles.
        if(row is 1 or 3)return (int)(distance*6)%4;
        ReadOnlySpan<int> cycle=[0,1,2,3,2,1];
        return cycle[(int)(distance*6)%cycle.Length];
    }

    internal static void Register(IModHelper modHelper,Harmony harmony)
    {
        helper=modHelper;
        harmony.Patch(AccessTools.Method(typeof(NPC),nameof(NPC.draw),[typeof(SpriteBatch),typeof(float)]),
            prefix:new HarmonyMethod(typeof(XiaowaiArt),nameof(DrawActor)));
    }

    private static bool DrawActor(NPC __instance,SpriteBatch b,float alpha)
    {
        if(__instance.Name!=AnniversaryContent.Xiaowai||!__instance.EventActor||!AnniversaryModule.IsActive)return true;
        if(__instance.IsInvisible)return false;
        // Use the vanilla NPC's position and origin, expressed in the detailed pixel grid.
        Vector2 at=__instance.getLocalPosition(Game1.viewport)
            +new Vector2(__instance.GetSpriteWidthForPositioning()*2,__instance.GetBoundingBox().Height/2);
        Vector2 origin=new(FrameWidth/2f,FrameHeight*.75f);
        // The festival host keeps the approved front-facing backpack pose.
        b.Draw(Walk,at,Frame(0,0),Color.White*alpha,0,origin,
            Math.Max(.2f,__instance.Scale)*DrawScale,SpriteEffects.None,
            Math.Max(0,__instance.StandingPixel.Y/10000f));
        return false;
    }
}
