// ThaiLoc: command-line form of RO3-FixThai for developers (see ThaiMap).
//
//   ThaiLoc <game folder> <out loc_en.tsv> [--english <loc_en.tsv>] [--extra <extra_th.tsv>]...
using Ro3FixThai;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: ThaiLoc <game folder> <out loc_en.tsv> [--english <loc_en.tsv>] [--extra <extra_th.tsv>]...");
    return 2;
}
string? english = null;
var extras = new List<(string, string)>();
for (int i = 2; i + 1 < args.Length; i += 2)
{
    if (args[i] == "--english") english = args[i + 1];
    else if (args[i] == "--extra") extras.Add((args[i + 1], File.ReadAllText(args[i + 1])));
    else { Console.Error.WriteLine("unknown option " + args[i]); return 2; }
}
try
{
    var r = ThaiMap.Build(args[0], args[1], english, extras, Console.Error.WriteLine);
    Console.WriteLine($"{args[1]}: {r.Entries} entries ({r.FromTables} Thai from the client, {r.FromExtra} extra, {r.StillEnglish} still English)");
    return 0;
}
catch (FileNotFoundException e)
{
    Console.Error.WriteLine($"{e.FileName}: {e.Message}");
    return 1;
}
