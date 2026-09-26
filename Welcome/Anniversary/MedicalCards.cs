using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework;

namespace Welcome.Anniversary;

/// <summary>Intercepts only the native rescue settlements, never snapshots a wallet or inventory.</summary>
internal sealed class MedicalCards
{
    private const string Letter=AnniversaryContent.ModId+"/RescueLetter";
    [ThreadStatic] private static bool waived;
    private static readonly HashSet<long> protectedDefeats=[];
    internal MedicalCards(IModHelper helper,Harmony harmony)
    {
        foreach(string name in new[]{"MineDeath","HospitalDeath"})
            harmony.Patch(AccessTools.Method(typeof(Event.DefaultCommands),name),prefix:new HarmonyMethod(typeof(MedicalCards),nameof(ProtectDefeat)));
        harmony.Patch(AccessTools.Method(typeof(Farmer),nameof(Farmer.Update),[typeof(GameTime),typeof(GameLocation)]),
            transpiler:new HarmonyMethod(typeof(MedicalCards),nameof(ProtectDesertEggs)));
        MethodInfo settlement=typeof(Farmer).Assembly.GetTypes().Where(t=>t.DeclaringType==typeof(Farmer))
            .SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))
            .Single(m=>m.Name.Contains("<performPassoutWarp>g__ContinuePassOut"));
        harmony.Patch(settlement,prefix:new HarmonyMethod(typeof(MedicalCards),nameof(BeginSettlement)),
            transpiler:new HarmonyMethod(typeof(MedicalCards),nameof(PassOutSettlement)),postfix:new HarmonyMethod(typeof(MedicalCards),nameof(EndSettlement)));
        helper.Events.GameLoop.DayStarted+=(_,_)=>
        {
            if(!Game1.player.modData.TryGetValue(Letter,out string? text))return;
            Game1.player.modData.Remove(Letter);
            // Inline letter has no missing Data/Mail key when the mod is removed.
            Game1.addHUDMessage(new HUDMessage("小外给你留了一封信。",HUDMessage.newQuest_type));
            letters[Game1.player.UniqueMultiplayerID]=text;
        };
        helper.Events.GameLoop.UpdateTicked+=(_,_)=>
        {
            if(!Context.IsWorldReady||Game1.activeClickableMenu is not null||Game1.eventUp||Game1.fadeToBlack||!Game1.player.CanMove)return;
            if(letters.Remove(Game1.player.UniqueMultiplayerID,out string? text))Game1.activeClickableMenu=new LetterViewerMenu(text,"小外的来信");
        };
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>{letters.Clear();protectedDefeats.Clear();};
    }
    private static readonly Dictionary<long,string> letters=[];
    private static bool Consume(Farmer who,string id)
    {
        Item? card=who.Items.FirstOrDefault(i=>i?.QualifiedItemId==id);
        if(card is null)return false;
        if(card.Stack>1)card.Stack--;else who.removeItemFromInventory(card);
        who.modData[Letter]="大人类：^昨天把你送回家的时候，地球很安静。^医疗卡已经用过了，费用由我处理。下次困了就回家，勇敢和熬夜不是同一件事。^^我来地球有什么目的……今天先写到这里。^^——小外";
        return true;
    }
    private static bool ProtectDefeat()
    {
        if(Game1.dialogueUp||!Context.IsWorldReady)return true;
        if(!protectedDefeats.Remove(Game1.player.UniqueMultiplayerID)&&!Consume(Game1.player,RewardCatalog.Mars))return true;
        Game1.player.itemsLostLastDeath.Clear();
        Game1.player.Stamina=Math.Min(Game1.player.Stamina,2f);
        Game1.drawObjectDialogue("小外在救援记录上盖了一个印章。火星医疗卡保护了本次金币和物品。");
        return false;
    }
    private static void BeginSettlement()=>waived=false;
    private static void EndSettlement()=>waived=false;
    private static int CountDesertEggs(Farmer who,string id)
    {
        if(id=="CalicoEgg"&&who.IsLocalPlayer&&who.health<=0&&Game1.killScreen)
        {
            if(protectedDefeats.Contains(who.UniqueMultiplayerID))return 0;
            if(Consume(who,RewardCatalog.Mars)){protectedDefeats.Add(who.UniqueMultiplayerID);return 0;}
        }
        // Preserve the game's legacy special-currency/category semantics at this call site.
#pragma warning disable CS0618
        return who.getItemCount(id);
#pragma warning restore CS0618
    }
    private static IEnumerable<CodeInstruction> ProtectDesertEggs(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var code in instructions)
        {
            if(code.Calls(AccessTools.Method(typeof(Farmer),nameof(Farmer.getItemCount))))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(MedicalCards),nameof(CountDesertEggs));}
            yield return code;
        }
    }
    private static void ChargePassout(Farmer who,int balance)
    {
        if(balance<who.Money&&Consume(who,RewardCatalog.Moon)){waived=true;return;}
        who.Money=balance;
    }
    private static bool AddPassoutMail(NetStringHashSet mails,string mail)
    {return !waived&&mails.Add(mail);}
    private static IEnumerable<CodeInstruction> PassOutSettlement(IEnumerable<CodeInstruction> instructions)
    {
        var list=instructions.ToList();int charges=0,mail=0;
        foreach(var code in list)
        {
            if(code.operand is not MethodInfo method)continue;
            if(method==AccessTools.PropertySetter(typeof(Farmer),nameof(Farmer.Money)))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(MedicalCards),nameof(ChargePassout));charges++;}
            else if(method.Name=="Add"&&method.GetParameters().Length==1&&method.GetParameters()[0].ParameterType==typeof(string)
                &&method.DeclaringType!.IsAssignableFrom(typeof(NetStringHashSet)))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(MedicalCards),nameof(AddPassoutMail));mail++;}
        }
        if(charges!=1||mail!=1)throw new InvalidOperationException("Unsupported pass-out settlement; refusing to patch unrelated money or mail operations.");
        return list;
    }
}
