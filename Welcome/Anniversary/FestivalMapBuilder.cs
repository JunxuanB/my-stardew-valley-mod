using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace Welcome.Anniversary;

/// <summary>The shipped layout also drives the offline preview; Town is never edited in place.</summary>
internal sealed class FestivalMapBuilder(IModHelper helper, FestivalLayout layout)
{
    internal const string Marker = "JunxuanB.Welcome/Anniversary";

    internal Map Build()
    {
        Map town = helper.GameContent.Load<Map>("Maps/Town");
        var map = new Map(town.Id);
        foreach (var property in town.Properties)
            map.Properties[property.Key] = property.Value;
        foreach (string property in new[] { "Warp", "Doors", "DayTiles", "NightTiles", "EntryAction", "OnWarp", "Music" })
            map.Properties.Remove(property);
        // Keep only the real BusStop exit. Walking off the west road uses the native
        // festival exit confirmation/readiness flow, rather than an invisible wall.
        map.Properties["Warp"] = "-1 53 BusStop 42 23 -1 54 BusStop 42 23 -1 55 BusStop 42 24";
        map.Properties[Marker] = "true";
        map.Properties["Outdoors"] = "T";
        map.Properties["indoorWater"] = "T";
        // Town seasonal updates address the vanilla sheets by ID, so preserve
        // those IDs before cloning any tiles into the festival map.
        foreach (TileSheet original in town.TileSheets)
            map.AddTileSheet(CloneSheet(original, map, original.Id));
        if(map.Properties.TryGetValue("Light",out var lights))
        {
            string[] parts=lights.ToString().Split(' ',StringSplitOptions.RemoveEmptyEntries);
            map.Properties["Light"]=string.Join(' ',Enumerable.Range(0,parts.Length/3)
                .Where(i=>!(parts[i*3]=="24"&&parts[i*3+1]=="50"))
                .SelectMany(i=>parts.Skip(i*3).Take(3)));
        }
        foreach (Layer original in town.Layers)
        {
            var layer = new Layer(original.Id, map, original.LayerSize, original.TileSize);
            map.AddLayer(layer);
            foreach (var property in original.Properties) layer.Properties[property.Key] = property.Value;
            for (int x = 0; x < original.LayerWidth; x++)
            for (int y = 0; y < original.LayerHeight; y++)
                layer.Tiles[x, y] = CloneTile(original.Tiles[x, y], layer, map);
        }
        foreach (LayoutClear clear in layout.Clear)
        foreach (string id in clear.Layers)
        {
            Layer? layer = map.GetLayer(id);
            if (layer is null) continue;
            for (int x = clear.Area[0]; x < clear.Area[0] + clear.Area[2]; x++)
            for (int y = clear.Area[1]; y < clear.Area[1] + clear.Area[3]; y++) layer.Tiles[x, y] = null;
        }
        foreach (LayoutCopy copy in layout.Copies)
        {
            Map source = helper.GameContent.Load<Map>(copy.Map);
            foreach (string id in copy.Layers)
            {
                Layer? from = source.GetLayer(id);
                Layer? to = map.GetLayer(id);
                if (from is null || to is null) continue;
                for (int x = 0; x < copy.Source[2]; x++)
                for (int y = 0; y < copy.Source[3]; y++)
                {
                    Tile? tile = from.Tiles[copy.Source[0] + x, copy.Source[1] + y];
                    if (tile is null || copy.FestivalOnly && !tile.TileSheet.ImageSource.Replace('\\', '/').Equals("Maps/Festivals", StringComparison.OrdinalIgnoreCase)) continue;
                    to.Tiles[copy.Target[0] + x, copy.Target[1] + y] = CloneTile(tile, to, map);
                }
            }
        }
        foreach (LayoutTile stamp in layout.Tiles)
        {
            Layer layer = map.GetLayer(stamp.Layer);
            string asset = ResolveTexture(stamp.Texture);
            TileSheet? sheet = map.TileSheets.FirstOrDefault(s => s.ImageSource == asset);
            if (sheet is null)
            {
                Texture2D texture = helper.GameContent.Load<Texture2D>(asset);
                sheet = new TileSheet("z_anniversary_" + map.TileSheets.Count, map, asset,
                    new(texture.Width / 16, texture.Height / 16), new(16, 16));
                map.AddTileSheet(sheet);
            }
            var tile = new StaticTile(layer, sheet, BlendMode.Alpha, stamp.Index);
            foreach (var property in stamp.Properties) tile.Properties[property.Key] = property.Value;
            layer.Tiles[stamp.At[0], stamp.At[1]] = tile;
        }
        if (map.GetLayer("Set-Up") is null)
        {
            Layer back = map.GetLayer("Back");
            map.AddLayer(new Layer("Set-Up", map, back.LayerSize, back.TileSize));
        }
        if (map.GetTileSheet("Town") is null)
            throw new InvalidOperationException("Anniversary map must preserve the vanilla 'Town' tile sheet ID.");
        map.LoadTileSheets(Game1.mapDisplayDevice);
        return map;
    }

    internal string ResolveTexture(string name) => name.StartsWith("assets/", StringComparison.Ordinal)
        ? helper.ModContent.GetInternalAssetName(name).Name : name;

    private static TileSheet Sheet(TileSheet original, Map map)
    {
        // Map IDs aren't unique across sources: match the image, not a reused sheet ID.
        TileSheet? existing = map.TileSheets.FirstOrDefault(s => s.ImageSource == original.ImageSource);
        if (existing is not null) return existing;
        TileSheet sheet = CloneSheet(original, map, "z_anniversary_" + map.TileSheets.Count);
        map.AddTileSheet(sheet);
        return sheet;
    }

    private static TileSheet CloneSheet(TileSheet original, Map map, string id)
    {
        var sheet = new TileSheet(id, map, original.ImageSource, original.SheetSize, original.TileSize)
        { Margin = original.Margin, Spacing = original.Spacing };
        foreach (var p in original.Properties) sheet.Properties[p.Key] = p.Value;
        for (int i = 0; i < original.TileCount; i++)
        foreach (var p in original.TileIndexProperties[i])
            if (p.Key is not "Action" and not "TouchAction") sheet.TileIndexProperties[i][p.Key] = p.Value;
        return sheet;
    }

    private static Tile? CloneTile(Tile? source, Layer layer, Map map)
    {
        if (source is null) return null;
        Tile result;
        if (source is AnimatedTile animated)
        {
            var frames = animated.TileFrames.Select(frame =>
            {
                var tile = new StaticTile(layer, Sheet(frame.TileSheet, map), frame.BlendMode, frame.TileIndex);
                foreach (var p in frame.Properties)
                    if (p.Key is not "Action" and not "TouchAction") tile.Properties[p.Key] = p.Value;
                return tile;
            }).ToArray();
            result = new AnimatedTile(layer, frames, animated.FrameInterval);
        }
        else result = new StaticTile(layer, Sheet(source.TileSheet, map), source.BlendMode, source.TileIndex);
        foreach (var p in source.Properties)
            if (p.Key is not "Action" and not "TouchAction") result.Properties[p.Key] = p.Value;
        return result;
    }
}
