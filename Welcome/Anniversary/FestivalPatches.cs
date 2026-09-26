using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Tools;
using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;

namespace Welcome.Anniversary;

/// <summary>Small, scene-scoped hooks: native festival tool restrictions otherwise prevent fishing.</summary>
internal static class FestivalPatches
{
    internal static Action<SpriteBatch>? DrawPond;
    internal static Func<NPC,SpriteBatch,float,bool>? DrawFestivalActor;
    internal static Action<string,int>? FishCaught;
    internal static Func<(string ItemId,int Ticket)?>? NextCatch;
    internal static Func<bool>? ActivityLocksMovement;
    private static readonly ConditionalWeakTable<FishingRod,CatchPlan> catches=new();

    internal static void Register(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Event), nameof(Event.canPlayerUseTool)),
            postfix: new HarmonyMethod(typeof(FestivalPatches), nameof(AllowFishing)));
        harmony.Patch(AccessTools.Method(typeof(GameLocation), nameof(GameLocation.draw), [typeof(SpriteBatch)]),
            postfix: new HarmonyMethod(typeof(FestivalPatches), nameof(DrawLocation)));
        harmony.Patch(AccessTools.Method(typeof(NPC),nameof(NPC.draw),[typeof(SpriteBatch),typeof(float)]),
            prefix:new HarmonyMethod(typeof(FestivalPatches),nameof(DrawActor)));
        harmony.Patch(AccessTools.Method(typeof(FishingRod), nameof(FishingRod.tickUpdate)),
            postfix: new HarmonyMethod(typeof(FestivalPatches), nameof(ReleaseFishingLock)));
        harmony.Patch(AccessTools.Method(typeof(Event),nameof(Event.caughtFish)),postfix:new HarmonyMethod(typeof(FestivalPatches),nameof(OnCatch)));
        harmony.Patch(AccessTools.Method(typeof(GameLocation),nameof(GameLocation.getFish)),prefix:new HarmonyMethod(typeof(FestivalPatches),nameof(FestivalFish)));
        harmony.Patch(AccessTools.Method(typeof(FishingRod),nameof(FishingRod.playerCaughtFishEndFunction)),
            prefix:new HarmonyMethod(typeof(FestivalPatches),nameof(ShowCatch)));
        harmony.Patch(AccessTools.Method(typeof(FishingRod),"calculateTimeUntilFishingBite"),
            postfix:new HarmonyMethod(typeof(FestivalPatches),nameof(QuickerBite)));
    }

    private static void AllowFishing(ref bool __result)
    {
        if (AnniversaryModule.IsActive && Game1.activeClickableMenu is null && !Game1.dialogueUp
            && Game1.player.CurrentTool is FishingRod && NextCatch?.Invoke() is not null) __result = true;
    }

    private static void DrawLocation(GameLocation __instance, SpriteBatch b)
    {
        if (AnniversaryModule.IsActive && ReferenceEquals(__instance, Game1.currentLocation)) DrawPond?.Invoke(b);
    }
    private static bool DrawActor(NPC __instance,SpriteBatch b,float alpha)
    {
        if(!AnniversaryModule.IsActive||!__instance.EventActor)return true;
        if(__instance.Name is "Pierre" or "Emily")
        {
            __instance.faceDirection(2);__instance.Sprite.StopAnimation();
            __instance.Sprite.CurrentFrame=0;__instance.Sprite.UpdateSourceRect();__instance.flip=false;
        }
        return DrawFestivalActor?.Invoke(__instance,b,alpha)!=true;
    }

    private static void ReleaseFishingLock(Farmer who)
    {
        // Vanilla completion uses !eventUp for movement, which stays false during a festival.
        if (AnniversaryModule.IsActive && who.IsLocalPlayer && !who.UsingTool && !who.isEating
            && ActivityLocksMovement?.Invoke()!=true && who.freezePause <= 0 && Game1.activeClickableMenu is null && !Game1.dialogueUp)
            who.CanMove = true;
    }
    private static void OnCatch(Farmer who,string itemId)
    {
        if(!AnniversaryModule.IsActive||!who.IsLocalPlayer||who.CurrentTool is not FishingRod rod)return;
        if(catches.TryGetValue(rod,out CatchPlan? plan)&&plan.ItemId==itemId)
        {catches.Remove(rod);FishCaught?.Invoke(itemId,plan.Ticket);}
    }
    private static bool FestivalFish(GameLocation __instance,Farmer who,ref Item __result)
    {
        if(!AnniversaryModule.IsActive||!ReferenceEquals(__instance,Game1.currentLocation)||!who.IsLocalPlayer)return true;
        if(who.CurrentTool is not FishingRod rod||NextCatch?.Invoke() is not { } plan)return true;
        catches.Remove(rod);catches.Add(rod,new CatchPlan(plan.ItemId,plan.Ticket));
        // Native collectible items retain cast, bite, hook and pull animations, without
        // spawning fish or a fish-specific minigame. The host preselects the matching reward.
        __result=ItemRegistry.Create(plan.ItemId);return false;
    }
    private static void QuickerBite(Farmer who,ref float __result)
    {if(AnniversaryModule.IsActive&&who.IsLocalPlayer&&__result>0)__result*=.85f;}
    private static bool ShowCatch(FishingRod __instance)
    {
        if(!AnniversaryModule.IsActive)return true;
        Farmer? who=__instance.getLastFarmerToUse();
        if(who?.IsLocalPlayer!=true||__instance.whichFish is null)return true;
        if(__instance.fishCaught)return false;
        who.Halt();who.armOffset=Vector2.Zero;who.canReleaseTool=false;who.CanMove=false;who.UsingTool=true;
        who.faceDirection(2);who.FarmerSprite.setCurrentFrame(84);
        __instance.castedButBobberStillInAir=false;
        __instance.isReeling=false;__instance.isFishing=false;__instance.pullingOutOfWater=false;
        __instance.treasureCaught=false;__instance.fishCaught=true;
        // Keep the original fish/sprite result bubble until the player clicks. Vanilla
        // doneHoldingFish still handles cleanup and won't add fish during a festival.
        Game1.CurrentEvent.caughtFish(__instance.whichFish.QualifiedItemId,__instance.fishSize,who);
        Game1.playSound("fishSlap");
        return false;
    }
    private sealed record CatchPlan(string ItemId,int Ticket);
}
