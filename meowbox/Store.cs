using System.Text.Json;

namespace meowbox;

public static class Store
{
    public record Data(List<Account> Accounts, List<Env> Envs, List<Instance> Instances, Settings? Settings = null);

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "meowbox");

    private static readonly string FilePath = Path.Combine(Dir, "data.json");
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    
    public static bool LoadFailed { get; private set; }

    public static Data Load()
    {
        if(!File.Exists(FilePath)) return Empty;

        try
        {
            return JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath), Opts) ?? Empty;
        }
        catch
        {
            LoadFailed = true;
            TryBackup();
            return Empty;
        }
    }

    public static void Save(Data d)
    {
        if(LoadFailed) return;

        Directory.CreateDirectory(Dir);

        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(d, Opts));

        if(File.Exists(FilePath)) File.Replace(temp, FilePath, null);
        else File.Move(temp, FilePath);
    }

    private static void TryBackup()
    {
        try { File.Copy(FilePath, FilePath + ".bak", overwrite: true); }
        catch { /* something didn't work, oh and also your config might be lost */ }
    }

    private static Data Empty => new([], [], []);
}
