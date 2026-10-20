// RO3-FixThai.exe: Thai for an existing RO3Emu, in one double-click.
//
//   RO3-FixThai.exe                 next to RO3Emu.exe: finds the game from RO3Emu.settings.json
//   RO3-FixThai.exe <game folder>   any game folder
//   RO3-FixThai.exe restore         puts RO3Emu's English map back
//
// Writes the Thai map (ThaiMap) to <RO3Emu>\data\loc_en.tsv, which "RO3Emu.exe join" copies next to the game, and to
// <game>\ro3emu\loc_en.tsv, which the game reads. The English map is kept once as data\loc_en.tsv.english.
using System.Reflection;
using System.Text;
using System.Text.Json;
using Ro3FixThai;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("RO3-FixThai - ภาษาไทยสำหรับ RO3Emu");
Console.WriteLine("==================================");
int code = Run(args);
if (!Console.IsInputRedirected)
{
    Console.WriteLine();
    Console.WriteLine("กด Enter เพื่อปิด");
    Console.ReadLine();
}
return code;

static int Run(string[] args)
{
    var home = AppContext.BaseDirectory;
    var data = Path.Combine(home, "data");
    var map = Path.Combine(data, "loc_en.tsv");
    var english = map + ".english";
    bool restore = args.Length > 0 && args[0].Equals("restore", StringComparison.OrdinalIgnoreCase);

    if (!File.Exists(Path.Combine(home, "RO3Emu.exe")))
        Console.WriteLine("! ไม่เจอ RO3Emu.exe ในโฟลเดอร์นี้: แนะนำให้วาง RO3-FixThai.exe ไว้ข้าง RO3Emu.exe");

    var game = !restore && args.Length > 0 ? args[0] : KnownGame(home);
    if (game == null || !File.Exists(Path.Combine(game, "ro3.exe")))
    {
        Console.WriteLine("ไม่รู้ว่าเกมอยู่ที่ไหน ใส่โฟลเดอร์ที่มี ro3.exe แล้วกด Enter:");
        game = Console.ReadLine()?.Trim().Trim('"');
        if (string.IsNullOrEmpty(game) || !File.Exists(Path.Combine(game, "ro3.exe")))
        {
            Console.WriteLine("x โฟลเดอร์นี้ไม่มี ro3.exe");
            return 1;
        }
    }
    Console.WriteLine("เกม:   " + game);
    Console.WriteLine("RO3Emu: " + home);
    var target = Path.Combine(game, "ro3emu", "loc_en.tsv");

    if (restore)
    {
        if (!File.Exists(english))
        {
            Console.WriteLine("x ไม่มีไฟล์อังกฤษที่สำรองไว้ (data\\loc_en.tsv.english)");
            return 1;
        }
        File.Copy(english, map, overwrite: true);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(english, target, overwrite: true);
        Console.WriteLine("v กลับเป็นภาษาอังกฤษแล้ว");
        return 0;
    }

    if (!File.Exists(ThaiMap.ThaiChunk(game)))
    {
        Console.WriteLine("x เกมนี้ไม่มีตารางภาษาไทย (Localization_th) ต้องใช้ไคลเอนต์ที่เลือกภาษาไทยได้");
        return 1;
    }
    if (System.Diagnostics.Process.GetProcessesByName("ro3").Length > 0)
        Console.WriteLine("! เกมเปิดอยู่: ภาษาไทยจะขึ้นหลังปิดเกมแล้วเปิดใหม่");

    // The English fallback: RO3Emu's own map, or the copy kept the first time (the live file may be Thai already).
    string? fallback = File.Exists(english) ? english
        : File.Exists(map) && !File.ReadLines(map).First().StartsWith(ThaiMap.Header, StringComparison.Ordinal) ? map : null;
    if (fallback == map) File.Copy(map, english);
    if (fallback == null) Console.WriteLine("! ไม่มีไฟล์อังกฤษของ RO3Emu: ข้อความที่ไม่มีคำแปลไทยจะเป็นจีน (รัน RO3Emu.exe join สักครั้งก่อนจะดีกว่า)");

    Console.WriteLine();
    Console.WriteLine("> กำลังสร้างภาษาไทยจากไฟล์เกมในเครื่อง ...");
    var staged = Path.Combine(data, "loc_th.tsv.tmp");
    ThaiMap.Result result;
    try
    {
        result = ThaiMap.Build(game, staged, fallback, Extras(), line => Console.WriteLine(line));
    }
    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
    {
        Console.WriteLine("x สร้างไม่สำเร็จ: " + e.Message);
        return 1;
    }
    File.Copy(staged, map, overwrite: true);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(staged, target, overwrite: true);
    File.Delete(staged);

    Console.WriteLine($"v เสร็จ: {result.Entries:N0} ข้อความ (ไทย {result.FromTables:N0}, แปลเพิ่ม {result.FromExtra}, ยังเป็นอังกฤษ {result.StillEnglish:N0})");
    Console.WriteLine();
    Console.WriteLine("เข้าเกมด้วย RO3Emu.exe join <server> ตามปกติ แล้วในเกมเลือกภาษาไทย");
    Console.WriteLine("ป้ายชื่อ NPC และ login ไคลเอนต์ Recovery ต้องใช้ RO3Emu.exe ที่ patch แล้ว (ดู README)");
    Console.WriteLine("กลับเป็นอังกฤษ: RO3-FixThai.exe restore");
    return 0;
}

static string? KnownGame(string home)
{
    try
    {
        var settings = Path.Combine(home, "RO3Emu.settings.json");
        if (!File.Exists(settings)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(settings));
        return doc.RootElement.TryGetProperty("ClientDir", out var dir) && dir.ValueKind == JsonValueKind.String ? dir.GetString() : null;
    }
    catch (JsonException)
    {
        return null;
    }
}

static IEnumerable<(string, string)> Extras()
{
    var asm = Assembly.GetExecutingAssembly();
    foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("translations/", StringComparison.Ordinal)).Order())
    {
        using var reader = new StreamReader(asm.GetManifestResourceStream(name)!, Encoding.UTF8);
        yield return (name, reader.ReadToEnd());
    }
}
