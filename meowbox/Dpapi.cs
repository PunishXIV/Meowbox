using System.Security.Cryptography;
using System.Text;

namespace meowbox;

public static class Dpapi
{
    public static string Protect(string s) => string.IsNullOrEmpty(s)
        ? ""
        : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));

    public static string Unprotect(string s)
    {
        if(string.IsNullOrEmpty(s)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }
}
