using System;
using System.IO;

public static class RebirthCamperSavedCursor
{
    // Save the current native inventory after complete publication. Cursor
    // return on window close is legitimate; never reconstruct or replay it.
    internal static bool SaveAndVerifyPublishedLocal(EntityPlayerLocal player,Guid receipt,Func<bool> current)
    {
        var manager=GameManager.Instance;
        if(!ThreadManager.IsMainThread() || receipt==Guid.Empty || manager?.World==null || manager.World.IsRemote()
            || player==null || !ReferenceEquals(manager.World.GetPrimaryPlayer(),player)
            || current==null || !current() || player.Buffs.GetCustomVar("rbCamper_"+receipt.ToString("N"))!=1f)return false;
        try
        {
            var identity=manager.getPersistentPlayerID(null);
            if(identity==null)return false;
            string owner=identity.CombinedString;
            manager.SaveLocalPlayerData();
            return current() && ReferenceEquals(manager.World.GetPrimaryPlayer(),player)
                && manager.getPersistentPlayerID(null)?.CombinedString==owner
                && VerifyPublishedReceipt(owner,receipt,current);
        }
        catch(Exception error){Trace(player,receipt,"owner-retry-exception."+error.GetType().Name);return false;}
    }
    public static bool SaveAndVerifyLocal(EntityPlayerLocal player,Guid receipt,ItemStack expected,Func<bool> currentSession)
    {
        var manager=GameManager.Instance;
        if(!ThreadManager.IsMainThread() || manager==null || manager.World==null || manager.World.IsRemote()
            || player==null || !ReferenceEquals(manager.World.GetPrimaryPlayer(),player)
            || currentSession==null || !currentSession())return false;
        try
        {
            // Resolve exactly the same server-owned identity used by the native
            // SaveLocalPlayerData implementation, never a caller-supplied path.
            var identity=manager.getPersistentPlayerID(null);
            if(identity==null){Trace(player,receipt,"owner-identity-missing");return false;}
            string owner=identity.CombinedString;
            manager.SaveLocalPlayerData();
            if(!currentSession() || !ReferenceEquals(manager.World.GetPrimaryPlayer(),player)
                || manager.getPersistentPlayerID(null)?.CombinedString!=owner)
            {Trace(player,receipt,"owner-session-changed");return false;}
            bool verified=Verify(owner,receipt,expected,currentSession);
            Trace(player,receipt,verified?"owner-save-verified":"owner-save-witness-refused");
            return verified;
        }
        catch(Exception error){Trace(player,receipt,"owner-save-exception."+error.GetType().Name);return false;}
    }
    private static void Trace(EntityPlayerLocal player,Guid receipt,string stage)
    {
        if(!RebirthCamperTrace.Enabled)return;
        // Correlate with publish.begin by operation, without attributing the
        // event to unrelated pending vehicles or scanning world entities.
        RebirthCamperTrace.Write(stage,null,receipt.ToString("N"));
    }
    // Identity is supplied by authenticated connection/session code, never a UI
    // request. Caller also verifies the same character creation before and after.
    public static bool Verify(string authenticatedPlayerId, Guid receipt, ItemStack expected, Func<bool> currentSession)
        => VerifyNative(authenticatedPlayerId,receipt,expected,currentSession,true);

    // Remote native inventory trust: receipt 1 is published only after applying
    // the server candidate. Subsequent ordinary cursor moves/uses do not undo
    // that acknowledged debit. Scoped remote journal owners prevent local
    // uncertain-before-save receipts from entering this admission path.
    internal static bool VerifyPublishedReceipt(string authenticatedPlayerId, Guid receipt, Func<bool> currentSession,
        string receiptPrefix="rbCamper_")
        => VerifyNative(authenticatedPlayerId,receipt,null,currentSession,false,receiptPrefix);
#if !REBIRTH_CAMPER_DIAGNOSTICS
    // Immutable native PDS witness for a separately retained original sink.
    // Account comes from the actual attached sender; no current-shape fallback.
    internal static bool TryCopyPublishedOwner(string authenticatedPlayerId,Guid receipt,Func<bool> current,out byte[] image,out IDisposable retained)
    {
        image=null;retained=null;IDisposable memory=null;
        try
        {
            if(!VerifyPublishedReceipt(authenticatedPlayerId,receipt,current))return false;
            string path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),authenticatedPlayerId+"."+PlayerDataFile.EXT));
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!File.Exists(path))return false;
            long length=new FileInfo(path).Length;
            if(length<5||length>16*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+4*length),out memory))return false;
            byte[] bytes=File.ReadAllBytes(path);
            if(bytes.Length!=length||!VerifyPublishedOwnerImage(bytes,receipt)||!current()||
                !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(path,bytes))return false;
            image=bytes;retained=memory;memory=null;return true;
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
    internal static bool VerifyPublishedOwnerImage(byte[] image,Guid receipt)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||receipt==Guid.Empty||image==null||image.Length<5||image.Length>16*1024*1024)return false;
            var saved=new PlayerDataFile();
            using(var stream=new MemoryStream(image,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0'||reader.ReadByte()!=PlayerDataFile.cFileVersion)return false;
                saved.Read(reader,PlayerDataFile.cFileVersion,StreamModeRead.Persistency);
                return stream.Position==stream.Length&&saved.dragAndDropItem!=null&&saved.buffData!=null&&
                    HasPublishedReceipt(saved.buffData.ToArray(),receipt);
            }
        }
        catch{return false;}
    }
#endif

    private static bool VerifyNative(string authenticatedPlayerId,Guid receipt,ItemStack expected,Func<bool> currentSession,bool exactCursor,
        string receiptPrefix="rbCamper_")
    {
        if(!ThreadManager.IsMainThread() || receipt==Guid.Empty || string.IsNullOrWhiteSpace(authenticatedPlayerId) || exactCursor && expected==null
            || currentSession==null || !currentSession()
            || authenticatedPlayerId.IndexOfAny(Path.GetInvalidFileNameChars())>=0
            || authenticatedPlayerId=="." || authenticatedPlayerId=="..") return false;
        string directory=GameIO.GetPlayerDataDir();
        if(string.IsNullOrEmpty(directory))return false;
        try
        {
            string path=Path.Combine(directory,authenticatedPlayerId+"."+PlayerDataFile.EXT);
            if(!SdFile.Exists(path))return false;
            var saved=new PlayerDataFile();
            using(var stream=SdFile.OpenRead(path))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadChar()!='t' || reader.ReadChar()!='t' || reader.ReadChar()!='p' || reader.ReadChar()!='\0')return false;
                byte version=reader.ReadByte();if(version!=PlayerDataFile.cFileVersion)return false;
                saved.Read(reader,version,StreamModeRead.Persistency);
                if(stream.Position!=stream.Length)return false;
            }
            if(!currentSession() || directory!=GameIO.GetPlayerDataDir() || saved.dragAndDropItem==null)return false;
            if(saved.buffData==null || !HasPublishedReceipt(saved.buffData.ToArray(),receipt,receiptPrefix))return false;
            if(!exactCursor)return true;
            byte[] wanted=RebirthCamperInventorySnapshot.Capture(new[]{expected});
            byte[] actual=RebirthCamperInventorySnapshot.Capture(new[]{saved.dragAndDropItem});
            if(wanted.Length!=actual.Length)return false;
            for(int i=0;i<wanted.Length;i++)if(wanted[i]!=actual[i])return false;
            return true;
        }
        catch(Exception){return false;}
    }

    private static bool HasPublishedReceipt(byte[] bytes,Guid receipt,string receiptPrefix="rbCamper_")
    {
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            int version=reader.ReadByte();
            if(version!=EntityBuffs.Version || version<2)return false;
            int buffs=reader.ReadUInt16();
            for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
            int variables=reader.ReadUInt16();
            string wanted=receiptPrefix+receipt.ToString("N");
            bool found=false;
            for(int i=0;i<variables;i++)
            {
                string name=reader.ReadString();float value=reader.ReadSingle();
                if(name!=wanted)continue;
                if(found || value!=1f)return false;
                found=true;
            }
            return found && stream.Position==stream.Length;
        }
    }
}
