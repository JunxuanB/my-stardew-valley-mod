namespace Welcome.Anniversary;

internal sealed class FestivalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Session { get; set; } = "";
    public string Action { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Game { get; set; } = "";
    public string Visit { get; set; } = "";
    public string Map { get; set; } = "";
    public int Date { get; set; }
    public long Target { get; set; }
    public int Round { get; set; }
    public int Choice { get; set; }
    public float Value { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Facing { get; set; }=2;
    public int ClickedX { get; set; }
    public int ClickedY { get; set; }
}
internal sealed class FestivalReply
{
    public string Request { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Success { get; set; }
    public string Speaker { get; set; } = "";
}
internal sealed class FestivalPlayer
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Visit { get; set; } = "";
    public bool Present { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Facing { get; set; }=2;
    public int Bouquets { get; set; }
    public int Fishing { get; set; }
    public string NextCatch { get; set; } = "";
    public int CatchTicket { get; set; }
    public int SlimeWins { get; set; }
    public int Strawberries { get; set; }
    public int Kisses { get; set; }
    public int LinusGifts { get; set; }
    public int EvelynGifts { get; set; }
    public Dictionary<string, int> Purchases { get; set; } = [];
}
internal sealed class FestivalActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "";
    public string Phase { get; set; } = "Invite";
    public long A { get; set; }
    public long B { get; set; }
    public int Round { get; set; }
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public double Started { get; set; }
    public double Deadline { get; set; }
    public int[] Questions { get; set; } = [];
    public string[] SurveyObjectives { get; set; } = [];
    public int Question { get; set; }
    public float? FirstCut { get; set; }
    public long? FirstCutter { get; set; }
    public float? SecondCut { get; set; }
    public long? SecondCutter { get; set; }
    public int CakeReward { get; set; }
    public HashSet<long> Reviewed { get; set; } = [];
    public int Heart { get; set; }
    public List<FestivalTarget> Targets { get; set; } = [];
    public List<long> Submitted { get; set; } = [];
    public string Result { get; set; } = "";
    public bool Involves(long id) => A == id || B == id;
    public bool NeedsPlayer(long id) => Involves(id)&&!(Kind=="Cake"&&Phase=="Result"&&Reviewed.Contains(id));
}
internal sealed class FestivalTarget
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public long? Owner { get; set; }
}
internal sealed class FestivalNpcMotion
{
    public string Name { get; set; }="";
    public float FromX { get; set; }
    public float FromY { get; set; }
    public float ToX { get; set; }
    public float ToY { get; set; }
    public double Started { get; set; }
    public double Duration { get; set; }
    public int Facing { get; set; }=2;
    public double PausedAt { get; set; }
}
internal sealed class FestivalSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long Revision { get; set; }
    public int Date { get; set; }
    public double Clock { get; set; }
    public double BerryRemaining { get; set; }
    public bool BerryReady { get; set; }
    public List<FestivalPlayer> Players { get; set; } = [];
    public List<FestivalActivity> Activities { get; set; } = [];
    public List<FestivalNpcMotion> Npcs { get; set; } = [];
}

internal sealed record EarthQuestion(string Question, string[] Answers);
internal static class EarthSurvey
{
    internal static readonly EarthQuestion[] Questions =
    [
        new("地球调查 01：人类说爱无法看见。你会先去哪里找？", ["一起吃饭的桌边", "等人回家的灯下", "吵架后递来的水里", "暂时找不到，但继续调查"]),
        new("地球调查 02：猫似乎很适合统治地球。你愿意担任什么职位？", ["罐头供应员", "纸箱建筑师", "太阳光管理员", "拒绝上班的另一只猫"]),
        new("地球调查 03：如果周一偷偷降落，你会先观察哪里？", ["早餐摊", "地铁站", "还亮着灯的卧室", "学校门口"]),
        new("地球调查 04：大人大人大人——这是跳跃的人。什么最能让你跳起来？", ["见到想见的人", "突然发工资", "抓到一条大鱼", "脚下有一只虫子"]),
        new("地球调查 05：人类把动物尸块裹上外壳，称作疯狂星期祀。你会带什么？", ["两份炸鸡", "一杯冰饮", "纸巾和湿巾", "完整的调查报告"]),
        new("地球调查 06：明知自己渺小，人类还是很勇敢。你会从哪件事开始？", ["先说喜欢你", "再试一次", "承认我也会害怕", "牵住旁边的手"]),
        new("地球调查 07：要带一件地球纪念品回去，你选哪件？", ["一张合影", "一袋种子", "一只毛绒玩具", "一张写满字的纸条"]),
        new("地球调查 08：两个人一起发呆，也算活动吗？", ["当然，观察云", "当然，观察彼此", "要有零食才算", "这是最高级的调查"]),
        new("地球调查 09：如果把今天装进瓶子，里面最应该是什么？", ["花香", "笑声", "蛋糕屑", "晚上的一点星光"]),
        new("地球调查 10：人类下雨时会挤在一把伞下，你认为原因是？", ["只带了一把伞", "想离对方近一点", "另一把伞借给猫了", "需要共同抵抗天气"]),
        new("地球调查 11：小外忘了来地球的目的，你建议它先做什么？", ["睡一觉", "吃点东西", "和喜欢的人散步", "忘了也没关系"]),
        new("地球调查 12：如果今晚只留下一句话，你会说？", ["今天很开心", "明天也一起吧", "路上小心", "我把蛋糕留给你了"])
    ];
    internal static string Name(string kind) => kind switch
    {
        "Cake" => "双人切蛋糕", "Quiz" => "小外的地球调查", "Survey" => "外星婚礼调查",
        "Slime" => "爱心史莱姆", "Kiss" => "接吻", "Photo" => "花拱合影", _ => kind
    };
}
