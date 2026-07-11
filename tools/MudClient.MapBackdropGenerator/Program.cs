using System.IO.Compression;
using System.Text.Json;

const int pixelsPerUnit = 2;
const int padding = 8;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: MudClient.MapBackdropGenerator <world-map.json> <output-directory>");
    return 1;
}

var worldPath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outputDirectory);

using var document = JsonDocument.Parse(await File.ReadAllTextAsync(worldPath));
var layers = new List<Layer>();
var roomsById = new Dictionary<int, Room>();

foreach (var area in document.RootElement.GetProperty("areas").EnumerateArray())
{
    var areaId = area.GetProperty("id").GetInt32();
    var areaRooms = new List<Room>();
    foreach (var rawRoom in area.GetProperty("rooms").EnumerateArray())
    {
        var coordinates = rawRoom.GetProperty("coordinates");
        var room = new Room(
            rawRoom.GetProperty("id").GetInt32(),
            areaId,
            coordinates[0].GetDouble(),
            coordinates[1].GetDouble(),
            coordinates[2].GetDouble(),
            GetSector(rawRoom),
            GetName(rawRoom),
            GetExitIds(rawRoom));
        areaRooms.Add(room);
        roomsById[room.Id] = room;
    }

    foreach (var group in areaRooms.GroupBy(room => room.Z))
    {
        if (group.Count() >= 20)
        {
            layers.Add(new Layer(areaId, group.Key, group.ToList()));
        }
    }
}

var manifest = new List<ManifestEntry>();
foreach (var layer in layers)
{
    var entry = RenderLayer(layer, roomsById, outputDirectory, pixelsPerUnit, padding);
    manifest.Add(entry);
    Console.WriteLine($"Rendered area {layer.AreaId}, Z {layer.Z}: {entry.Width}x{entry.Height}");
}

var manifestOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, "manifest.json"),
    JsonSerializer.Serialize(manifest, manifestOptions));
return 0;

static ManifestEntry RenderLayer(
    Layer layer,
    IReadOnlyDictionary<int, Room> roomsById,
    string outputDirectory,
    int scale,
    int mapPadding)
{
    var minX = (int)Math.Floor(layer.Rooms.Min(room => room.X)) - mapPadding;
    var maxX = (int)Math.Ceiling(layer.Rooms.Max(room => room.X)) + mapPadding;
    var minY = (int)Math.Floor(layer.Rooms.Min(room => room.Y)) - mapPadding;
    var maxY = (int)Math.Ceiling(layer.Rooms.Max(room => room.Y)) + mapPadding;
    var gridWidth = maxX - minX + 1;
    var gridHeight = maxY - minY + 1;
    var sectors = Enumerable.Repeat(-1, gridWidth * gridHeight).ToArray();
    var queue = new Queue<int>();

    var biomeSeeds = layer.Z >= 0 && layer.AreaId is 1 or 2
        ? layer.Rooms.Where(room => Palette.IndexFor(room.Sector, room.Name) != Palette.UndergroundIndex).ToList()
        : layer.Rooms;
    if (biomeSeeds.Count == 0)
    {
        biomeSeeds = layer.Rooms;
    }

    foreach (var room in biomeSeeds)
    {
        var x = Math.Clamp((int)Math.Round(room.X) - minX, 0, gridWidth - 1);
        var y = Math.Clamp(maxY - (int)Math.Round(room.Y), 0, gridHeight - 1);
        var index = y * gridWidth + x;
        sectors[index] = Palette.IndexFor(room.Sector, room.Name);
        queue.Enqueue(index);
    }

    while (queue.TryDequeue(out var index))
    {
        var x = index % gridWidth;
        var y = index / gridWidth;
        Spread(index - 1, x > 0);
        Spread(index + 1, x + 1 < gridWidth);
        Spread(index - gridWidth, y > 0);
        Spread(index + gridWidth, y + 1 < gridHeight);

        void Spread(int target, bool valid)
        {
            if (valid && sectors[target] < 0)
            {
                sectors[target] = sectors[index];
                queue.Enqueue(target);
            }
        }
    }

    var width = gridWidth * scale;
    var height = gridHeight * scale;
    var pixels = new byte[width * height * 4];
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var color = Palette.Colors[sectors[(y / scale) * gridWidth + x / scale]];
            var noise = HashNoise(x, y) - 8;
            SetPixel(pixels, width, x, y,
                Clamp(color.R + noise), Clamp(color.G + noise), Clamp(color.B + noise), 255);
        }
    }

    BoxBlur(pixels, width, height, radius: 7);

    var overlay = new byte[pixels.Length];
    foreach (var room in layer.Rooms)
    {
        var fromX = (int)Math.Round((room.X - minX) * scale);
        var fromY = (int)Math.Round((maxY - room.Y) * scale);
        foreach (var exitId in room.ExitIds)
        {
            if (!roomsById.TryGetValue(exitId, out var target) ||
                target.AreaId != layer.AreaId || target.Z != layer.Z || room.Id >= target.Id)
            {
                continue;
            }

            var toX = (int)Math.Round((target.X - minX) * scale);
            var toY = (int)Math.Round((maxY - target.Y) * scale);
            DrawLine(overlay, width, height, fromX, fromY, toX, toY, 118, 112, 94, 235);
        }
    }

    foreach (var room in layer.Rooms)
    {
        var x = (int)Math.Round((room.X - minX) * scale);
        var y = (int)Math.Round((maxY - room.Y) * scale);
        var color = Palette.MarkerFor(room.Sector, room.Name);
        FillRect(overlay, width, height, x - 1, y - 1, 3, 3, color.R, color.G, color.B, 255);
    }

    var zName = layer.Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture).Replace('-', 'm').Replace('.', '_');
    var fileName = $"area-{layer.AreaId}-z-{zName}-terrain.png";
    var overviewFileName = $"area-{layer.AreaId}-z-{zName}-rooms.png";
    PngWriter.Write(Path.Combine(outputDirectory, fileName), width, height, pixels);
    PngWriter.Write(Path.Combine(outputDirectory, overviewFileName), width, height, overlay);
    return new ManifestEntry(layer.AreaId, layer.Z, minX, minY, maxX, maxY, scale, width, height, fileName, overviewFileName);
}

static string GetSector(JsonElement room)
{
    if (room.TryGetProperty("userData", out var userData) &&
        userData.ValueKind == JsonValueKind.Object &&
        userData.TryGetProperty("sector", out var sector))
    {
        return sector.GetString() ?? string.Empty;
    }

    return string.Empty;
}

static string GetName(JsonElement room) =>
    room.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
        ? name.GetString() ?? string.Empty
        : string.Empty;

static int[] GetExitIds(JsonElement room) => room.TryGetProperty("exits", out var exits)
    ? exits.EnumerateArray().Select(exit => exit.GetProperty("exitId").GetInt32()).ToArray()
    : [];

static int HashNoise(int x, int y)
{
    unchecked
    {
        var hash = x * 374761393 + y * 668265263;
        hash = (hash ^ (hash >> 13)) * 1274126177;
        return (hash ^ (hash >> 16)) & 15;
    }
}

static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);

static void BoxBlur(byte[] pixels, int width, int height, int radius)
{
    var source = (byte[])pixels.Clone();
    var diameter = radius * 2 + 1;
    for (var y = 0; y < height; y++)
    {
        var sums = new int[3];
        for (var x = -radius; x <= radius; x++) Add(Math.Clamp(x, 0, width - 1), y, 1);
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = (byte)(sums[0] / diameter);
            pixels[offset + 1] = (byte)(sums[1] / diameter);
            pixels[offset + 2] = (byte)(sums[2] / diameter);
            if (x + 1 < width)
            {
                Add(Math.Max(x - radius, 0), y, -1);
                Add(Math.Min(x + radius + 1, width - 1), y, 1);
            }
        }

        void Add(int x, int row, int direction)
        {
            var offset = (row * width + x) * 4;
            sums[0] += source[offset] * direction;
            sums[1] += source[offset + 1] * direction;
            sums[2] += source[offset + 2] * direction;
        }
    }

    source = (byte[])pixels.Clone();
    for (var x = 0; x < width; x++)
    {
        var sums = new int[3];
        for (var y = -radius; y <= radius; y++) Add(x, Math.Clamp(y, 0, height - 1), 1);
        for (var y = 0; y < height; y++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = (byte)(sums[0] / diameter);
            pixels[offset + 1] = (byte)(sums[1] / diameter);
            pixels[offset + 2] = (byte)(sums[2] / diameter);
            if (y + 1 < height)
            {
                Add(x, Math.Max(y - radius, 0), -1);
                Add(x, Math.Min(y + radius + 1, height - 1), 1);
            }
        }

        void Add(int column, int y, int direction)
        {
            var offset = (y * width + column) * 4;
            sums[0] += source[offset] * direction;
            sums[1] += source[offset + 1] * direction;
            sums[2] += source[offset + 2] * direction;
        }
    }
}

static void DrawLine(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, byte r, byte g, byte b, byte a)
{
    var dx = Math.Abs(x1 - x0);
    var sx = x0 < x1 ? 1 : -1;
    var dy = -Math.Abs(y1 - y0);
    var sy = y0 < y1 ? 1 : -1;
    var error = dx + dy;
    while (true)
    {
        SetPixel(pixels, width, x0, y0, r, g, b, a);
        if (x0 == x1 && y0 == y1) break;
        var twiceError = error * 2;
        if (twiceError >= dy) { error += dy; x0 += sx; }
        if (twiceError <= dx) { error += dx; y0 += sy; }
    }
}

static void FillRect(byte[] pixels, int width, int height, int x, int y, int rectWidth, int rectHeight, byte r, byte g, byte b, byte a)
{
    for (var py = Math.Max(y, 0); py < Math.Min(y + rectHeight, height); py++)
    for (var px = Math.Max(x, 0); px < Math.Min(x + rectWidth, width); px++)
        SetPixel(pixels, width, px, py, r, g, b, a);
}

static void SetPixel(byte[] pixels, int width, int x, int y, byte r, byte g, byte b, byte a)
{
    if (x < 0 || y < 0 || x >= width || y * width * 4 >= pixels.Length) return;
    var offset = (y * width + x) * 4;
    pixels[offset] = r;
    pixels[offset + 1] = g;
    pixels[offset + 2] = b;
    pixels[offset + 3] = a;
}

readonly record struct Room(int Id, int AreaId, double X, double Y, double Z, string Sector, string Name, int[] ExitIds);
readonly record struct Layer(int AreaId, double Z, List<Room> Rooms);
readonly record struct ManifestEntry(int AreaId, double Z, double MinX, double MinY, double MaxX, double MaxY, int PixelsPerUnit, int Width, int Height, string FileName, string OverviewFileName);
readonly record struct Rgb(byte R, byte G, byte B);

static class Palette
{
    public const int UndergroundIndex = 10;

    public static readonly Rgb[] Colors =
    [
        new(42, 52, 40), new(24, 70, 40), new(19, 53, 36), new(77, 94, 51),
        new(133, 105, 57), new(80, 82, 78), new(145, 158, 158), new(25, 66, 91),
        new(48, 64, 44), new(96, 77, 59), new(43, 39, 47), new(112, 37, 22),
    ];

    public static int IndexFor(string sector, string? roomName = null)
    {
        var value = sector.Trim().ToLowerInvariant();
        if (value.Contains("lawa")) return 11;
        if (value.Contains("ocean") || value.Contains("morze") || value.Contains("rzeka") || value.Contains("jezioro") || value.Contains("woda")) return 7;
        if (value.Contains("lodowiec") || value.Contains("arkty") || value.Contains("tundra")) return 6;
        if (value.Contains("gory") || value.Contains("gorska") || value.Contains("wzgorza")) return 5;
        if (value.Contains("pust") || value.Contains("wydmy") || value.Contains("piaski") || value.Contains("plaza")) return 4;
        if (value.Contains("bagno") || value.Contains("blotna")) return 8;
        if (value.Contains("puszcza")) return 2;
        if (value.Contains("las")) return 1;
        if (value.Contains("miasto") || value.Contains("plac") || value.Contains("arena") || value.Contains("ruiny")) return 9;
        if (value.Contains("podzi") || value.Contains("jaskinia") || value.Contains("kopalnia") || value.Contains("wewnatrz")) return 10;
        if (value.Contains("pole") || value.Contains("laka") || value.Contains("trawa") || value.Contains("step")) return 3;
        var name = (roomName ?? string.Empty).ToLowerInvariant();
        if (name.Contains("las") || name.Contains("puszcz")) return 1;
        if (name.Contains("gor") || name.Contains("skal")) return 5;
        if (name.Contains("pustyn") || name.Contains("piask")) return 4;
        if (name.Contains("ocean") || name.Contains("rzek") || name.Contains("jezior") || name.Contains("wod")) return 7;
        if (name.Contains("ulica") || name.Contains("mur") || name.Contains("miast")) return 9;
        if (name.Contains("jaskin") || name.Contains("tunel") || name.Contains("korytar")) return 10;
        return 0;
    }

    public static Rgb MarkerFor(string sector, string? roomName = null)
    {
        var color = Colors[IndexFor(sector, roomName)];
        return new Rgb(Clamp(color.R + 75), Clamp(color.G + 75), Clamp(color.B + 65));
    }

    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);
}

static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static void Write(string path, int width, int height, byte[] rgba)
    {
        using var output = File.Create(path);
        output.Write(Signature);
        using var header = new MemoryStream();
        WriteInt(header, width);
        WriteInt(header, height);
        header.Write([8, 6, 0, 0, 0]);
        WriteChunk(output, "IHDR", header.ToArray());

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba, y * width * 4, width * 4);
            }
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        WriteInt(output, data.Length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        WriteInt(output, unchecked((int)Crc32(crcInput)));
    }

    private static void WriteInt(Stream output, int value)
    {
        output.WriteByte((byte)(value >> 24));
        output.WriteByte((byte)(value >> 16));
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)value);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xffffffffu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
