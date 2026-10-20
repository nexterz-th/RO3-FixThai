// RO3-FixThai.exe: Thai for an existing RO3Emu, in a small window (the console cannot draw Thai with its default font).
//
// Put it next to RO3Emu.exe (the game is read from RO3Emu.settings.json) or in the game folder (next to ro3.exe);
// either folder can also be picked in the window. "RO3-FixThai.exe restore" puts the English map back without a window.
//
// Writes the Thai map (ThaiMap) to <RO3Emu>\data\loc_en.tsv, which "RO3Emu.exe join" copies next to the game, and to
// <game>\ro3emu\loc_en.tsv, which the game reads. The English map is kept once as data\loc_en.tsv.english.
using System.Reflection;
using System.Text;
using System.Text.Json;
using Ro3FixThai;

ApplicationConfiguration.Initialize();
var form = new FixThaiForm(args.Length > 0 && args[0].Equals("restore", StringComparison.OrdinalIgnoreCase));
Application.Run(form);
return form.ExitCode;

internal sealed class FixThaiForm : Form
{
    private static readonly Font ThaiFont = new("Leelawadee UI", 10f);
    private static readonly Color Green = Color.FromArgb(46, 164, 79);

    private readonly TextBox _game = new() { ReadOnly = true };
    private readonly TextBox _home = new() { ReadOnly = true };
    private readonly Button _install = new() { Text = "ติดตั้งภาษาไทย", Height = 46 };
    private readonly Button _restore = new() { Text = "กลับเป็นภาษาอังกฤษ", Height = 32 };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly bool _restoreOnly;

    public int ExitCode { get; private set; }

    public FixThaiForm(bool restoreOnly)
    {
        _restoreOnly = restoreOnly;
        Text = "RO3-FixThai - ภาษาไทยสำหรับ RO3Emu";
        Font = ThaiFont;
        ClientSize = new Size(620, 430);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label { Text = "ภาษาไทยสำหรับ RO3Emu", Font = new Font(ThaiFont.FontFamily, 16f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 14) };
        var sub = new Label { Text = "สร้างคำแปลไทยจากไฟล์เกมในเครื่องของคุณ แล้ววางให้ RO3Emu ใช้", AutoSize = true, ForeColor = Color.DimGray, Location = new Point(20, 50) };
        Controls.Add(title);
        Controls.Add(sub);

        AddRow("โฟลเดอร์เกม (มี ro3.exe)", _game, 86, PickGame);
        AddRow("โฟลเดอร์ RO3Emu (มี RO3Emu.exe)", _home, 146, PickHome);

        _install.SetBounds(20, 206, 580, 46);
        _install.BackColor = Green;
        _install.ForeColor = Color.White;
        _install.FlatStyle = FlatStyle.Flat;
        _install.Font = new Font(ThaiFont.FontFamily, 12f, FontStyle.Bold);
        _install.Click += async (_, _) => await InstallAsync();
        _restore.SetBounds(20, 258, 180, 32);
        _restore.Click += (_, _) => Restore();
        _log.SetBounds(20, 300, 580, 112);
        Controls.Add(_install);
        Controls.Add(_restore);
        Controls.Add(_log);

        Locate();
        ActiveControl = _install;
        Shown += (_, _) =>
        {
            if (_restoreOnly) Restore();
        };
    }

    private void AddRow(string caption, TextBox box, int y, Action pick)
    {
        Controls.Add(new Label { Text = caption, AutoSize = true, Location = new Point(20, y) });
        box.SetBounds(20, y + 24, 480, 28);
        var change = new Button { Text = "เลือก...", Bounds = new Rectangle(508, y + 22, 92, 30) };
        change.Click += (_, _) => pick();
        Controls.Add(box);
        Controls.Add(change);
    }

    /// <summary>The folders from where this exe sits: next to RO3Emu.exe (game from its settings) or next to ro3.exe.</summary>
    private void Locate()
    {
        var here = AppContext.BaseDirectory.TrimEnd('\\', '/');
        if (File.Exists(Path.Combine(here, "ro3.exe"))) _game.Text = here;
        if (File.Exists(Path.Combine(here, "RO3Emu.exe")))
        {
            _home.Text = here;
            if (_game.Text.Length == 0 && KnownGame(here) is { } game && File.Exists(Path.Combine(game, "ro3.exe"))) _game.Text = game;
        }
        if (_game.Text.Length == 0) Log("ยังไม่รู้ว่าเกมอยู่ที่ไหน: กด \"เลือก...\" แล้วเลือกโฟลเดอร์ที่มี ro3.exe");
        if (_home.Text.Length == 0)
            Log("ไม่เจอ RO3Emu.exe: เลือกโฟลเดอร์ RO3Emu ด้วย ไม่อย่างนั้น RO3Emu.exe join อาจเขียนภาษาอังกฤษทับตอนเข้าเกม");
    }

    private void PickGame()
    {
        if (Pick("เลือกโฟลเดอร์เกม (ที่มี ro3.exe)", "ro3.exe") is { } dir)
        {
            _game.Text = dir;
            if (_home.Text.Length == 0) Log("เกม: " + dir);
        }
    }

    private void PickHome()
    {
        if (Pick("เลือกโฟลเดอร์ RO3Emu (ที่มี RO3Emu.exe)", "RO3Emu.exe") is not { } dir) return;
        _home.Text = dir;
        if (_game.Text.Length == 0 && KnownGame(dir) is { } game && File.Exists(Path.Combine(game, "ro3.exe"))) _game.Text = game;
    }

    private string? Pick(string description, string mustHave)
    {
        using var dialog = new FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        if (File.Exists(Path.Combine(dialog.SelectedPath, mustHave))) return dialog.SelectedPath;
        MessageBox.Show(this, $"โฟลเดอร์นี้ไม่มี {mustHave}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return null;
    }

    private async Task InstallAsync()
    {
        string game = _game.Text, home = _home.Text;
        if (game.Length == 0)
        {
            MessageBox.Show(this, "เลือกโฟลเดอร์เกม (ที่มี ro3.exe) ก่อน", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(ThaiMap.ThaiChunk(game)))
        {
            Fail("เกมนี้ไม่มีตารางภาษาไทย (Localization_th) ต้องใช้ไคลเอนต์ที่เลือกภาษาไทยได้");
            return;
        }
        if (System.Diagnostics.Process.GetProcessesByName("ro3").Length > 0)
            Log("เกมเปิดอยู่: ภาษาไทยจะขึ้นหลังปิดเกมแล้วเปิดใหม่");

        string? map = home.Length > 0 ? Path.Combine(home, "data", "loc_en.tsv") : null;
        string? english = map != null ? map + ".english" : null;
        // The English fallback: RO3Emu's own map, or the copy kept the first time (the live file may be Thai already).
        string? fallback = english != null && File.Exists(english) ? english
            : map != null && File.Exists(map) && !File.ReadLines(map).First().StartsWith(ThaiMap.Header, StringComparison.Ordinal) ? map : null;
        if (fallback == map && map != null) File.Copy(map, english!);
        if (fallback == null) Log("ไม่มีไฟล์อังกฤษของ RO3Emu: ข้อความที่ไม่มีคำแปลไทยจะเป็นจีน");

        _install.Enabled = _restore.Enabled = false;
        _install.Text = "กำลังสร้างภาษาไทย ...";
        Log("กำลังสร้างภาษาไทยจากไฟล์เกมในเครื่อง ...");
        var staged = Path.Combine(Path.GetTempPath(), "ro3-fixthai-loc.tsv");
        try
        {
            var result = await Task.Run(() => ThaiMap.Build(game, staged, fallback, Extras(), line => BeginInvoke(() => Log(line))));
            var target = Path.Combine(game, "ro3emu", "loc_en.tsv");
            // Thai tables differ between client builds (2.2 MB vs 4.3 MB seen): never swap a richer map for a poorer one unasked.
            int before = ThaiLines(target), after = ThaiLines(staged);
            if (after < before && MessageBox.Show(this,
                    $"ไฟล์ภาษาไทยที่ใช้อยู่มีคำแปลไทยมากกว่า ({before:N0} บรรทัด) ไฟล์ที่เพิ่งสร้างจากเกมนี้ ({after:N0} บรรทัด)\n\n"
                    + "ตารางภาษาไทยของเกมแต่ละเวอร์ชันไม่เท่ากัน ต้องการทับไฟล์เดิมไหม?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                File.Delete(staged);
                Log($"ไม่ได้ทับ: ไฟล์เดิมมีภาษาไทย {before:N0} บรรทัด มากกว่าไฟล์ใหม่ {after:N0} บรรทัด");
                _install.Text = "ติดตั้งภาษาไทย";
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(staged, target, overwrite: true);
            if (map != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(map)!);
                File.Copy(staged, map, overwrite: true);
            }
            File.Delete(staged);
            Log($"เสร็จ: {result.Entries:N0} ข้อความ (ไทย {result.FromTables:N0}, แปลเพิ่ม {result.FromExtra}, ยังเป็นอังกฤษ {result.StillEnglish:N0})");
            _install.Text = "ติดตั้งเสร็จแล้ว ✓";
            MessageBox.Show(this,
                "ติดตั้งภาษาไทยเสร็จแล้ว\n\nเข้าเกมด้วย RO3Emu.exe join <server> ตามปกติ แล้วในเกมเลือกภาษาไทย"
                + (map == null ? "\n\nยังไม่ได้เลือกโฟลเดอร์ RO3Emu: ถ้าเข้าเกมแล้วกลับเป็นอังกฤษ ให้เลือกโฟลเดอร์ RO3Emu แล้วกดติดตั้งอีกครั้ง" : ""),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            Fail("สร้างไม่สำเร็จ: " + e.Message);
            _install.Text = "ติดตั้งภาษาไทย";
        }
        finally
        {
            _install.Enabled = _restore.Enabled = true;
        }
    }

    private void Restore()
    {
        string game = _game.Text, home = _home.Text;
        var english = home.Length > 0 ? Path.Combine(home, "data", "loc_en.tsv.english") : null;
        if (english == null || !File.Exists(english))
        {
            Fail("ไม่มีไฟล์อังกฤษที่สำรองไว้ (data\\loc_en.tsv.english ในโฟลเดอร์ RO3Emu)");
            return;
        }
        File.Copy(english, Path.Combine(home, "data", "loc_en.tsv"), overwrite: true);
        if (game.Length > 0)
        {
            var target = Path.Combine(game, "ro3emu", "loc_en.tsv");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(english, target, overwrite: true);
        }
        Log("กลับเป็นภาษาอังกฤษแล้ว");
        MessageBox.Show(this, "กลับเป็นภาษาอังกฤษแล้ว", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Fail(string message)
    {
        ExitCode = 1;
        Log(message);
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>Lines of a map whose text holds Thai letters (0 when there is no map).</summary>
    private static int ThaiLines(string path) =>
        File.Exists(path) ? File.ReadLines(path).Count(l => l.IndexOf('\t') is var tab and > 0 && l.AsSpan(tab).IndexOfAnyInRange('฀', '๿') >= 0) : 0;

    private void Log(string line) => _log.AppendText((_log.TextLength > 0 ? Environment.NewLine : "") + line);

    private static string? KnownGame(string home)
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

    private static IEnumerable<(string, string)> Extras()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("translations/", StringComparison.Ordinal)).Order())
        {
            using var reader = new StreamReader(asm.GetManifestResourceStream(name)!, Encoding.UTF8);
            yield return (name, reader.ReadToEnd());
        }
    }
}
