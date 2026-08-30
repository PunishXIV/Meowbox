using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Runtime.InteropServices;
using System.Text;
using Button = FlaUI.Core.AutomationElements.Button;
using TextBox = FlaUI.Core.AutomationElements.TextBox;

namespace meowbox.Native;

public static class LauncherAutomation
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static List<IntPtr> FindWindowsForPid(uint targetPid, Func<string, bool>? titleMatches = null)
    {
        var found = new List<IntPtr>();

        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if(pid != targetPid || !IsWindowVisible(hWnd)) return true;

            if(titleMatches != null)
            {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if(!titleMatches(sb.ToString())) return true;
            }

            found.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return found;
    }
    
    private static string DescribeVisibleWindows(uint targetPid)
    {
        var seen = new List<string>();

        EnumWindows((hWnd, lParam) =>
        {
            if(!IsWindowVisible(hWnd)) return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            if(title.Length == 0) return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            if(pid == targetPid || title.Contains("XIVLauncher", StringComparison.OrdinalIgnoreCase))
                seen.Add($"'{title}' pid {pid}");

            return true;
        }, IntPtr.Zero);

        return seen.Count == 0 ? "none" : string.Join(", ", seen);
    }

    private static Window? WaitForWindow(UIA3Automation automation, uint targetPid, Func<string, bool> titleMatches, DateTime deadline) =>
        WaitFor(() => FirstWindow(automation, FindWindowsForPid(targetPid, titleMatches), _ => true), deadline);
    
    private static Window? WaitForWindowContaining(UIA3Automation automation, uint targetPid, string automationId, DateTime deadline) =>
        WaitFor(() => FirstWindow(automation, FindWindowsForPid(targetPid),
            w => w.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null), deadline);

    private static Window? FirstWindow(UIA3Automation automation, List<IntPtr> handles, Func<Window, bool> accept)
    {
        foreach(var handle in handles)
        {
            try
            {
                if(automation.FromHandle(handle)?.AsWindow() is { } window && accept(window)) return window;
            }
            catch { /* something didn't work */ }
        }
        return null;
    }

    private static T? WaitFor<T>(Func<T?> find, DateTime deadline) => Retry.WhileNull(
        find,
        timeout: deadline - DateTime.UtcNow,
        interval: TimeSpan.FromMilliseconds(150),
        throwOnTimeout: false,
        ignoreException: true).Result;

    public static bool SubmitOtp(uint targetPid, string code, TimeSpan timeout, string titleContains = "OTP")
    {
        var deadline = DateTime.UtcNow + timeout;

        using var automation = new UIA3Automation();
        if(WaitForWindow(automation, targetPid, t => t.Contains(titleContains, StringComparison.OrdinalIgnoreCase), deadline) is not { } window) return false;

        if(WaitFor(() => window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.AsTextBox(), deadline) is not { } otpBox) return false;
        if(WaitFor(() => window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK")))?.AsButton(), deadline) is not Button okButton) return false;

        otpBox.Focus();
        otpBox.Text = code;
        Retry.WhileFalse(() => okButton.IsEnabled, timeout: TimeSpan.FromSeconds(2), interval: TimeSpan.FromMilliseconds(50), throwOnTimeout: false);

        okButton.Invoke();
        return true;
    }
    
    private static void SetText(TextBox box, string value)
    {
        box.Focus();

        var valuePattern = box.Patterns.Value.PatternOrDefault;
        if(valuePattern != null && !valuePattern.IsReadOnly)
        {
            try { valuePattern.SetValue(value); return; }
            catch { /* something didn't work */ }
        }

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(VirtualKeyShort.DELETE);
        Keyboard.Type(value);
    }
    
    public static string? FillLogin(uint targetPid, string username, string password, bool useOtp, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        using var automation = new UIA3Automation();
        if(WaitForWindowContaining(automation, targetPid, "LoginUsername", deadline) is not { } window)
            return $"login window not found for pid {targetPid}; visible windows: {DescribeVisibleWindows(targetPid)}";

        if(window.FindFirstDescendant(cf => cf.ByAutomationId("LoginUsername"))?.AsTextBox() is not { } userBox)
            return "username field vanished";

        if(WaitFor(() => window.FindFirstDescendant(cf => cf.ByAutomationId("LoginPassword"))?.AsTextBox(), deadline) is not { } passwordBox)
            return "password field not found";

        window.SetForeground();

        SetText(userBox, username);
        SetText(passwordBox, password);

        if(WaitFor(() => window.FindFirstDescendant(cf => cf.ByAutomationId("OtpCheckBox"))?.AsCheckBox(), deadline) is not { } otpCheckBox) return "OTP checkbox not found";
        if(otpCheckBox.IsChecked != useOtp) otpCheckBox.IsChecked = useOtp;

        passwordBox.Focus();
        Keyboard.Type(VirtualKeyShort.ENTER);
        return null;
    }
}
