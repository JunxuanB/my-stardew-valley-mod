using StardewValley;

namespace Welcome.Anniversary;

/// <summary>Inventory-only exchanges; wearing glasses never silently gives away the hat.</summary>
internal static class GlassesExchange
{
    internal static bool Carries(Farmer who,string id)=>Find(who,id)>=0;
    private static int Find(Farmer who,string id)
    {
        for(int i=0;i<who.Items.Count;i++)
            if(who.Items[i]?.QualifiedItemId==id&&who.Items[i]!.Stack>0)return i;
        return -1;
    }
    internal static string? Repair(Farmer who)
    {
        int broken=Find(who,RewardCatalog.BrokenGlasses);
        if(broken<0)return "先把破损的眼镜放进随身背包，再交给我修吧。";
        Item original=who.Items[broken]!;
        int destination=original.Stack==1?broken:Enumerable.Range(0,who.Items.Count).FirstOrDefault(i=>who.Items[i] is null,-1);
        if(destination<0)return "修好的眼镜需要一个空格。背包腾出一格再来，材料没有扣除。";
        Item repaired=ItemRegistry.Create(RewardCatalog.Glasses);
        // A single broken pair frees its own slot, even with an otherwise full backpack.
        if(original.Stack>1)original.Stack--;
        who.Items[destination]=repaired;
        return null;
    }
    internal static bool Give(Farmer who)
    {
        int slot=Find(who,RewardCatalog.Glasses);if(slot<0)return false;
        if(who.Items[slot]!.Stack>1)who.Items[slot]!.Stack--;
        else who.Items[slot]=null;
        return true;
    }
}
