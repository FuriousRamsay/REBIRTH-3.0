using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RebirthProfiler;

/// <summary>
/// Starts 7 Days To Die suspended, loads the native sampling module into it, runs the module's
/// init export (Mono only allows sampling to be enabled before the runtime has started), then resumes the game.
/// Nothing is written to the game folder or the mod.
/// </summary>
static class GameLauncher
{
    const string ModuleName = "mono-profiler-rebirthprof.dll";
    const string InitExport = "RebirthProfInit";

    public static string FindGameExe(string startDir)
    {
        for (var d = new DirectoryInfo(startDir); d != null; d = d.Parent)
        {
            var exe = Path.Combine(d.FullName, "7DaysToDie.exe");
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    public static string FindModule(string appDir)
    {
        foreach (var rel in new[] { ModuleName, Path.Combine("..", "native", ModuleName), Path.Combine("native", ModuleName) })
        {
            var p = Path.GetFullPath(Path.Combine(appDir, rel));
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static void T(string s) { try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "..", "out", "launcher.log"), s + Environment.NewLine); } catch { } }

    /// <returns>the game's process id; throws with a readable message on failure.</returns>

    public static uint LastMainThreadId { get; private set; }
    /// <summary>Also sample the game worker threads (pathfinding, jobs, AI). Off by default: it briefly suspends busy threads.</summary>
    public static bool SampleThreads;

    public static int Launch(string gameExe, string arguments, string modulePath, string outputPath, bool trackAllocations)
    {
        Environment.SetEnvironmentVariable("PROFILER_OUT", outputPath);
        Environment.SetEnvironmentVariable("PROFILER_ALLOC", trackAllocations ? "1" : "0");
        Environment.SetEnvironmentVariable("PROFILER_THREADS", SampleThreads ? "1" : "0");
        T("launch begin");
        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        var cmd = $"\"{gameExe}\" {arguments}";
        if (!CreateProcessW(gameExe, cmd, IntPtr.Zero, IntPtr.Zero, false, 0x4 /*CREATE_SUSPENDED*/, IntPtr.Zero,
                Path.GetDirectoryName(gameExe), ref si, out var pi))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start the game");

        T("process created");
        try
        {
            T("loadlibrary remote");
            RunRemote(pi.hProcess, GetProcAddress(GetModuleHandleW("kernel32.dll"), "LoadLibraryW"), modulePath, out var loadResult);
            if (loadResult == 0) throw new InvalidOperationException("The game process refused to load the profiler module (is the C++ runtime installed?).");

            // Find the module's remote base address to compute the address of the init export.
            IntPtr remoteBase = IntPtr.Zero;
            for (int i = 0; i < 50 && remoteBase == IntPtr.Zero; i++)
            {
                try
                {
                    foreach (ProcessModule m in Process.GetProcessById((int)pi.dwProcessId).Modules)
                        if (m.ModuleName.Equals(ModuleName, StringComparison.OrdinalIgnoreCase)) { remoteBase = m.BaseAddress; break; }
                }
                catch (Win32Exception) { }
                if (remoteBase == IntPtr.Zero) Thread.Sleep(20);
            }
            if (remoteBase == IntPtr.Zero) throw new InvalidOperationException("Could not locate the profiler module inside the game.");

            T("remote base found");
            var local = NativeLibrary.Load(modulePath);
            var initOffset = (long)NativeLibrary.GetExport(local, InitExport) - (long)local;
            NativeLibrary.Free(local);

            T("init remote");
            RunRemote(pi.hProcess, (IntPtr)((long)remoteBase + initOffset), null, out var initResult, (IntPtr)pi.dwThreadId);
            if (initResult != 0)
                throw new InvalidOperationException($"The sampler could not start (code {initResult}). See {outputPath}.log");

            T("resume");
            if (ResumeThread(pi.hThread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not resume the game");
            LastMainThreadId = pi.dwThreadId;
            return (int)pi.dwProcessId;
        }
        catch
        {
            TerminateProcess(pi.hProcess, 1);
            throw;
        }
        finally
        {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
        }
    }

    /// <summary>
    /// Starts the game normally (nothing loaded into it) for benchmark runs, so the measurement is not disturbed.
    /// The main thread is taken to be the process's oldest thread.
    /// </summary>
    public static int LaunchPlain(string gameExe, string arguments, out uint mainThreadId)
    {
        var p = Process.Start(new ProcessStartInfo(gameExe, arguments) { WorkingDirectory = Path.GetDirectoryName(gameExe), UseShellExecute = false })
                ?? throw new InvalidOperationException("Could not start the game");
        mainThreadId = 0;
        for (int i = 0; i < 30 && mainThreadId == 0; i++)
        {
            try
            {
                p.Refresh();
                var first = p.Threads.Cast<ProcessThread>().OrderBy(t => t.StartTime).FirstOrDefault();
                if (first != null) mainThreadId = (uint)first.Id;
            }
            catch (Exception) { }
            if (mainThreadId == 0) Thread.Sleep(100);
        }
        return p.Id;
    }

    static void RunRemote(IntPtr process, IntPtr function, string wideStringArg, out uint exitCode, IntPtr rawArg = default)
    {
        IntPtr arg = rawArg;
        if (wideStringArg != null)
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes(wideStringArg + "\0");
            arg = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)bytes.Length, 0x3000, 0x04);
            if (arg == IntPtr.Zero || !WriteProcessMemory(process, arg, bytes, (UIntPtr)bytes.Length, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not write to the game process");
        }
        var t = CreateRemoteThread(process, IntPtr.Zero, UIntPtr.Zero, function, arg, 0, out _);
        if (t == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start a thread in the game process");
        WaitForSingleObject(t, 20000);
        GetExitCodeThread(t, out exitCode);
        CloseHandle(t);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb; public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public uint dwProcessId, dwThreadId; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessW(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr t);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr p, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, UIntPtr size, out UIntPtr written);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateRemoteThread(IntPtr p, IntPtr attr, UIntPtr stack, IntPtr fn, IntPtr arg, uint flags, out uint id);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeThread(IntPtr h, out uint code);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr m, string name);
}
