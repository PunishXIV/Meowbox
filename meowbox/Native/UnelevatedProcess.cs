using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace meowbox.Native;

public static class UnelevatedProcess
{
    public static uint Start(string executablePath, string currentDirectory, IEnumerable<string>? arguments = null)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        var commandLine = BuildCommandLine(executablePath, arguments ?? []);

        Process? explorer = Process.GetProcessesByName("explorer").FirstOrDefault(x => x.SessionId == Process.GetCurrentProcess().SessionId) ?? throw new InvalidOperationException("Could not find explorer.exe in the current session.");
        var processToken = IntPtr.Zero;
        var userToken = IntPtr.Zero;
        var environment = IntPtr.Zero;

        try
        {
            var explorerHandle = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, explorer.Id);

            if(explorerHandle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                if(!OpenProcessToken(explorerHandle,  TokenAccessFlags.Query | TokenAccessFlags.Duplicate |
                    TokenAccessFlags.AssignPrimary, out processToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                CloseHandle(explorerHandle);
            }

            SECURITY_ATTRIBUTES securityAttributes = new()
            {
                nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>()
            };

            if(!DuplicateTokenEx(processToken, TokenAccessFlags.AllAccess, ref securityAttributes, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out userToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if(!CreateEnvironmentBlock(out environment, userToken, false))
            {
                environment = IntPtr.Zero;
            }

            STARTUPINFO startupInfo = new()
            {
                cb = Marshal.SizeOf<STARTUPINFO>()
            };

            var creationFlags = CREATE_UNICODE_ENVIRONMENT;

            if(!CreateProcessWithTokenW(userToken, LogonFlags.WithProfile, null, commandLine, creationFlags, environment, currentDirectory, ref startupInfo, out PROCESS_INFORMATION processInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                return processInfo.dwProcessId;
            }
            finally
            {
                CloseHandle(processInfo.hThread);
                CloseHandle(processInfo.hProcess);
            }
        }
        finally
        {
            if(environment != IntPtr.Zero) DestroyEnvironmentBlock(environment);
            if(userToken != IntPtr.Zero) CloseHandle(userToken);
            if(processToken != IntPtr.Zero) CloseHandle(processToken);
            explorer.Dispose();
        }
    }

    private static string BuildCommandLine(string executablePath, IEnumerable<string> arguments)
    {
        StringBuilder result = new();

        result.Append(QuoteArgument(executablePath));

        foreach(var argument in arguments)
        {
            result.Append(' ');
            result.Append(QuoteArgument(argument));
        }

        return result.ToString();
    }

    private static string QuoteArgument(string argument)
    {
        if(argument.Length == 0) return "\"\"";

        var needsQuotes = argument.Any(char.IsWhiteSpace) || argument.Contains('"');

        if(!needsQuotes) return argument;

        StringBuilder result = new();
        result.Append('"');

        var backslashCount = 0;

        foreach(var c in argument)
        {
            if(c == '\\')
            {
                backslashCount++;
                continue;
            }

            if(c == '"')
            {
                result.Append('\\', backslashCount * 2 + 1);
                result.Append('"');
                backslashCount = 0;
                continue;
            }

            result.Append('\\', backslashCount);
            backslashCount = 0;

            result.Append(c);
        }

        result.Append('\\', backslashCount * 2);
        result.Append('"');

        return result.ToString();
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, TokenAccessFlags DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr hExistingToken, TokenAccessFlags dwDesiredAccess, ref SECURITY_ATTRIBUTES lpTokenAttributes, SECURITY_IMPERSONATION_LEVEL ImpersonationLevel, TOKEN_TYPE TokenType, out IntPtr phNewToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithTokenW(IntPtr hToken, LogonFlags dwLogonFlags, string? lpApplicationName, string lpCommandLine, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [Flags]
    private enum TokenAccessFlags : uint
    {
        AssignPrimary = 0x0001,
        Duplicate = 0x0002,
        Query = 0x0008,

        AllAccess = 0x000F01FF
    }

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryLimitedInformation = 0x1000
    }

    [Flags]
    private enum LogonFlags : uint
    {
        WithProfile = 0x00000001,
        NetCredentialsOnly = 0x00000002
    }

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
}