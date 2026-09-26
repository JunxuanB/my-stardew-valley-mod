namespace Welcome.Anniversary;

internal sealed class FestivalLayout
{
    public int[] Entry { get; set; } = [1, 54];
    public int[] Exit { get; set; } = [0, 53, 1, 3];
    public int[] Pond { get; set; } = [36, 76];
    public List<int[]> ClearBushes { get; set; } = [];
    public List<int[]> ClearTrees { get; set; } = [];
    public List<LayoutClear> Clear { get; set; } = [];
    public List<LayoutCopy> Copies { get; set; } = [];
    public List<LayoutTile> Tiles { get; set; } = [];
    public List<LayoutSprite> Sprites { get; set; } = [];
    public List<LayoutActor> Actors { get; set; } = [];
    public List<LayoutInteraction> Interactions { get; set; } = [];
    public List<SurveyObjective> SurveyTargets { get; set; } = [];
}
internal sealed class LayoutClear
{
    public int[] Area { get; set; } = [];
    public string[] Layers { get; set; } = [];
}
internal sealed class LayoutCopy
{
    public string Map { get; set; } = "";
    public int[] Source { get; set; } = [];
    public int[] Target { get; set; } = [];
    public string[] Layers { get; set; } = [];
    public bool FestivalOnly { get; set; }
}
internal sealed class LayoutTile
{
    public string Layer { get; set; } = "Buildings";
    public int[] At { get; set; } = [];
    public string Texture { get; set; } = "";
    public int Index { get; set; }
    public Dictionary<string, string> Properties { get; set; } = [];
}
internal sealed class LayoutSprite
{
    public string Texture { get; set; } = "";
    public int[] Source { get; set; } = [];
    public int[] At { get; set; } = [];
    public float Scale { get; set; } = 1;
    public float? SortY { get; set; }
}
internal sealed class LayoutActor
{
    public string Name { get; set; } = "";
    public string Dialogue { get; set; } = "";
    public int[] At { get; set; } = [];
    public int Facing { get; set; } = 2;
}
internal sealed class LayoutInteraction
{
    public string Id { get; set; } = "";
    public int[] Area { get; set; } = [];
}
