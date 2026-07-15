using System.Text.Json;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Globalization;

namespace MudClient.MapImageCalibrator;

public sealed record NortantisExportLayout(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    int ProjectWidth,
    int ProjectHeight,
    double Resolution)
{
    public int OverlayWidth => checked((int)Math.Round(ProjectWidth * Resolution));
    public int OverlayHeight => checked((int)Math.Round(ProjectHeight * Resolution));
}

public sealed record NortantisExportResult(string ProjectPath, string OverlayPath, NortantisExportLayout Layout);

public sealed class NortantisExportService
{
    private const int MaximumProjectSide = 4096;
    private const int MinimumProjectSide = 1024;
    private const double Resolution = 2;
    private const double ContentFraction = 0.9;

    public NortantisExportLayout CalculateLayout(IReadOnlyList<RoomPoint> rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        if (rooms.Count == 0) throw new ArgumentException("Wybierz co najmniej jeden room.", nameof(rooms));

        var contentWidth = Math.Max(rooms.Max(room => room.X) - rooms.Min(room => room.X), 4);
        var contentHeight = Math.Max(rooms.Max(room => room.Y) - rooms.Min(room => room.Y), 4);
        var centerX = (rooms.Min(room => room.X) + rooms.Max(room => room.X)) / 2;
        var centerY = (rooms.Min(room => room.Y) + rooms.Max(room => room.Y)) / 2;
        var worldWidth = contentWidth / ContentFraction;
        var worldHeight = contentHeight / ContentFraction;
        var aspect = worldWidth / worldHeight;

        int projectWidth;
        int projectHeight;
        if (aspect >= 1)
        {
            projectWidth = MaximumProjectSide;
            projectHeight = Math.Max(MinimumProjectSide, (int)Math.Round(projectWidth / aspect));
        }
        else
        {
            projectHeight = MaximumProjectSide;
            projectWidth = Math.Max(MinimumProjectSide, (int)Math.Round(projectHeight * aspect));
        }

        var projectAspect = projectWidth / (double)projectHeight;
        if (worldWidth / worldHeight < projectAspect)
            worldWidth = worldHeight * projectAspect;
        else
            worldHeight = worldWidth / projectAspect;

        return new NortantisExportLayout(
            centerX - worldWidth / 2,
            centerY - worldHeight / 2,
            centerX + worldWidth / 2,
            centerY + worldHeight / 2,
            projectWidth,
            projectHeight,
            Resolution);
    }

    public NortantisExportResult Export(
        IReadOnlyList<RoomPoint> rooms,
        string projectName,
        string templatePath,
        string nortantisDirectory)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Nie znaleziono bazowego projektu Nortantis.", templatePath);

        var safeName = ToSafeFileName(projectName);
        if (safeName.Length == 0) throw new ArgumentException("Podaj nazwę projektu.", nameof(projectName));

        var layout = CalculateLayout(rooms);
        var projectsDirectory = Path.Combine(nortantisDirectory, "Projects");
        var overlaysDirectory = Path.Combine(nortantisDirectory, "Overlays");
        Directory.CreateDirectory(projectsDirectory);
        Directory.CreateDirectory(overlaysDirectory);
        var projectPath = Path.Combine(projectsDirectory, safeName + ".nort");
        var overlayPath = Path.Combine(overlaysDirectory, safeName + "-rooms.png");

        SaveOverlay(rooms, layout, overlayPath);
        SaveProject(templatePath, projectPath, overlayPath, layout);
        return new NortantisExportResult(projectPath, overlayPath, layout);
    }

    public void RepairProject(string projectPath, string templatePath)
    {
        using var broken = JsonDocument.Parse(File.ReadAllText(projectPath));
        var root = broken.RootElement;
        var width = root.GetProperty("generatedWidth").GetInt32();
        var height = root.GetProperty("generatedHeight").GetInt32();
        var resolution = root.GetProperty("resolution").GetDouble();
        var overlayPath = root.GetProperty("overlayImagePath").GetString()
            ?? throw new InvalidDataException("Projekt nie zawiera ścieżki overlayu.");
        overlayPath = overlayPath.Replace(
            "%HOMEPATH%",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            StringComparison.OrdinalIgnoreCase);

        var temporaryPath = projectPath + ".repairing";
        try
        {
            SaveProject(
                templatePath,
                temporaryPath,
                overlayPath,
                new NortantisExportLayout(0, 0, 1, 1, width, height, resolution));
            File.Move(temporaryPath, projectPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public void SaveProject(
        string templatePath,
        string projectPath,
        string overlayPath,
        NortantisExportLayout layout)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Bazowy plik .nort nie zawiera poprawnego obiektu JSON.");

        using var stream = File.Create(projectPath);
        using var writer = new Utf8JsonWriter(stream);
        var written = new HashSet<string>(StringComparer.Ordinal);
        writer.WriteStartObject();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            written.Add(property.Name);
            switch (property.Name)
            {
                case "generatedWidth": writer.WriteNumber(property.Name, layout.ProjectWidth); break;
                case "generatedHeight": writer.WriteNumber(property.Name, layout.ProjectHeight); break;
                case "resolution": WriteDouble(writer, property.Name, layout.Resolution); break;
                case "overlayImagePath": writer.WriteString(property.Name, ToNortantisPath(overlayPath)); break;
                case "drawOverlayImage": writer.WriteBoolean(property.Name, true); break;
                case "overlayImageTransparency": writer.WriteNumber(property.Name, 25); break;
                case "overlayScale": WriteDouble(writer, property.Name, 1); break;
                case "overlayOffsetResolutionInvariant": writer.WriteString(property.Name, "(0.0, 0.0)"); break;
                case "drawBorder": writer.WriteBoolean(property.Name, false); break;
                case "borderWidth": writer.WriteNumber(property.Name, 0); break;
                case "borderPosition": writer.WriteString(property.Name, "Outside map"); break;
                case "drawText": writer.WriteBoolean(property.Name, false); break;
                case "drawRoads": writer.WriteBoolean(property.Name, false); break;
                case "cityProbability": WriteDouble(writer, property.Name, 0); break;
                case "imageExportPath": writer.WriteNull(property.Name); break;
                case "edits": WriteEdits(writer, property); break;
                default: property.WriteTo(writer); break;
            }
        }
        if (!written.Contains("generatedWidth")) writer.WriteNumber("generatedWidth", layout.ProjectWidth);
        if (!written.Contains("generatedHeight")) writer.WriteNumber("generatedHeight", layout.ProjectHeight);
        if (!written.Contains("resolution")) WriteDouble(writer, "resolution", layout.Resolution);
        if (!written.Contains("overlayImagePath")) writer.WriteString("overlayImagePath", ToNortantisPath(overlayPath));
        if (!written.Contains("drawOverlayImage")) writer.WriteBoolean("drawOverlayImage", true);
        if (!written.Contains("overlayImageTransparency")) writer.WriteNumber("overlayImageTransparency", 25);
        if (!written.Contains("overlayScale")) WriteDouble(writer, "overlayScale", 1);
        if (!written.Contains("overlayOffsetResolutionInvariant"))
            writer.WriteString("overlayOffsetResolutionInvariant", "(0.0, 0.0)");
        if (!written.Contains("drawBorder")) writer.WriteBoolean("drawBorder", false);
        if (!written.Contains("borderWidth")) writer.WriteNumber("borderWidth", 0);
        if (!written.Contains("borderPosition")) writer.WriteString("borderPosition", "Outside map");
        if (!written.Contains("drawText")) writer.WriteBoolean("drawText", false);
        if (!written.Contains("drawRoads")) writer.WriteBoolean("drawRoads", false);
        if (!written.Contains("cityProbability")) WriteDouble(writer, "cityProbability", 0);
        if (!written.Contains("imageExportPath")) writer.WriteNull("imageExportPath");
        writer.WriteEndObject();
    }

    private static void WriteEdits(Utf8JsonWriter writer, JsonProperty editsProperty)
    {
        writer.WritePropertyName(editsProperty.Name);
        writer.WriteStartObject();
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in editsProperty.Value.EnumerateObject())
        {
            written.Add(property.Name);
            switch (property.Name)
            {
                case "iconEdits":
                case "textEdits":
                case "roads":
                    writer.WritePropertyName(property.Name);
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                    break;
                case "hasIconEdits":
                    // An explicitly edited, empty list means "draw no icons". With false,
                    // Nortantis tries to regenerate icons from center.biome; imported center
                    // edits do not carry biome data and that path crashes while opening.
                    writer.WriteBoolean(property.Name, true);
                    break;
                default:
                    property.WriteTo(writer);
                    break;
            }
        }
        foreach (var name in new[] { "iconEdits", "textEdits", "roads" })
        {
            if (written.Contains(name)) continue;
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
        if (!written.Contains("hasIconEdits")) writer.WriteBoolean("hasIconEdits", true);
        writer.WriteEndObject();
    }

    private static void WriteDouble(Utf8JsonWriter writer, string propertyName, double value)
    {
        writer.WritePropertyName(propertyName);
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!text.Contains('.') && !text.Contains('E') && !text.Contains('e')) text += ".0";
        writer.WriteRawValue(text, skipInputValidation: true);
    }

    public static IReadOnlyList<(RoomPoint From, RoomPoint To)> SelectedConnections(IReadOnlyList<RoomPoint> rooms)
    {
        var byId = rooms.ToDictionary(room => room.Id);
        return rooms.SelectMany(room => room.Exits
                .Where(exitId => room.Id < exitId && byId.ContainsKey(exitId))
                .Select(exitId => (room, byId[exitId])))
            .ToList();
    }

    public static void SaveOverlay(
        IReadOnlyList<RoomPoint> rooms,
        NortantisExportLayout layout,
        string path)
    {
        var canvas = new RgbaCanvas(layout.OverlayWidth, layout.OverlayHeight);
        (double X, double Y) Map(RoomPoint room) => (
            (room.X - layout.MinX) / (layout.MaxX - layout.MinX) * layout.OverlayWidth,
            (layout.MaxY - room.Y) / (layout.MaxY - layout.MinY) * layout.OverlayHeight);

        foreach (var (from, to) in SelectedConnections(rooms))
        {
            var a = Map(from);
            var b = Map(to);
            canvas.DrawLine(a.X, a.Y, b.X, b.Y, 7, new Rgba(20, 18, 13, 220));
            canvas.DrawLine(a.X, a.Y, b.X, b.Y, 3, new Rgba(244, 226, 174, 245));
        }

        foreach (var room in rooms)
        {
            var point = Map(room);
            canvas.FillRectangle(point.X - 9, point.Y - 9, 18, 18, SectorColor(room.Sector));
            canvas.StrokeRectangle(point.X - 9, point.Y - 9, 18, 18, 3, new Rgba(18, 16, 12, 245));
        }

        canvas.SavePng(path);
    }

    private static Rgba SectorColor(string? sector)
    {
        var value = (sector ?? string.Empty).ToLowerInvariant();
        if (value.Contains("woda") || value.Contains("rzeka") || value.Contains("morze") || value.Contains("ocean"))
            return new Rgba(76, 151, 192, 255);
        if (value.Contains("pust") || value.Contains("piask") || value.Contains("wydm"))
            return new Rgba(221, 177, 91, 255);
        if (value.Contains("gory") || value.Contains("gorsk") || value.Contains("wzgorz"))
            return new Rgba(169, 170, 164, 255);
        if (value.Contains("miasto") || value.Contains("plac") || value.Contains("ruin"))
            return new Rgba(199, 145, 94, 255);
        if (value.Contains("las") || value.Contains("puszcz"))
            return new Rgba(69, 145, 83, 255);
        return new Rgba(205, 210, 187, 255);
    }

    private static string ToSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return string.Concat(value.Trim().Select(character => invalid.Contains(character) ? '-' : character))
            .Replace(' ', '-').ToLowerInvariant();
    }

    private static string ToNortantisPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (fullPath.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return "%HOMEPATH%" + fullPath[home.Length..];
        return fullPath;
    }

    private readonly record struct Rgba(byte R, byte G, byte B, byte A);

    private sealed class RgbaCanvas
    {
        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        private readonly byte[] _pixels;

        public RgbaCanvas(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            _pixels = new byte[checked(width * height * 4)];
        }

        public int Width { get; }
        public int Height { get; }

        public void DrawLine(double x1, double y1, double x2, double y2, int width, Rgba color)
        {
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1))));
            var radius = Math.Max(1, width / 2);
            for (var step = 0; step <= steps; step++)
            {
                var progress = step / (double)steps;
                StampCircle(
                    (int)Math.Round(x1 + (x2 - x1) * progress),
                    (int)Math.Round(y1 + (y2 - y1) * progress),
                    radius,
                    color);
            }
        }

        public void FillRectangle(double x, double y, double width, double height, Rgba color)
        {
            var left = (int)Math.Floor(x);
            var top = (int)Math.Floor(y);
            var right = (int)Math.Ceiling(x + width);
            var bottom = (int)Math.Ceiling(y + height);
            for (var py = top; py < bottom; py++)
                for (var px = left; px < right; px++)
                    SetPixel(px, py, color);
        }

        public void StrokeRectangle(double x, double y, double width, double height, int stroke, Rgba color)
        {
            FillRectangle(x, y, width, stroke, color);
            FillRectangle(x, y + height - stroke, width, stroke, color);
            FillRectangle(x, y, stroke, height, color);
            FillRectangle(x + width - stroke, y, stroke, height, color);
        }

        public void SavePng(string path)
        {
            using var stream = File.Create(path);
            stream.Write(PngSignature);
            Span<byte> header = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, Width);
            BinaryPrimitives.WriteInt32BigEndian(header[4..], Height);
            header[8] = 8;
            header[9] = 6;
            WriteChunk(stream, "IHDR", header);

            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                for (var y = 0; y < Height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(_pixels, y * Width * 4, Width * 4);
                }
            }
            WriteChunk(stream, "IDAT", compressed.ToArray());
            WriteChunk(stream, "IEND", []);
        }

        private void StampCircle(int centerX, int centerY, int radius, Rgba color)
        {
            for (var y = -radius; y <= radius; y++)
                for (var x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius) SetPixel(centerX + x, centerY + y, color);
        }

        private void SetPixel(int x, int y, Rgba color)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            var index = (y * Width + x) * 4;
            _pixels[index] = color.R;
            _pixels[index + 1] = color.G;
            _pixels[index + 2] = color.B;
            _pixels[index + 3] = color.A;
        }

        private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);

            var crc = 0xffffffffu;
            foreach (var value in typeBytes) crc = UpdateCrc(crc, value);
            foreach (var value in data) crc = UpdateCrc(crc, value);
            Span<byte> crcBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xffffffffu);
            stream.Write(crcBytes);
        }

        private static uint UpdateCrc(uint crc, byte value)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            return crc;
        }
    }
}
