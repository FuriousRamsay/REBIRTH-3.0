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
            if(!TryReadOwnerFile(owner,out var saved))return false;
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
    // Historical applied receipt only. Native application sets1 only after
    // observing exact credit; subsequent legitimate inventory use is permitted.
    // Cold callers still authenticate exact owner/delivery/cancellation records.
    internal static bool HasSavedReceipt(RebirthStablePlayerIdentity owner,Guid delivery)
    {
        if(!ThreadManager.IsMainThread()||delivery==Guid.Empty)return false;
        return TryReadOwnerFile(owner,out var saved)&&saved.buffData!=null&&
            RebirthStationRefundReceiptReader.Contains(saved.buffData.ToArray(),delivery,true);
    }
    internal static bool HasSavedBefore(RebirthStablePlayerIdentity owner,Guid delivery,RebirthStationRefundBackpackPlan plan)
        =>HasSavedBeforeCore(owner,delivery,plan,false);
    internal static bool TryReadSavedPhase(RebirthStablePlayerIdentity owner,Guid delivery,out bool present,out float phase)
    {
        present=false;phase=0f;
        return ThreadManager.IsMainThread()&&TryReadOwnerFile(owner,out var saved)&&saved.buffData!=null&&
            RebirthStationRefundReceiptReader.TryReadPhase(saved.buffData.ToArray(),delivery,out present,out phase);
    }
    // Detached authenticated native snapshot for server planning; never a permit.
    internal static bool TryCopySavedBag(RebirthStablePlayerIdentity owner,out Bag bag)
    {
        bag=null;
        try
        {
            if(!ThreadManager.IsMainThread()||!TryReadOwnerFile(owner,out var saved)||saved.bagData==null||
                saved.bagData.Length==0||saved.bagData.Length>2*1024*1024)return false;
            using(var stream=new MemoryStream(saved.bagData.ToArray(),false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadByte()!=Bag.Version)return false;stream.Position=0;
                var parsed=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);
                if(stream.Position!=stream.Length||parsed?.ItemGrid?.items==null)return false;
                bag=parsed;return true;
            }
        }
        catch{return false;}
    }
    internal static bool HasSavedMissingBefore(RebirthStablePlayerIdentity owner,Guid delivery,RebirthStationRefundBackpackPlan plan)
        =>HasSavedBeforeCore(owner,delivery,plan,true);
    private static bool HasSavedBeforeCore(RebirthStablePlayerIdentity owner,Guid delivery,RebirthStationRefundBackpackPlan plan,bool missing)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||delivery==Guid.Empty||plan==null||!TryReadOwnerFile(owner,out var saved)||
                saved.buffData==null||saved.bagData==null||saved.bagData.Length==0||saved.bagData.Length>2*1024*1024||
                !RebirthStationRefundReceiptReader.TryReadPhase(saved.buffData.ToArray(),delivery,out var present,out var phase)||
                (missing?present:!present||phase!=3f))return false;
            using(var stream=new MemoryStream(saved.bagData.ToArray(),false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadByte()!=Bag.Version)return false;
                stream.Position=0;
                var bag=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);
                var slots=bag?.ItemGrid?.items;
                return stream.Position==stream.Length&&slots!=null&&plan.MatchesBefore(slots,slots.Length,
                    slot=>bag.LockedSlots!=null&&slot<bag.LockedSlots.Length&&bag.LockedSlots[slot]);
            }
        }
        catch{return false;}
    }
    private static bool TryReadOwnerFile(RebirthStablePlayerIdentity owner,out PlayerDataFile saved)
    {
        saved=null;
        if(owner==null||string.IsNullOrEmpty(owner.CanonicalId))return false;
        try
        {
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            string name=owner.CanonicalId+"."+PlayerDataFile.EXT;
            if(Path.GetFileName(name)!=name||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)return false;
            string path=Path.GetFullPath(Path.Combine(root,name));
            if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!SdFile.Exists(path))return false;
            var candidate=new PlayerDataFile();
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
                    candidate.Read(reader,version,StreamModeRead.Persistency);
                    if(stream.Position!=stream.Length)return false;
                }
            }
            saved=candidate;return true;
        }
        catch{return false;}
    }
}
