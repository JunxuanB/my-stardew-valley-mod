using Microsoft.Xna.Framework;

namespace Welcome.Anniversary;

internal sealed class SurveyObjective
{
    public string Id { get; set; }="";
    public string Name { get; set; }="";
    public string Npc { get; set; }="";
    public int[] Area { get; set; }=[];
    public string ClueA { get; set; }="";
    public string ClueB { get; set; }="";
    public string Observation { get; set; }="";
    internal Rectangle Bounds=>new(Area[0],Area[1],Area[2],Area[3]);
}

/// <summary>Shared map/quest definitions keep clickable props separate from nearby NPCs.</summary>
internal sealed class WeddingSurvey(FestivalLayout layout)
{
    internal IReadOnlyList<SurveyObjective> Targets {get;}=layout.SurveyTargets.Where(t=>t.Npc is not ("Pierre" or "Emily")).ToArray();
    internal SurveyObjective? Find(string id)=>Targets.FirstOrDefault(t=>t.Id==id);
    internal SurveyObjective? ForNpc(string name)
    {
        if(name is "Pierre" or "Emily")return null;
        SurveyObjective? known=Targets.FirstOrDefault(t=>t.Npc==name);
        if(known is not null)return known;
        LayoutActor? actor=layout.Actors.FirstOrDefault(a=>a.Name==name||a.Name=="Xiaowai"&&name==AnniversaryContent.Xiaowai);
        return actor is null?null:new SurveyObjective{Id="npc:"+name,Name=name,Npc=name,Area=[actor.At[0],actor.At[1],1,1]};
    }
    internal SurveyObjective? Decoration(Point tile)=>Targets.FirstOrDefault(t=>t.Npc==""&&t.Bounds.Contains(tile));
    internal SurveyObjective? Current(FestivalActivity game)=>game.Round>=0&&game.Round<game.SurveyObjectives.Length?Find(game.SurveyObjectives[game.Round]):null;
    internal static bool InReach(SurveyObjective target,Point player,Point clicked)
    {
        int distance=Math.Abs(player.X-clicked.X)+Math.Abs(player.Y-clicked.Y);
        if(target.Npc=="")return target.Bounds.Contains(clicked)&&distance<=3;
        // Guests can stroll by up to two tiles. Validate the clicked actor near its
        // registered home, not a different peer's independent visual NPC instance.
        return distance<=2&&Math.Abs(clicked.X-target.Area[0])+Math.Abs(clicked.Y-target.Area[1])<=3;
    }
}
