// ThaiLoc: the Thai text map for Ro3Loc, made from the player's own game files.
//
//   ThaiLoc <game folder> <out loc_en.tsv> [--english <loc_en.tsv>] [--extra <extra_th.tsv>]...
//
// 1. Decodes the client's Localization_zh_CN and Localization_th chunks (Recovery/LuaPayload).
// 2. zh -> th for every id both tables hold (the most frequent Thai when one Chinese text has several).
//    Quest goals carry a trailing '*' in the table that the client drops when it draws them: both forms are kept.
// 3. --extra files (translations/extra_th.tsv): text no table holds; they win over the tables.
// 4. --english (RO3Emu's data/loc_en.tsv): every Chinese text the Thai table lacks stays English.
// The output starts with RO3Emu's generated-map header, so "RO3Emu.exe join" copies it next to the game.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ro3Emu.DataExport;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: ThaiLoc <game folder> <out loc_en.tsv> [--english <loc_en.tsv>] [--extra <extra_th.tsv>]...");
    return 2;
}
string client = args[0], output = args[1];
string? english = null;
var extras = new List<string>();
for (int i = 2; i + 1 < args.Length; i += 2)
{
    if (args[i] == "--english") english = args[i + 1];
    else if (args[i] == "--extra") extras.Add(args[i + 1]);
    else { Console.Error.WriteLine("unknown option " + args[i]); return 2; }
}

var cjk = new Regex("[\u2E80-\u9FFF\uF900-\uFAFF]");
string Unescape(string s) => Regex.Replace(s, @"\\(.)", m => m.Groups[1].Value switch { "t" => "\t", "n" => "\n", "r" => "\r", var c => c });
string Escape(string s) => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

var payload = GameDataExport.LuaPayloadDir(client);
var work = Directory.CreateTempSubdirectory("thailoc-");
try
{
    var luac = Path.Combine(work.FullName, "luac");
    Directory.CreateDirectory(luac);
    foreach (var name in new[] { "Localization_zh_CN", "Localization_th" })
    {
        var source = Path.Combine(payload, name + ".lua.bytes");
        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"{source} is missing: this client has no {name} (a client without Thai cannot be used).");
            return 1;
        }
        var bytes = File.ReadAllBytes(source);
        LuaChunkDecoder.Decode(bytes);
        File.WriteAllBytes(Path.Combine(luac, name + ".luac"), bytes);
    }
    var tables = Path.Combine(work.FullName, "tables");
    var errors = new List<string>();
    using (var exporter = new LuaConfigExporter(luac))
        exporter.ExportDirs(tables, ["Localization_zh_CN.luac", "Localization_th.luac"], errors.Add);
    foreach (var e in errors) Console.Error.WriteLine("  " + e);

    Dictionary<string, string> Texts(string table)
    {
        var texts = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tables, table + ".json")));
        foreach (var row in doc.RootElement.EnumerateObject())
            if (row.Value.ValueKind == JsonValueKind.Object && row.Value.TryGetProperty("_kDes", out var d) && d.ValueKind == JsonValueKind.String)
                texts[row.Name] = d.GetString()!;
        return texts;
    }
    var zh = Texts("Localization_zh_CN");
    var th = Texts("Localization_th");

    var votes = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
    foreach (var (id, z) in zh)
    {
        if (!th.TryGetValue(id, out var t) || !cjk.IsMatch(z) || string.IsNullOrWhiteSpace(t) || t == "None" || cjk.IsMatch(t)) continue;
        if (!votes.TryGetValue(z, out var v)) votes[z] = v = new Dictionary<string, int>(StringComparer.Ordinal);
        v[t] = v.GetValueOrDefault(t) + 1;
    }
    var thai = votes.ToDictionary(p => p.Key, p => p.Value.OrderByDescending(x => x.Value).First().Key, StringComparer.Ordinal);
    foreach (var (z, t) in thai.ToList())
    {
        var bare = z.TrimEnd('*');
        if (bare != z && !thai.ContainsKey(bare)) thai[bare] = t.TrimEnd('*');
    }
    int fromTables = thai.Count, fromExtra = 0;
    foreach (var file in extras)
        foreach (var line in File.ReadLines(file))
        {
            int tab = line.IndexOf('\t');
            if (line.Length == 0 || line[0] == '#' || tab <= 0) continue;
            thai[Unescape(line[..tab]).TrimStart('\uFEFF')] = Unescape(line[(tab + 1)..]);
            fromExtra++;
        }

    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    int keptEnglish = 0;
    if (english != null)
        foreach (var line in File.ReadLines(english))
        {
            int tab = line.IndexOf('\t');
            if (line.Length == 0 || line[0] == '#' || tab <= 0) continue;
            var z = Unescape(line[..tab]);
            if (thai.TryGetValue(z, out var t)) map[z] = t;
            else { map[z] = Unescape(line[(tab + 1)..]); keptEnglish++; }
        }
    foreach (var (z, t) in thai) map.TryAdd(z, t);

    var sb = new StringBuilder("# zh\ten - generated by RO3-FixThai ThaiLoc: Thai (client Localization_th, English fallback)\n");
    foreach (var (z, t) in map) sb.Append(Escape(z)).Append('\t').Append(Escape(t)).Append('\n');
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"{output}: {map.Count} entries ({fromTables} Thai from the client, {fromExtra} extra, {keptEnglish} still English)");
    return 0;
}
finally
{
    work.Delete(recursive: true);
}
