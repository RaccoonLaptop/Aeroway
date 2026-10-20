using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ZapretUI.Services;

/// <summary>
/// Windows pseudoconsole owned by Aeroway itself.
/// A separate PTY library pulls in Vanara, which Smart App Control blocks.
/// </summary>
internal sealed class ConPtyProcess : IDisposable
{
    private readonly SafeFileHandle _inputWrite;
    private readonly SafeFileHandle _outputRead;
    private readonly IntPtr _process;
    private readonly IntPtr _thread;
    private readonly IntPtr _pseudoConsole;
    private int _disposed;
    private int _consoleClosed;

    private ConPtyProcess(
        SafeFileHandle inputWrite,
        SafeFileHandle outputRead,
        IntPtr process,
        IntPtr thread,
        IntPtr pseudoConsole)
    {
        _inputWrite = inputWrite;
        _outputRead = outputRead;
        _process = process;
        _thread = thread;
        _pseudoConsole = pseudoConsole;
        Input = new FileStream(inputWrite, FileAccess.Write, 4096, isAsync: false);
        Output = new FileStream(outputRead, FileAccess.Read, 4096, isAsync: false);
    }

    public Stream Input { get; }
    public Stream Output { get; }
    public event Action<int>? Exited;

    public static ConPtyProcess Start(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        short columns,
        short rows)
    {
        var commandLine = new StringBuilder(Quote(executable) + " " + string.Join(" ", arguments.Select(Quote)), 32768);
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1
        };

        if (!CreatePipe(out var inputRead, out var inputWrite, ref sa, 0))
            throw new InvalidOperationException("CreatePipe input failed: " + Marshal.GetLastWin32Error());
        if (!CreatePipe(out var outputRead, out var outputWrite, ref sa, 0))
        {
            CloseHandle(inputRead);
            CloseHandle(inputWrite);
            throw new InvalidOperationException("CreatePipe output failed: " + Marshal.GetLastWin32Error());
        }

        // Parent ends must not be inherited, or the child keeps the pipes open.
        SetHandleInformation(inputWrite, HandleFlagInherit, 0);
        SetHandleInformation(outputRead, HandleFlagInherit, 0);

        var size = new COORD { X = columns, Y = rows };
        var hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out var pseudoConsole);
        if (hr != 0)
        {
            CloseHandle(inputRead);
            CloseHandle(inputWrite);
            CloseHandle(outputRead);
            CloseHandle(outputWrite);
            throw new InvalidOperationException("CreatePseudoConsole failed: 0x" + hr.ToString("X8"));
        }

        var attributeList = IntPtr.Zero;
        try
        {
            attributeList = CreateAttributeList(pseudoConsole);
            var startup = new STARTUPINFOEX
            {
                StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() },
                lpAttributeList = attributeList
            };

            if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ExtendedStartupInfoPresent,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startup,
                    out var processInfo))
            {
                throw new InvalidOperationException("CreateProcess failed: " + Marshal.GetLastWin32Error());
            }

            CloseHandle(inputRead);
            CloseHandle(outputWrite);
            inputRead = IntPtr.Zero;
            outputWrite = IntPtr.Zero;

            var session = new ConPtyProcess(
                new SafeFileHandle(inputWrite, ownsHandle: true),
                new SafeFileHandle(outputRead, ownsHandle: true),
                processInfo.hProcess,
                processInfo.hThread,
                pseudoConsole);
            inputWrite = IntPtr.Zero;
            outputRead = IntPtr.Zero;
            pseudoConsole = IntPtr.Zero;
            session.WatchExit();
            return session;
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
            if (inputRead != IntPtr.Zero) CloseHandle(inputRead);
            if (inputWrite != IntPtr.Zero) CloseHandle(inputWrite);
            if (outputRead != IntPtr.Zero) CloseHandle(outputRead);
            if (outputWrite != IntPtr.Zero) CloseHandle(outputWrite);
            if (pseudoConsole != IntPtr.Zero) ClosePseudoConsole(pseudoConsole);
        }
    }

    public void Kill()
    {
        if (_process != IntPtr.Zero)
            TerminateProcess(_process, 1);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try { Input.Dispose(); } catch { /* closed */ }
        try { Output.Dispose(); } catch { /* closed */ }
        CloseConsole();
        if (_thread != IntPtr.Zero)
            CloseHandle(_thread);
        if (_process != IntPtr.Zero)
            CloseHandle(_process);
    }

    private void WatchExit()
    {
        if (!DuplicateHandle(GetCurrentProcess(), _process, GetCurrentProcess(), out var process, 0, false, DuplicateSameAccess))
            process = _process;

        _ = Task.Run(() =>
        {
            WaitForSingleObject(process, Infinite);
            GetExitCodeProcess(process, out var code);
            if (process != _process)
                CloseHandle(process);
            Exited?.Invoke(unchecked((int)code));
            // Closing the session flushes the last output and ends the read pipe.
            CloseConsole();
        });
    }

    private void CloseConsole()
    {
        if (Interlocked.Exchange(ref _consoleClosed, 1) != 0)
            return;
        if (_pseudoConsole != IntPtr.Zero)
            ClosePseudoConsole(_pseudoConsole);
    }

    private static IntPtr CreateAttributeList(IntPtr pseudoConsole)
    {
        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var list = Marshal.AllocHGlobal(size);
        if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
        {
            Marshal.FreeHGlobal(list);
            throw new InvalidOperationException("InitializeProcThreadAttributeList failed: " + Marshal.GetLastWin32Error());
        }

        // Windows reads the HPCON from the pointer value itself.
        if (!UpdateProcThreadAttribute(
                list,
                0,
                PseudoConsoleAttribute,
                pseudoConsole,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
        {
            DeleteProcThreadAttributeList(list);
            Marshal.FreeHGlobal(list);
            throw new InvalidOperationException("UpdateProcThreadAttribute failed: " + Marshal.GetLastWin32Error());
        }

        return list;
    }

    private static string Quote(string value)
    {
        if (value.Length == 0)
            return "\"\"";
        if (value.IndexOfAny([' ', '\t', '"']) < 0)
            return value;

        var builder = new System.Text.StringBuilder();
        builder.Append('"');
        var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                slashes++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', slashes * 2 + 1);
                builder.Append('"');
                slashes = 0;
                continue;
            }

            if (slashes > 0)
            {
                builder.Append('\\', slashes);
                slashes = 0;
            }

            builder.Append(ch);
        }

        if (slashes > 0)
            builder.Append('\\', slashes * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private const uint HandleFlagInherit = 0x00000001;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint Infinite = 0xFFFFFFFF;
    private const uint DuplicateSameAccess = 2;
    private static readonly IntPtr PseudoConsoleAttribute = new(0x00020016);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        IntPtr hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr lpTargetHandle,
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwOptions);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
}
