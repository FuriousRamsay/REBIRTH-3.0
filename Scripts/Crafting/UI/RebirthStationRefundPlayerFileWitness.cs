using System;
using System.IO;

// Final native player file only. Never invoke Load: it may silently use .bak.
internal static class RebirthStationRefundPlayerFileWitness
{
    internal static bool HasSaved(RebirthStablePlayerIdentity owner,Guid delivery,RebirthStationRefundBackpackPlan plan)
    {
        if(owner==null||delivery==Guid.Empty||plan==null||string.IsNullOrEmpty(owner.CanonicalId))return false;
        try
        {
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            string name=owner.CanonicalId+"."+PlayerDataFile.EXT;
            if(Path.GetFileName(name)!=name)return false;
            string path=Path.GetFullPath(Path.Combine(root,name));
            if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!SdFile.Exists(path))return false;
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
            if(saved.buffData==null||saved.bagData==null||!RebirthStationRefundReceiptReader.Contains(saved.buffData.ToArray(),delivery,true)||
                saved.bagData.Length==0||saved.bagData.Length>2*1024*1024)return false;
            using(var stream=new MemoryStream(saved.bagData.ToArray(),false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadByte()!=Bag.Version)return false;
                stream.Position=0;
                var bag=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);
                return stream.Position==stream.Length&&plan.MatchesAfter(bag?.ItemGrid?.items);
            }
        }
        catch{return false;}
    }
}