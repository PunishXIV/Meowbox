using meowbox.Native;

namespace meowbox;

public static class Launcher
{
    public static uint Launch(Env env)
    {
        if(string.IsNullOrWhiteSpace(env.RoamingPath)) throw new InvalidOperationException("Environment has no roaming path.");
        Directory.CreateDirectory(env.RoamingPath);

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XIVLauncher", "current");
        var exe = Path.Combine(dir, "XIVLauncher.exe");
        if(!File.Exists(exe)) throw new FileNotFoundException($"XIVLauncher not found at {exe}");

        // --noautologin guarantees the login form appears for us to fill.
        return UnelevatedProcess.Start(exe, dir, ["--roamingPath", env.RoamingPath, "--noautologin"]);
    }
    
    public static string? SignIn(uint pid, string username, string password, string totpSecret, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var useOtp = !string.IsNullOrWhiteSpace(totpSecret);

        if(LauncherAutomation.FillLogin(pid, username, password, useOtp, deadline - DateTime.UtcNow) is { } failure) return failure;

        return !useOtp || SubmitOtpUntil(pid, totpSecret, deadline) ? null : "timed out submitting OTP";
    }

    private static bool SubmitOtpUntil(uint pid, string secret, DateTime deadline)
    {
        while(DateTime.UtcNow < deadline)
        {
            if(LauncherAutomation.SubmitOtp(pid, TotpGenerator.GenerateCode(secret), deadline - DateTime.UtcNow)) return true;
            Thread.Sleep(500);
        }
        return false;
    }
}
