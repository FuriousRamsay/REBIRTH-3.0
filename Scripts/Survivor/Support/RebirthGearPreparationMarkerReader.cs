using System;

// Detached native buff blob only. No live EntityBuffs.Read or stat callbacks.
// Native owner-file/current-world qualification remains the caller's obligation.
public static class RebirthGearPreparationMarkerReader
{
    // A fully parsed final blob with no member of the original-intent family.
    // Zero-valued, malformed, unknown-version and case-variant family keys are
    // still present and cannot certify retirement. Caller authenticates the file.
    public static bool HasNoOriginal(byte[] bytes)
    {
        if(bytes==null||bytes.Length==0||bytes.Length>1024*1024)return false;
        try
        {
            using(var stream=new System.IO.MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();
                if(version!=EntityBuffs.Version||version<2)return false;
                int buffs=reader.ReadUInt16();
                for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
                int variables=reader.ReadUInt16();bool present=false;
                for(int i=0;i<variables;i++)
                {
                    string key=reader.ReadString();reader.ReadSingle();
                    if(key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))present=true;
                }
                return !present&&stream.Position==stream.Length;
            }
        }
        catch{return false;}
    }
    public static bool TryRead(byte[] bytes,out string marker,out Guid world,out int ownedBeltSlots,out RebirthGearPreparationIntent intent)
    {
        marker=null;world=Guid.Empty;ownedBeltSlots=0;intent=null;
        if(bytes==null||bytes.Length==0||bytes.Length>1024*1024)return false;
        try
        {
            using(var stream=new System.IO.MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();
                if(version!=EntityBuffs.Version||version<2)return false;
                int buffs=reader.ReadUInt16();
                for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
                int variables=reader.ReadUInt16();string found=null;Guid savedWorld=Guid.Empty;
                int owned=0;RebirthGearPreparationIntent original=null;
                for(int i=0;i<variables;i++)
                {
                    string key=reader.ReadString();float value=reader.ReadSingle();
                    if(!key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))continue;
                    if(found!=null||!RebirthGearPreparationMarker.TryRead(key,value,out savedWorld,out owned,out original))return false;
                    found=key;
                }
                if(found==null||stream.Position!=stream.Length)return false;
                marker=found;world=savedWorld;ownedBeltSlots=owned;intent=original;return true;
            }
        }
        catch {return false;}
    }
}