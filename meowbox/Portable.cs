using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace meowbox;

public static class Portable
{
    private record Envelope(int V, string Kdf, int T, string Salt, string Nonce, string Tag, string Data);
    private record Plain(Guid Id, string Label, string Username, string Password, string Totp);

    private const int Iterations = 600_000;
    private const string Kdf = "pbkdf2-sha256";

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string Export(IEnumerable<Account> accounts, string passphrase)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(
            accounts.Select(a => new Plain(a.Id, a.Label, a.Username, a.Password, a.TotpSecret)));

        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plain.Length];

        using(var aes = new AesGcm(DeriveKey(passphrase, salt, Iterations), tag.Length))
            aes.Encrypt(nonce, plain, cipher, tag);

        return JsonSerializer.Serialize(new Envelope(1, Kdf, Iterations,
            Convert.ToBase64String(salt), Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag), Convert.ToBase64String(cipher)), Opts);
    }
    
    public static List<Account> Import(string json, string passphrase)
    {
        if(JsonSerializer.Deserialize<Envelope>(json, Opts) is not { } env) throw new InvalidDataException("not a meowbox export");
        if(env.V != 1 || env.Kdf != Kdf) throw new InvalidDataException($"unsupported export (v{env.V}, {env.Kdf})");

        var cipher = Convert.FromBase64String(env.Data);
        var plain = new byte[cipher.Length];

        using(var aes = new AesGcm(DeriveKey(passphrase, Convert.FromBase64String(env.Salt), env.T), 16))
            aes.Decrypt(Convert.FromBase64String(env.Nonce), cipher, Convert.FromBase64String(env.Tag), plain);

        return [.. (JsonSerializer.Deserialize<List<Plain>>(plain, Opts) ?? []).Select(p => new Account
        {
            Id = p.Id, Label = p.Label, Username = p.Username, Password = p.Password, TotpSecret = p.Totp,
        })];
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA256, 32);
}
