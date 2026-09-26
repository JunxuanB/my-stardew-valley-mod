using StardewValley;
using StardewValley.Menus;

namespace Welcome.Anniversary;

/// <summary>Native dialogue UI with deferred transitions. Never nest afterQuestion callbacks.</summary>
internal sealed class NativeFestivalDialogs
{
    private IClickableMenu? owned;
    private Action? pending;
    private Action? cancelAnswer;
    internal bool OwnsCurrent => owned is not null && ReferenceEquals(owned, Game1.activeClickableMenu);
    private static bool Portraitless(NPC? npc)=>npc?.EventActor==true&&npc.modData.ContainsKey(AnniversaryContent.Portraitless);

    internal void Ask(string text, IEnumerable<Response> responses, Action<string> answer, NPC? speaker = null)
    {
        if(Portraitless(speaker)){text=speaker!.displayName+"："+text;speaker=null;}
        cancelAnswer=()=>answer("Cancel");
        Game1.currentLocation.createQuestionDialogue(text, responses.ToArray(),
            (_, response) => pending = () => answer(response), speaker);
        owned = Game1.activeClickableMenu;
    }

    internal void Say(string text,NPC? speaker=null)
    {
        cancelAnswer=null;
        if(Portraitless(speaker)){text=speaker!.displayName+"："+text;speaker=null;}
        if(speaker is null)Game1.drawObjectDialogue(text);
        // The string overload takes a translation asset key, not dialogue text.
        else {speaker.setNewDialogue(new Dialogue(speaker,null,text));Game1.drawDialogue(speaker);}
        owned = Game1.activeClickableMenu;
    }

    internal void Talk(NPC npc,Dialogue dialogue)
    {
        cancelAnswer=null;
        if(Portraitless(npc))
        {
            // Keep all dialogue pages, but don't let DialogueBox query NPC.Portrait.
            var plain=new Dialogue(dialogue){speaker=null,showPortrait=false};
            Game1.currentSpeaker=null;
            Game1.DrawDialogue(plain);
        }
        else {npc.setNewDialogue(dialogue);Game1.drawDialogue(npc);}
        owned=Game1.activeClickableMenu;
    }

    internal void Update()
    {
        // answerDialogue clears afterQuestion AFTER invoking it, and the old box has an outro.
        // Waiting for the box to close preserves both the new callback and the new menu.
        if (Game1.activeClickableMenu is not null) return;
        owned = null;
        Action? next = pending;
        pending = null;
        next?.Invoke();
    }

    internal void Cancel(bool notify=false)
    {
        pending = notify?cancelAnswer:null;
        cancelAnswer=null;
        if (OwnsCurrent)
        {
            Game1.currentLocation.afterQuestion = null;
            Game1.currentLocation.lastQuestionKey = null;
            Game1.objectDialoguePortraitPerson = null;
            Game1.exitActiveMenu();
            Game1.dialogueUp = false;
            Game1.player.forceCanMove();
        }
        owned = null;
    }
}
