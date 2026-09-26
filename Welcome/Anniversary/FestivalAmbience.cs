using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Welcome.Anniversary;

/// <summary>Host-planned movement by temperament, plus Clint's original hammer animation.</summary>
internal sealed class FestivalAmbience(FestivalLayout layout,FestivalSession session)
{
    private readonly Random random=new();
    private readonly Dictionary<string,double> nextAction=[];
    private int[]? hammerFrames;
    private bool checkedHammer;
    private long lastHammerBeat=-1;
    private static readonly Point[] directions=[new(0,-1),new(1,0),new(0,1),new(-1,0)];
    internal void Update()
    {
        Event? festival=Game1.CurrentEvent;if(festival is null||session.View is not { } view)return;
        double now=session.ServerNow;
        if(!checkedHammer)
        {
            checkedHammer=true;
            if(DataLoader.AnimationDescriptions(Game1.content).TryGetValue("clint_hammer",out string? description))
            {
                string[] parts=description.Split('/');
                if(parts.Length>1)
                {
                    int[] frames=parts[1].Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(s=>int.TryParse(s,out int n)?n:-1).ToArray();
                    if(frames.Length>0&&frames.All(n=>n>=0))hammerFrames=frames;
                }
            }
        }
        foreach(LayoutActor placement in layout.Actors)
        {
            string name=placement.Name=="Xiaowai"?AnniversaryContent.Xiaowai:placement.Name;
            NPC? npc=festival.getActorByName(name);if(npc is null)continue;
            FestivalNpcMotion? motion=view.Npcs.FirstOrDefault(n=>n.Name==name);
            if(motion is null&&Context.IsMainPlayer)
            {
                motion=new FestivalNpcMotion{Name=name,FromX=npc.Position.X,FromY=npc.Position.Y,ToX=npc.Position.X,ToY=npc.Position.Y,Facing=npc.FacingDirection};
                view.Npcs.Add(motion);
                nextAction[name]=now+1+random.NextDouble()*7;
            }
            if(motion is null)continue;
            if(placement.Name is "Pierre" or "Emily" or "Xiaowai" or "Clint")
            {
                int facing=placement.Name=="Clint"?1:2;
                motion.FromX=motion.ToX=placement.At[0]*64;motion.FromY=motion.ToY=placement.At[1]*64;
                motion.Duration=0;motion.PausedAt=0;motion.Facing=facing;
                npc.Position=new(motion.ToX,motion.ToY);npc.faceDirection(facing);npc.Sprite.StopAnimation();
                npc.Sprite.CurrentFrame=facing==1?4:0;npc.Sprite.UpdateSourceRect();
                if(placement.Name=="Clint")HammerSound(npc,now);
                continue;
            }
            float Progress()=>motion.Duration<=0?1:Math.Clamp((float)(((motion.PausedAt>0?motion.PausedAt:now)-motion.Started)/motion.Duration),0,1);
            Vector2 Position()=>Vector2.Lerp(new(motion.FromX,motion.FromY),new(motion.ToX,motion.ToY),Progress());
            if(Context.IsMainPlayer)
            {
                Vector2 current=Position();
                bool nearby=view.Players.Any(p=>p.Present&&Vector2.Distance(new Vector2(p.X,p.Y)*64,current)<150);
                if(Progress()<1)
                {
                    if(nearby&&motion.PausedAt<=0)motion.PausedAt=now;
                    else if(!nearby&&motion.PausedAt>0){motion.Started+=now-motion.PausedAt;motion.PausedAt=0;}
                }
                else if(now>=nextAction.GetValueOrDefault(name))
                {
                    bool anchored=placement.Name is "Pierre" or "Emily" or "Gus" or "Lewis" or "Willy" or "Xiaowai" or "Clint";
                    // Reserved personalities and guests lacking native poses stay put.
                    bool still=placement.Name is "Wizard" or "George" or "Mister Qi" or "Birdie"
                        or "Marlon" or "Gunther" or "Governor" or "Bouncer" or "Old Mariner";
                    bool lively=placement.Name is "Abigail" or "Sam" or "Alex" or "Haley" or "Jas" or "Vincent" or "Leo";
                    bool quiet=placement.Name is "Sebastian" or "Shane" or "Kent" or "Linus" or "Harvey" or "Elliott";
                    nextAction[name]=now+(lively?7:quiet?24:14)+random.NextDouble()*(lively?7:14);
                    bool walked=false;
                    if(!still&&!anchored&&!nearby&&random.Next(100)<(lively?70:quiet?25:45))
                    {
                        Point origin=new((int)Math.Round(current.X/64),(int)Math.Round(current.Y/64));
                        foreach(Point step in directions.OrderBy(_=>random.Next()))
                        {
                            Point tile=origin+step;
                            if(!SafeStep(tile,placement,motion,view))continue;
                            Vector2 delta=tile.ToVector2()*64-current;
                            motion.FromX=current.X;motion.FromY=current.Y;motion.ToX=tile.X*64;motion.ToY=tile.Y*64;
                            motion.Started=now;motion.Duration=delta.Length()/32;motion.PausedAt=0;
                            motion.Facing=step.X>0?1:step.X<0?3:step.Y>0?2:0;
                            walked=true;break;
                        }
                    }
                    if(!walked&&!still&&placement.Name!="Clint"&&!nearby&&random.Next(100)<(quiet?20:50))
                        motion.Facing=Enumerable.Range(0,4).Where(d=>d!=motion.Facing).OrderBy(_=>random.Next()).First();
                }
            }
            float progress=Progress();npc.Position=Position();
            npc.faceDirection(motion.Facing);
            if(progress<1&&motion.Duration>0&&motion.PausedAt<=0)
            {
                int row=motion.Facing==2?0:motion.Facing==1?1:motion.Facing==0?2:3;
                npc.Sprite.CurrentFrame=row*4+(int)(Math.Max(0,now-motion.Started)*6)%4;npc.Sprite.UpdateSourceRect();
            }
            else npc.Sprite.StopAnimation();

        }
    }
    internal bool DrawActor(NPC npc,SpriteBatch batch,float alpha)
    {
        if(npc.Name!="Clint"||hammerFrames is null)return false;
        if(npc.IsInvisible)return true;
        int step=(int)(session.ServerNow/.1%hammerFrames.Length);
        int frame=hammerFrames[step];
        Texture2D texture=npc.Sprite.Texture;
        int columns=texture.Width/32;
        if(columns<1||frame>=columns*(texture.Height/32))return false;
        Rectangle source=new(frame%columns*32,frame/columns*32,32,32);
        // Render the real wide hammer frames without changing NPC geometry or relying
        // on sprite state that the native event update can reset between frames.
        Vector2 at=Game1.GlobalToLocal(npc.Position+new Vector2(0,npc.GetBoundingBox().Height/2-32*3));
        batch.Draw(texture,at,source,Color.White*alpha,0,Vector2.Zero,4,SpriteEffects.None,Math.Max(0,npc.StandingPixel.Y/10000f));
        return true;
    }
    private void HammerSound(NPC npc,double now)
    {
        if(hammerFrames is null)return;
        long beat=(long)(now/.1);
        if(hammerFrames[(int)(beat%hammerFrames.Length)]!=9||beat==lastHammerBeat)return;
        lastHammerBeat=beat;
        if(Game1.activeClickableMenu is null&&Vector2.Distance(Game1.player.Position,npc.Position)<8*64)
            Game1.currentLocation.localSound("hammer");
    }

    private bool SafeStep(Point tile,LayoutActor home,FestivalNpcMotion motion,FestivalSnapshot view)
    {
        GameLocation room=Game1.currentLocation;
        // Only one tile from the assigned spot, with the contest floor and service areas clear.
        if(Math.Abs(tile.X-home.At[0])>1||Math.Abs(tile.Y-home.At[1])>1
            ||!room.isTileOnMap(tile)||!room.isTilePassable(tile.ToVector2())||room.isWaterTile(tile.X,tile.Y)
            ||room.IsTileOccupiedBy(tile.ToVector2())||new Rectangle(26,65,6,6).Contains(tile)
            ||new Rectangle(43,60,5,3).Contains(tile))return false;
        if(layout.Interactions.Any(i=>new Rectangle(i.Area[0],i.Area[1],i.Area[2],i.Area[3]).Contains(tile)))return false;
        if(room.Map.GetLayer("Back")?.Tiles[tile.X,tile.Y] is null)return false;
        foreach(string layerName in new[]{"Front","AlwaysFront"})
        {
            var layer=room.Map.GetLayer(layerName);
            if(layer is null)continue;
            if(layer.Tiles[tile.X,tile.Y] is not null||tile.Y>0&&layer.Tiles[tile.X,tile.Y-1] is not null)return false;
        }
        Vector2 destination=tile.ToVector2()*64;
        if(view.Npcs.Any(n=>n!=motion&&(Vector2.Distance(destination,new(n.ToX,n.ToY))<80
            ||Vector2.Distance(destination,new(n.FromX,n.FromY))<80)))return false;
        return !view.Players.Any(p=>p.Present&&Vector2.Distance(tile.ToVector2(),new(p.X,p.Y))<1.8f);
    }
}
