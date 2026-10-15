using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Native player-file receipt evidence only. Parsing never delivers rewards.
internal static class RebirthCamperStationNativeRewardReceipt
{
    internal static string Key(RebirthStationGridAdmission admission)
    {
        if(admission==null||!admission.IsPublicationAttempted||!admission.TryGetMobileAddress(out _))
            throw new ArgumentException("Attempted mobile admission required.");
        using(var hash=SHA256.Create())
            return "rbCamperCraft_"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                admission.Write().ToString(SaveOptions.DisableFormatting)))).Replace("-","");
    }
    internal static bool Contains(byte[] bytes,string key,bool applied)
        =>TryRead(bytes,key,out var phase)&&phase==(applied?1f:2f);
    // False means unknown/invalid. A successfully parsed absent key means0,
    // which must never be inferred from a missing or unreadable player file.
    internal static bool TryRead(byte[] bytes,string key,out float phase)
    {
        phase=float.NaN;
        if(bytes==null||bytes.Length==0||bytes.Length>1024*1024||key==null||!key.StartsWith("rbCamperCraft_",StringComparison.Ordinal)||key.Length!=78)return false;
        for(int i=14;i<key.Length;i++)if(!Uri.IsHexDigit(key[i]))return false;
        try
        {
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
                int buffs=reader.ReadUInt16();for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
                int variables=reader.ReadUInt16();bool found=false;float result=0f;
                for(int i=0;i<variables;i++)
                {
                    string name=reader.ReadString();float value=reader.ReadSingle();
                    if(name!=key)continue;
                    if(found||value!=0f&&value!=1f&&value!=2f)return false;found=true;result=value;
                }
                if(stream.Position!=stream.Length)return false;
                phase=result;return true;
            }
        }
        catch{return false;}
    }
    internal static bool HasSaved(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission,bool applied)
        =>TryReadSaved(identity,admission,out var phase)&&phase==(applied?1f:2f);
    internal static bool TryReadSaved(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission,out float phase)
    {
        phase=float.NaN;
        if(!ThreadManager.IsMainThread()||identity==null||string.IsNullOrEmpty(identity.CanonicalId))return false;
        try
        {
            string key=Key(admission),root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            string name=identity.CanonicalId+"."+PlayerDataFile.EXT;
            if(Path.GetFileName(name)!=name)return false;
            string path=Path.GetFullPath(Path.Combine(root,name));
            if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,
                Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)||!SdFile.Exists(path))return false;
            var saved=new PlayerDataFile();
            using(var input=SdFile.OpenRead(path))
            using(var stream=new MemoryStream())
            {
                var buffer=new byte[8192];int read;
                while((read=input.Read(buffer,0,buffer.Length))>0)
                {if(stream.Length+read>16*1024*1024)return false;stream.Write(buffer,0,read);}
                stream.Position=0;
                using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0')return false;
                    uint version=reader.ReadByte();if(version!=PlayerDataFile.cFileVersion)return false;
                    saved.Read(reader,version,StreamModeRead.Persistency);
                    if(stream.Position!=stream.Length)return false;
                }
            }
            return saved.buffData!=null&&TryRead(saved.buffData.ToArray(),key,out phase);
        }
        catch{return false;}
    }
}
