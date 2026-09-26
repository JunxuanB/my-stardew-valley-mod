using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Objects;

namespace Welcome.Anniversary;

/// <summary>Local visual companions tied to nests, drawn inside the native world/light pass.</summary>
internal sealed class HomeXiaowai
{
    private static HomeXiaowai? instance;
    private static readonly Point[] directions=[new(0,1),new(1,0),new(-1,0),new(0,-1)];
    private readonly Random random=new();
    private readonly IInputHelper input;
    private readonly HashSet<string> lightIds=[];
    private readonly PerScreen<RoomState> rooms=new(()=>new());
    internal HomeXiaowai(IModHelper helper,Harmony harmony)
    {
        instance=this;input=helper.Input;
        helper.Events.GameLoop.UpdateTicked+=Update;
        helper.Events.Input.ButtonPressed+=Interact;
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>
        {
            foreach(string id in lightIds)Game1.currentLightSources?.Remove(id);
            lightIds.Clear();rooms.ResetAllScreens();
        };
        harmony.Patch(AccessTools.Method(typeof(GameLocation),nameof(GameLocation.draw),[typeof(SpriteBatch)]),
            postfix:new HarmonyMethod(typeof(HomeXiaowai),nameof(DrawInWorld)));
    }
    private static bool Indoors(GameLocation? location)=>location?.Map is not null&&!location.IsOutdoors&&!location.IsTemporary;
    private static bool Bedtime=>Game1.timeOfDay>=2100;
    private static Vector2 Bed(Companion pet)=>pet.Home+new Vector2(.5f,-.125f);
    private static Point Tile(Vector2 position)=>new((int)Math.Round(position.X),(int)Math.Round(position.Y));
    private void Remove(Companion pet)
    {Game1.currentLightSources?.Remove(pet.LightId);lightIds.Remove(pet.LightId);}
    private void Clear(RoomState room)
    {foreach(Companion pet in room.Pets.Values)Remove(pet);room.Pets.Clear();}
    private void Update(object? sender,UpdateTickedEventArgs e)
    {
        RoomState room=rooms.Value;
        GameLocation? location=Context.IsWorldReady?Game1.currentLocation:null;
        if(!ReferenceEquals(room.Location,location)){Clear(room);room.Location=location;}
        if(!Indoors(location)){Clear(room);return;}
        var nests=new HashSet<Furniture>(location!.furniture.Where(f=>f?.QualifiedItemId==RewardCatalog.Nest),ReferenceEqualityComparer.Instance);
        foreach(var old in room.Pets.Keys.Where(f=>!nests.Contains(f)).ToArray()){Remove(room.Pets[old]);room.Pets.Remove(old);}
        bool paused=Game1.activeClickableMenu is not null||Game1.eventUp||Game1.HostPaused||Game1.paused||Game1.fadeToBlack;
        float dt=Math.Min(.1f,(float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds);
        foreach(Furniture nest in nests)
        {
            if(!room.Pets.TryGetValue(nest,out Companion? pet)||pet.Home!=nest.TileLocation)
            {
                if(pet is not null)Remove(pet);
                pet=new Companion{Home=nest.TileLocation,Mode=2,Remaining=.6f};room.Pets[nest]=pet;
                // Entering a room after bedtime starts with Xiaowai already tucked in.
                Vector2? origin=Bedtime?null:SpawnPoint(location,pet.Home);
                if(origin is null)Sleep(pet);
                else pet.Position=pet.Target=pet.SegmentStart=origin.Value;
            }
            if(!paused)
            {
                if(Bedtime)UpdateNight(location,pet,dt);
                else UpdateDay(location,pet,dt);
            }
            if(Game1.eventUp)Remove(pet);else UpdateLight(location,pet);
        }
    }
    private void UpdateDay(GameLocation location,Companion pet,float dt)
    {
        if(pet.InNest)
        {
            if(SpawnPoint(location,pet.Home) is not { } free)return;
            pet.InNest=false;pet.Position=pet.Target=pet.SegmentStart=free;pet.Mode=2;pet.Pose=0;pet.Remaining=.6f;
        }
        pet.Returning=false;pet.EnteringNest=false;pet.Route.Clear();
        pet.Remaining-=dt;
        if(pet.Remaining<=0&&Vector2.Distance(pet.Position,pet.Target)<=.001f)
        {
            int choice=pet.ForceMove?60:random.Next(100);
            pet.FulfillForcedMove=pet.ForceMove;
            pet.Mode=choice<60?0:choice<90?1:2;pet.Remaining=8;
            pet.Target=pet.Position;pet.Frame=0;pet.Stride=0;
            pet.Pose=pet.Mode==0?(random.Next(2)==0?4:2):pet.Mode==2?random.Next(2):-1;
        }
        if(pet.Mode!=1)return;
        if(Vector2.Distance(pet.Position,pet.Target)>.001f)
        {
            if(!Walkable(location,Tile(pet.Target))){WaitOrRetreat(location,pet,dt);return;}
            pet.BlockedFor=0;
            Move(pet,dt);
            if(pet.FulfillForcedMove){pet.ForceMove=false;pet.FulfillForcedMove=false;}
            return;
        }
        Point grid=Tile(pet.Position);
        Point? next=directions.OrderBy(_=>random.Next()).Select(step=>grid+step)
            .Where(t=>Vector2.Distance(t.ToVector2(),pet.Home)<=5&&Walkable(location,t)).Select(t=>(Point?)t).FirstOrDefault();
        if(next.HasValue)SetTarget(pet,next.Value.ToVector2());
        else {pet.Pose=0;pet.Frame=0;pet.Stride=0;if(pet.ForceMove)pet.Remaining=Math.Min(pet.Remaining,.5f);}
    }
    private static void Sleep(Companion pet)
    {
        pet.Position=pet.Target=pet.SegmentStart=Bed(pet);pet.InNest=true;pet.Returning=false;pet.EnteringNest=false;
        pet.Pose=4;pet.Frame=0;pet.Stride=0;pet.Route.Clear();pet.ForceMove=false;pet.FulfillForcedMove=false;
    }
    private static void UpdateNight(GameLocation location,Companion pet,float dt)
    {
        if(pet.InNest)return;
        if(!pet.Returning)
        {pet.Returning=true;pet.Pose=-1;pet.Route.Clear();pet.Retry=0;pet.ForceMove=false;}
        if(pet.EnteringNest)
        {if(Move(pet,dt))Sleep(pet);return;}
        // Finish the current safe tile before choosing a path; never cut through furniture.
        if(Vector2.Distance(pet.Position,pet.Target)>.001f)
        {
            if(Walkable(location,Tile(pet.Target))){pet.BlockedFor=0;Move(pet,dt);}
            else WaitOrRetreat(location,pet,dt);
            return;
        }
        Point here=Tile(pet.Position);
        if(Docks(pet.Home).Contains(here))
        {pet.EnteringNest=true;SetTarget(pet,Bed(pet));return;}
        pet.Retry-=dt;
        if(pet.Route.Count==0&&pet.Retry<=0)
        {pet.Route=RouteHome(location,here,pet.Home);pet.Retry=1;}
        if(pet.Route.TryDequeue(out Point next))
        {
            if(Walkable(location,next))SetTarget(pet,next.ToVector2());
            else {pet.Route.Clear();pet.Pose=0;}
        }
        else pet.Pose=0; // If the player blocks the route, wait and retry instead of warping.
    }
    private static void SetTarget(Companion pet,Vector2 target)
    {pet.SegmentStart=pet.Position;pet.Target=target;pet.BlockedFor=0;pet.Pose=-1;}
    private static void WaitOrRetreat(GameLocation room,Companion pet,float dt)
    {
        pet.Pose=0;pet.Route.Clear();pet.BlockedFor+=dt;
        if(pet.BlockedFor<1||!Walkable(room,Tile(pet.SegmentStart)))return;
        // Retrace just this segment before planning again, without crossing the new obstacle.
        Vector2 previous=pet.SegmentStart;SetTarget(pet,previous);
    }
    private static bool Move(Companion pet,float dt)
    {
        Vector2 delta=pet.Target-pet.Position;
        float distance=delta.Length();
        if(distance<=.001f){pet.Position=pet.Target;return true;}
        delta/=distance;float step=Math.Min(distance,.75f*dt);pet.Position+=delta*step;
        pet.Row=Math.Abs(delta.X)>Math.Abs(delta.Y)?delta.X>0?1:3:delta.Y>0?0:2;
        pet.Stride+=step;pet.Frame=XiaowaiArt.WalkFrame(pet.Stride,pet.Row);pet.Pose=-1;
        if(step>=distance){pet.Position=pet.Target;return true;}return false;
    }
    private static IEnumerable<Point> Docks(Vector2 home)
    {
        Point at=Tile(home);
        yield return at+new Point(0,1);yield return at+new Point(1,1);
        yield return at+new Point(-1,0);yield return at+new Point(2,0);
    }
    private static Queue<Point> RouteHome(GameLocation room,Point start,Vector2 home)
    {
        var goals=Docks(home).Where(p=>Walkable(room,p)).ToHashSet();
        if(goals.Count==0)return new Queue<Point>();
        var previous=new Dictionary<Point,Point>{{start,start}};var queue=new Queue<Point>();queue.Enqueue(start);
        while(queue.TryDequeue(out Point tile)&&previous.Count<4096)
        {
            if(goals.Contains(tile))
            {
                var steps=new Stack<Point>();
                while(tile!=start){steps.Push(tile);tile=previous[tile];}
                return new Queue<Point>(steps);
            }
            foreach(Point step in directions)
            {
                Point next=tile+step;
                if(previous.ContainsKey(next)||!Walkable(room,next))continue;
                previous[next]=tile;queue.Enqueue(next);
            }
        }
        return new Queue<Point>();
    }
    private static bool Floor(GameLocation room,Point tile)
    {
        if(!room.isTileOnMap(tile))return false;
        var back=room.Map.GetLayer("Back");
        return back?.Tiles[tile.X,tile.Y] is not null&&room.isTilePassable(tile.ToVector2())
            &&!room.isWaterTile(tile.X,tile.Y);
    }
    private static bool SafeFloor(GameLocation room,Point tile)
    {
        if(!Floor(room,tile)||room.warps.Any(w=>w.X==tile.X&&w.Y==tile.Y))return false;
        // Keep a tile away from static walls/map voids, not from movable furniture.
        if(directions.Any(step=>!Floor(room,tile+step)))return false;
        foreach(string layerName in new[]{"Front","AlwaysFront"})
        {
            var layer=room.Map.GetLayer(layerName);
            if(layer is not null&&layer.Tiles[tile.X,tile.Y] is not null)return false;
        }
        return true;
    }
    private static bool Walkable(GameLocation room,Point tile)=>SafeFloor(room,tile)&&!room.IsTileOccupiedBy(tile.ToVector2());
    private static Vector2? SpawnPoint(GameLocation room,Vector2 origin)
    {
        // Only step out beside the nest; searching farther could spawn behind a wall
        // or inside another room with no route back to this nest.
        foreach(Point tile in Docks(origin))if(Walkable(room,tile))return tile.ToVector2();
        return null;
    }
    private void UpdateLight(GameLocation room,Companion pet)
    {
        pet.Light??=new LightSource(pet.LightId,LightSource.lantern,Vector2.Zero,1.25f,Color.Black,
            LightSource.LightContext.None,0,room.NameOrUniqueName);
        pet.Light.position.Value=pet.Position*64+new Vector2(32,0);
        Game1.currentLightSources[pet.LightId]=pet.Light;lightIds.Add(pet.LightId);
    }
    private void Interact(object? sender,ButtonPressedEventArgs e)
    {
        if(!Context.IsWorldReady||!Indoors(Game1.currentLocation)||!ReferenceEquals(rooms.Value.Location,Game1.currentLocation)
            ||Game1.activeClickableMenu is not null||Game1.eventUp||Game1.fadeToBlack
            ||Game1.player.UsingTool||Game1.player.isEating||!e.Button.IsActionButton())return;
        Vector2 point=e.Button==SButton.MouseRight?e.Cursor.AbsolutePixels:Game1.player.GetGrabTile()*64+new Vector2(32,32);
        foreach(Companion pet in rooms.Value.Pets.Values)
        {
            bool pose=pet.Pose>=0;
            Rectangle bounds=new((int)(pet.Position.X*64)-(pose?48:16),(int)(pet.Position.Y*64)-(pose?96:64),pose?160:96,pose?160:128);
            if(!bounds.Contains(point.ToPoint())||Vector2.Distance(Game1.player.Tile,pet.Position)>2.5f)continue;
            input.Suppress(e.Button);
            // Bedtime takes priority; clicking doesn't send the sleeper wandering again.
            if(Bedtime){Game1.playSound("smallSelect");break;}
            pet.ForceMove=true;
            if(pet.Mode!=1){pet.Remaining=Math.Min(pet.Remaining,.6f);pet.Pose=3;}
            Game1.playSound("smallSelect");break;
        }
    }
    private static void DrawInWorld(GameLocation __instance,SpriteBatch b)=>instance?.Draw(__instance,b);
    private void Draw(GameLocation location,SpriteBatch b)
    {
        RoomState room=rooms.Value;
        if(!Context.IsWorldReady||Game1.eventUp||!Indoors(location)||!ReferenceEquals(location,Game1.currentLocation)||!ReferenceEquals(location,room.Location))return;
        foreach(Companion pet in room.Pets.Values)
        {
            bool pose=pet.Pose>=0;
            Rectangle source=pose?new Rectangle(pet.Pose*XiaowaiArt.PoseSize,0,XiaowaiArt.PoseSize,XiaowaiArt.PoseSize):XiaowaiArt.Frame(pet.Frame,pet.Row);
            Vector2 at=Game1.GlobalToLocal(pet.Position*64+new Vector2(pose?-48:-16,pose?-96:-64));
            float depth=(pet.InNest?(pet.Home.Y+1)*64+1:(pet.Position.Y+1)*64)/10000f;
            b.Draw(pose?XiaowaiArt.Poses:XiaowaiArt.Walk,at,source,Color.White,0,Vector2.Zero,XiaowaiArt.DrawScale,SpriteEffects.None,depth);
        }
    }
    private sealed class RoomState
    {
        internal GameLocation? Location;
        internal readonly Dictionary<Furniture,Companion> Pets=new(ReferenceEqualityComparer.Instance);
    }
    private sealed class Companion
    {
        internal Vector2 Home,Position,Target,SegmentStart;
        internal int Row,Frame,Pose,Mode;
        internal float Stride,Remaining,Retry,BlockedFor;
        internal bool ForceMove,FulfillForcedMove,Returning,EnteringNest,InNest;
        internal Queue<Point> Route=new();
        internal readonly string LightId=AnniversaryContent.ModId+"/HomeXiaowai/"+Guid.NewGuid().ToString("N");
        internal LightSource? Light;
    }
}
