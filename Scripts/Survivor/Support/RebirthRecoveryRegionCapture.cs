using System;
using System.Globalization;
using System.IO;
using System.Threading;

// Captures saved bytes only; caller supplies authenticated world/owner binding.
// Never creates a missing region, loads chunks into the world or changes native queues.
public static class RebirthRecoveryRegionCapture
{
    public static bool TryCapture(string savedRegionDirectory,int regionX,int regionZ,int localX,int localZ,
        Func<bool> isCurrent,out byte[] frame)
    {
        frame=null;
        if(string.IsNullOrWhiteSpace(savedRegionDirectory)||isCurrent==null)return false;
        int thread=Thread.CurrentThread.ManagedThreadId;
        try
        {
            string root=Path.GetFullPath(savedRegionDirectory);
            string name="r."+regionX.ToString(CultureInfo.InvariantCulture)+"."+regionZ.ToString(CultureInfo.InvariantCulture)+".7rg";
            string path=Path.GetFullPath(Path.Combine(root,name));
            if(Path.GetDirectoryName(path)!=root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)||
                !isCurrent()||!File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
            byte[] captured;
            // Read sharing denies cooperating writer/delete handles for this capture.
            // An existing writer makes this attempt pending; never retry by opening RW.
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(Thread.CurrentThread.ManagedThreadId!=thread||!isCurrent()||
                    !RebirthRecoverySectorFrame.TryRead(input,localX,localZ,out captured)||!isCurrent())return false;
            }
            if(Thread.CurrentThread.ManagedThreadId!=thread||!isCurrent())return false;
            frame=captured;return true;
        }
        catch{return false;}
    }
}