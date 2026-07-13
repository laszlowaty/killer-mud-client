using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

internal static class TeacherCatalogLoader
{
    private const string ResourceName = "MudClient.App.Assets.Data.teachers.json.gz";
    private static readonly Lazy<IReadOnlyList<TeacherEntry>> Catalog = new(LoadCore);

    public static IReadOnlyList<TeacherEntry> Load() => Catalog.Value;

    private static IReadOnlyList<TeacherEntry> LoadCore()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Brak osadzonej bazy nauczycieli: {ResourceName}.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        var source = JsonSerializer.Deserialize<Dictionary<string, TeacherDto>>(
            gzip,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Osadzona baza nauczycieli jest pusta.");

        var teachers = source.ToDictionary(
            pair => pair.Key,
            pair => MutableTeacher.FromSource(pair.Key, pair.Value),
            StringComparer.Ordinal);

        foreach (var supplement in Supplements)
        {
            if (!teachers.TryGetValue(supplement.MobVnum, out var teacher))
            {
                teacher = new MutableTeacher(
                    supplement.MobVnum,
                    supplement.TeacherName,
                    supplement.Region,
                    supplement.Area,
                    null,
                    supplement.Classes.ToList(),
                    []);
                teachers.Add(supplement.MobVnum, teacher);
            }

            teacher.Area ??= supplement.Area;
            var skill = new TeacherSkillEntry(
                supplement.SkillName,
                supplement.Min,
                supplement.Max,
                supplement.RequiredSkill,
                supplement.Price);
            if (!teacher.Skills.Contains(skill))
            {
                teacher.Skills.Add(skill);
            }
        }

        return teachers.Values
            .Select(teacher => teacher.ToEntry())
            .OrderBy(teacher => teacher.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), true))
            .ToArray();
    }

    private sealed class TeacherDto
    {
        public string[] Classes { get; init; } = [];
        public string Mob { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public JsonElement RoomVnum { get; init; }
        public SkillDto[] Skills { get; init; } = [];
    }

    private sealed class SkillDto
    {
        public int Min { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? Max { get; init; }
        public int ReqSkill { get; init; }
        public int Price { get; init; }
    }

    private sealed class MutableTeacher(
        string mobVnum,
        string name,
        string region,
        string? area,
        string? roomVnum,
        List<string> classes,
        List<TeacherSkillEntry> skills)
    {
        public string MobVnum { get; } = mobVnum;
        public string Name { get; } = name;
        public string Region { get; } = region;
        public string? Area { get; set; } = area;
        public string? RoomVnum { get; } = roomVnum;
        public List<string> Classes { get; } = classes;
        public List<TeacherSkillEntry> Skills { get; } = skills;

        public static MutableTeacher FromSource(string mobVnum, TeacherDto source) => new(
            mobVnum,
            source.Mob,
            source.Region,
            null,
            source.RoomVnum.ValueKind switch
            {
                JsonValueKind.String => source.RoomVnum.GetString(),
                JsonValueKind.Number => source.RoomVnum.GetRawText(),
                _ => null,
            },
            source.Classes.ToList(),
            source.Skills.Select(skill => new TeacherSkillEntry(
                skill.Name,
                skill.Min,
                skill.Max,
                skill.ReqSkill,
                skill.Price)).ToList());

        public TeacherEntry ToEntry() => new(
            MobVnum,
            Name,
            Region,
            Area,
            RoomVnum,
            Classes,
            Skills.OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private sealed record TeacherSupplement(
        string SkillName,
        int Min,
        int Max,
        int Price,
        int RequiredSkill,
        string MobVnum,
        string TeacherName,
        string Area,
        string Region,
        string[] Classes);

    private static TeacherSupplement S(
        string skill,
        int min,
        int max,
        int price,
        int required,
        string mobVnum,
        string teacher,
        string area,
        string region,
        params string[] classes) =>
        new(skill, min, max, price, required, mobVnum, teacher, area, region, classes);

    // Entries supplied separately from the MudletScripts kbase snapshot.
    private static readonly TeacherSupplement[] Supplements =
    [
        S("whirlwind", 0, 45, 100, 30, "6611", "Wielki półogr Haghburg", "Koszmary Pustyni Kaan-ar", "Carrallak", "Nomad"),
        S("whirlwind", 0, 35, 0, 0, "60751", "Wędrowiec", "Karakris", "Karakris", "Nomad"),
        S("whirlwind", 0, 35, 0, 0, "15111", "Stary drowi trener", "Podmrok drowia strażnica", "Podmrok", "Nomad"),
        S("whirlwind", 35, 65, 75, 35, "61507", "Doświadczony nomad", "(Karakris) Wielka Pustynia 2", "Karakris", "Nomad"),
        S("whirlwind", 35, 50, 60, 42, "19216", "Drowi renegat", "Jaskinie Umbrowych Kolosów", "Podmrok", "Nomad"),
        S("whirlwind", 40, 75, 100, 40, "19409", "Doświadczony barbarzyńca", "Lodowa kraina", "Easterial", "Nomad"),
        S("whirlwind", 70, 95, 100, 70, "3810", "Spokojny tytan", "Wyspa tytanów", "Carrallak", "Nomad"),

        S("cyclone", 0, 15, 0, 0, "129", "Dowódca straży", "Krasnoludzka Forteca", "Forteca", "Nomad"),
        S("cyclone", 0, 20, 0, 0, "511", "Wojownik Wollheim", "Carrallak", "Carrallak", "Nomad"),
        S("cyclone", 0, 30, 0, 0, "6022", "Mistrz cechu najemników", "Miasto Arras", "Arras", "Nomad"),
        S("cyclone", 0, 35, 0, 0, "15111", "Stary drowi trener", "Podmrok drowia strażnica", "Podmrok", "Nomad"),
        S("cyclone", 15, 40, 0, 0, "10763", "Daerdyk", "Dziwny Zajazd Kraken", "Carrallak", "Nomad"),
        S("cyclone", 30, 55, 50, 40, "27577", "Instruktor", "Podmrok - Seeyma", "Forteca", "Nomad"),
        S("cyclone", 35, 70, 70, 45, "17937", "Nauczyciel Venzil", "Jaskinie Naugrimów", "Easterial", "Nomad"),
        S("cyclone", 35, 50, 60, 42, "19216", "Drowi renegat", "Jaskinie Umbrowych Kolosów", "Podmrok", "Nomad"),
        S("cyclone", 40, 70, 60, 50, "3601", "Dae'raira, Róża Pustyni", "Korytarze pod pustynią Kaan-ar", "Carrallak", "Nomad"),
        S("cyclone", 50, 90, 65, 55, "6611", "Wielki półogr Haghburg", "Koszmary Pustyni Kaan-ar", "Carrallak", "Nomad"),
        S("cyclone", 85, 95, 85, 85, "4507", "Mistrz Gregor", "Podwodna jaskinia Sahuaginów", "Carrallak", "Nomad"),

        S("circle", 0, 55, 65, 35, "17916", "Fhaana", "Jaskinie Naugrimów", "Easterial", "Złodziej"),
        S("circle", 0, 65, 66, 40, "61507", "Doświadczony nomad", "(Karakris) Wielka Pustynia 2", "Karakris", "Złodziej"),
        S("circle", 0, 65, 89, 46, "16705", "Drow złodziej", "Messholzarn", "Podmrok", "Złodziej"),
        S("circle", 30, 65, 65, 45, "40559", "Przywódca szajki", "Łowcy Smoków", "Forteca", "Złodziej"),
        S("circle", 55, 70, 83, 55, "1960", "Chytry złodziej", "Kryjówka złodziei", "Carrallak", "Złodziej"),
        S("circle", 60, 80, 80, 64, "16601", "Tankartez", "Kryjówka piratów", "Carrallak", "Złodziej"),
        S("circle", 60, 85, 75, 60, "20404", "Drowi zwiadowca", "Podmrok - Groty Zbojów", "Podmrok", "Złodziej"),
        S("circle", 80, 95, 90, 80, "14961", "Snat", "Filia Gildii Złodziei w Arras", "Silea", "Złodziej"),

        S("panther form", 0, 40, 35, 35, "1660", "Stary druid", "Druidzki Krąg", "Carrallak", "Druid"),
        S("panther form", 35, 65, 60, 35, "2508", "Druid Iverl", "Wyspa obłąkanych", "Carrallak", "Druid"),
        S("panther form", 65, 85, 80, 65, "42832", "Druidka Della", "Góry na zachód od Fortecy", "Forteca", "Druid"),
        S("panther form", 80, 95, 90, 80, "16288", "Duch druida", "[NK] Śnieżne równiny", "Easterial", "Druid"),

        S("wolf form", 0, 40, 35, 35, "1660", "Stary druid", "Druidzki Krąg", "Carrallak", "Druid"),
        S("wolf form", 35, 65, 60, 35, "2508", "Druid Iverl", "Wyspa obłąkanych", "Carrallak", "Druid"),
        S("wolf form", 65, 85, 80, 65, "42830", "Druid Drand", "Góry na zachód od Fortecy", "Forteca", "Druid"),
        S("wolf form", 80, 95, 90, 80, "16288", "Duch druida", "[NK] Śnieżne równiny", "Easterial", "Druid"),

        S("bear form", 0, 40, 35, 35, "1660", "Stary druid", "Druidzki Krąg", "Carrallak", "Druid"),
        S("bear form", 35, 65, 60, 35, "2508", "Druid Iverl", "Wyspa obłąkanych", "Carrallak", "Druid"),
        S("bear form", 65, 85, 80, 65, "42831", "Druid Ganor", "Góry na zachód od Fortecy", "Forteca", "Druid"),
        S("bear form", 80, 95, 90, 80, "16288", "Duch druida", "[NK] Śnieżne równiny", "Easterial", "Druid"),

        S("bladesplash", 0, 45, 75, 35, "1960", "Chytry złodziej", "Kryjówka złodziei", "Carrallak", "Złodziej"),
        S("bladesplash", 45, 65, 75, 45, "10785", "Nohi", "Dziwny Zajazd Kraken", "Carrallak", "Złodziej"),
        S("bladesplash", 60, 95, 75, 60, "20404", "Drowi zwiadowca", "Podmrok - Groty Zbojów", "Podmrok", "Złodziej"),
        S("bladesplash", 65, 85, 75, 65, "16705", "Drow złodziej", "Messholzarn", "Podmrok", "Złodziej"),
    ];
}
