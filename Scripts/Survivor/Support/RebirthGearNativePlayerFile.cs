using System;
using System.IO;

// Detached exact final native file. This loader grants no gameplay permission;
// callers separately bind creation, saved world, phase and original inventory.
internal static class RebirthGearNativePlayerFile
{
    internal static bool TryRead(RebirthStablePlayerIdentity owner,out PlayerDataFile saved)
        =>TryRead(owner,out saved,out _);
    internal static bool TryRead(RebirthStablePlayerIdentity owner,out PlayerDataFile saved,out string failure)
    {
        failure="owner";saved=null;
        if(owner==null||string.IsNullOrEmpty(owner.CanonicalId))return false;
        try
        {
            failure="directory";string canonical=owner.CanonicalId;
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            string name=canonical+"."+PlayerDataFile.EXT;
            failure="filename";if(Path.GetFileName(name)!=name)return false;
            string path=Path.GetFullPath(Path.Combine(root,name));
            failure="path-or-missing-final";if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)||!SdFile.Exists(path))return false;
            failure="open-read";var data=new PlayerDataFile();
            using(var input=SdFile.OpenRead(path))
            using(var stream=new MemoryStream())
            {
                var buffer=new byte[8192];int read;
                while((read=input.Read(buffer,0,buffer.Length))>0)
                {
                    failure="size-bound";if(stream.Length+read>16*1024*1024)return false;
                    stream.Write(buffer,0,read);
                }
                stream.Position=0;
                using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    failure="magic";if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0')return false;
                    uint version=reader.ReadByte();
                    failure="version";if(version!=PlayerDataFile.cFileVersion)return false;
                    failure="native-read";data.Read(reader,version,StreamModeRead.Persistency);
                    failure="trailing-bytes";if(stream.Position!=stream.Length)return false;
                }
            }
            failure="scope-after-read";if(owner.CanonicalId!=canonical||Path.GetFullPath(GameIO.GetPlayerDataDir())!=root)return false;
            saved=data;failure=null;return true;
        }
        catch {return false;}
    }
}
