using System;
using System.IO;

// Detached exact final native file. This loader grants no gameplay permission;
// callers separately bind creation, saved world, phase and original inventory.
internal static class RebirthGearNativePlayerFile
{
    internal static bool TryRead(RebirthStablePlayerIdentity owner,out PlayerDataFile saved)
    {
        saved=null;
        if(owner==null||string.IsNullOrEmpty(owner.CanonicalId))return false;
        try
        {
            string canonical=owner.CanonicalId;
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            string name=canonical+"."+PlayerDataFile.EXT;
            if(Path.GetFileName(name)!=name)return false;
            string path=Path.GetFullPath(Path.Combine(root,name));
            if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)||!SdFile.Exists(path))return false;
            var data=new PlayerDataFile();
            using(var input=SdFile.OpenRead(path))
            using(var stream=new MemoryStream())
            {
                var buffer=new byte[8192];int read;
                while((read=input.Read(buffer,0,buffer.Length))>0)
                {
                    if(stream.Length+read>16*1024*1024)return false;
                    stream.Write(buffer,0,read);
                }
                stream.Position=0;
                using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0')return false;
                    uint version=reader.ReadByte();
                    if(version!=PlayerDataFile.cFileVersion)return false;
                    data.Read(reader,version,StreamModeRead.Persistency);
                    if(stream.Position!=stream.Length)return false;
                }
            }
            if(owner.CanonicalId!=canonical||Path.GetFullPath(GameIO.GetPlayerDataDir())!=root)return false;
            saved=data;return true;
        }
        catch {return false;}
    }
}