param(
    [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$GameProcessId,
    [Parameter(Mandatory=$true)][string]$ExpectedExecutable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if(-not [Environment]::Is64BitProcess){throw 'A 64-bit PowerShell host is required for the 64-bit game snapshot.'}
# Diagnostic only: no launch, input, window messages, process exit or sampler control.
# Invoked as a separate bounded child by measure-capt so a stuck dump cannot block cleanup.
$gameProcess=Get-Process -Id $GameProcessId
if($gameProcess.ProcessName -ne '7DaysToDie'){throw 'Snapshot target is not 7DaysToDie.'}
$actualExecutable=[IO.Path]::GetFullPath($gameProcess.Path)
if($actualExecutable -ine [IO.Path]::GetFullPath($ExpectedExecutable)){throw 'Snapshot target executable changed.'}
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
$statusPath=Join-Path $OutputDirectory 'status.json'
$dumpPath=Join-Path $OutputDirectory ('game-'+$GameProcessId+'.dmp')
$status=[ordered]@{pid=$GameProcessId;exe=$actualExecutable;startedUtc=$gameProcess.StartTime.ToUniversalTime().ToString('o');capturedUtc=[DateTime]::UtcNow.ToString('o');stage='dump-start';dump=$dumpPath;dumpError=$null;metadataError=$null}
$status|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $statusPath -Encoding UTF8
try {
    if(-not ('RebirthStartupHangSnapshotV1' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class RebirthStartupHangSnapshotV1 {
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int id);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("dbghelp.dll",SetLastError=true)] static extern bool MiniDumpWriteDump(IntPtr process,uint id,SafeFileHandle file,uint type,IntPtr exception,IntPtr streams,IntPtr callback);
    public static void Write(int id,string path) {
        IntPtr process=OpenProcess(0x0400|0x0010,false,id); // query information + VM read
        if(process==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"OpenProcess for startup snapshot");
        try {
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read)) {
                // Normal thread contexts/stacks + thread info, memory-region map, unloaded modules.
                // No full-memory or indirectly referenced heap capture.
                if(!MiniDumpWriteDump(process,(uint)id,file.SafeFileHandle,0x1000|0x0800|0x0020,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(),"MiniDumpWriteDump startup snapshot");
            }
        } finally {CloseHandle(process);}
    }
}
'@
    }
    [RebirthStartupHangSnapshotV1]::Write($GameProcessId,$dumpPath)
    $status.stage='dump-complete'
} catch {$status.stage='dump-failed';$status.dumpError=$_.Exception.ToString()}
$status|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $statusPath -Encoding UTF8
try {
    $gameProcess.Refresh()
    $threads=@(foreach($thread in $gameProcess.Threads){
        $row=[ordered]@{id=$thread.Id;state=$null;waitReason=$null;cpuMs=$null;startAddress=$null;error=$null}
        try {$row.state=$thread.ThreadState.ToString();if($thread.ThreadState -eq [Diagnostics.ThreadState]::Wait){$row.waitReason=$thread.WaitReason.ToString()};$row.cpuMs=$thread.TotalProcessorTime.TotalMilliseconds;$row.startAddress=$thread.StartAddress.ToInt64().ToString('X16')}
        catch {$row.error=$_.Exception.Message}
        [pscustomobject]$row
    })
    $threads|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'threads.json') -Encoding UTF8
    $modules=@(foreach($module in $gameProcess.Modules){[pscustomobject]@{name=$module.ModuleName;path=$module.FileName;base=$module.BaseAddress.ToInt64().ToString('X16');size=$module.ModuleMemorySize}})
    $modules|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'modules.json') -Encoding UTF8
} catch {$status.metadataError=$_.Exception.ToString()}
$status.stage='finished'
$status|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $statusPath -Encoding UTF8
Write-Output ('Startup diagnostic evidence: '+$OutputDirectory)
if($status.dumpError){exit 1}
