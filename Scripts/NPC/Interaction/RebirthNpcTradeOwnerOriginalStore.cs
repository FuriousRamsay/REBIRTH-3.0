using System;
using System.IO;
using System.Linq;
using System.Text;

// Owner-side immutable description retention. This file is not a native saved
// Before/After, a debit receipt or an NPC stock reservation. Those are separate
// protocol stages. An existing original is read before planning any new output.
internal static class RebirthNpcTradeOwnerOriginalStore
{
    private const int MaximumBytes=24*1024*1024,MaximumOriginals=512;
    // Read-only explicit Bridge observation; description/frame equality here
    // issues no setter grant, acknowledgement, save or release capability.
    internal static object CaptureAuditState(EntityPlayer actor,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||actor is not EntityPlayerLocal player||
            !TryReadRetained(player,operation,out var original))return new{schema=1,available=false};
        var quote=original.CopyQuote();
        bool before=original.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false);
        bool after=original.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,true);
        bool saved=RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out int step,out int phase);
        bool final=TryReadFinalDisposition(player,original,out bool unused);
        if(!IsRetained(player,original))return new{schema=1,available=false};
        return new{schema=1,available=true,operation=operation.ToString("N"),original=original.Digest,npc=quote.NpcId.ToString(),
            direction=quote.Direction==RebirthNpcTradeDirection.BuyFromNpc?"buy":"sell",item=quote.ItemKey,currency=quote.CurrencyItemKey,
            quantity=quote.Quantity,total=quote.TotalPrice,beforeMatches=before,afterMatches=after,savedStage=saved,step,phase,
            finalDisposition=final,unused=final&&unused};
    }
    internal static bool TryReadFinalDisposition(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out bool unused)
    {
        unused=false;
        try
        {
            if(!IsRetained(player,original)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
            string directory=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"REBIRTH","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),owner.StorageKey);
            string prefix=Path.Combine(directory,original.Operation.ToString("N"));
            if(!IsClosedHistory(prefix+".xml"))return false;
            unused=File.Exists(prefix+".unused");
            return IsRetained(player,original)&&IsClosedHistory(prefix+".xml")&&unused==File.Exists(prefix+".unused");
        }
        catch{return false;}
    }
    internal static bool TryReadRetained(EntityPlayerLocal player,Guid operation,out RebirthNpcTradeOriginal original)
    {
        original=null;
        try
        {
            if(!Current(player)||operation==Guid.Empty||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                !Guid.TryParse(player.world.worldState?.Guid,out var world))return false;
            string directory=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"REBIRTH","NpcTradeOriginals",world.ToString("N"),owner.StorageKey);
            var retained=Read(Path.Combine(directory,operation.ToString("N")+".xml"));
            if(retained.Operation!=operation||!IsRetained(player,retained))return false;original=retained;return true;
        }
        catch{return false;}
    }
    internal static bool TryReadAcknowledgement(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out string terminal)
    {
        terminal=null;
        try
        {
            if(!IsRetained(player,original)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
            string directory=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"REBIRTH","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),owner.StorageKey);
            string path=Path.Combine(directory,original.Operation.ToString("N")+".ack");SafePath(directory);
            if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)return false;
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            long length=stream.Length;if(length<1||length>4096)return false;
            using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,8192,true);string image=reader.ReadToEnd();
            if(stream.Length!=length||stream.Position!=length||Encoding.UTF8.GetByteCount(image)!=length||
                !RebirthNpcTradeOwnerReceipt.TryRead(image,out var operation,out var digest,out var savedTerminal,out bool closed)||
                closed||operation!=original.Operation||digest!=original.Digest||!IsRetained(player,original))return false;
            SafePath(directory);terminal=savedTerminal;return true;
        }
        catch{return false;}
    }
    internal static bool TryRetainReceipt(EntityPlayerLocal player,RebirthNpcTradeOriginal original,string purpose,string image)
    {
        try
        {
            if(purpose!="ack"&&purpose!="closed"&&purpose!="unused"||image==null||!IsRetained(player,original)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(image);if(bytes.Length<1||bytes.Length>4096)return false;
            string directory=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"REBIRTH","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),owner.StorageKey);
            SafePath(directory);string path=Path.Combine(directory,original.Operation.ToString("N")+"."+purpose);
            if(!File.Exists(path))
                using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            SafePath(directory);
            if((File.GetAttributes(path)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=bytes.Length)return false;
                byte[] actual=new byte[bytes.Length];int offset=0;
                while(offset<actual.Length){int read=stream.Read(actual,offset,actual.Length-offset);if(read<=0)return false;offset+=read;}
                if(stream.ReadByte()!=-1||!actual.SequenceEqual(bytes))return false;
            }
            return IsRetained(player,original);
        }
        catch{return false;}
    }
    internal static bool IsRetained(EntityPlayerLocal player,RebirthNpcTradeOriginal original)
    {
        try
        {
            // This establishes only retained description ownership. Original
            // NativeId stays historical; actual native stage/grant producers
            // separately qualify the current actor and independently saved ID.
            if(!Current(player)||original==null||player.Died!=original.Deaths||
                player.world.worldState?.Guid!=original.World||RebirthCamperCharacterAccess.CurrentCreation(player)!=original.Creation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=original.Account||
                !Guid.TryParse(original.World,out var world))return false;
            string selected=GameIO.GetPlayerDataDir();
            if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected))return false;
            string directory=Path.Combine(Path.GetFullPath(selected),"REBIRTH","NpcTradeOriginals",world.ToString("N"),owner.StorageKey);
            return Read(Path.Combine(directory,original.Operation.ToString("N")+".xml")).Image==original.Image;
        }
        catch{return false;}
    }
    internal static bool TryRetain(EntityPlayerLocal player,RebirthNpcTradeQuote quote,out RebirthNpcTradeOriginal original)
    {
        original=null;
        try
        {
            if(!Current(player)||quote==null||quote.QuoteId==Guid.Empty||
                quote.EndpointId!="player:"+player.entityId||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(player),world=player.world.worldState?.Guid;
            if(string.IsNullOrWhiteSpace(creation)||!Guid.TryParse(world,out var worldId))return false;
            var nativeWorld=player.world;var nativeBag=player.bag;var nativeBelt=player.inventory;int deaths=player.Died,id=player.entityId;
            bool SameScope()=>Current(player)&&ReferenceEquals(nativeWorld,player.world)&&ReferenceEquals(nativeBag,player.bag)&&
                ReferenceEquals(nativeBelt,player.inventory)&&player.Died==deaths&&player.entityId==id&&
                player.world.worldState?.Guid==world&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var actual)&&actual.CanonicalId==owner.CanonicalId;
            string selectedDirectory=GameIO.GetPlayerDataDir();
            if(string.IsNullOrWhiteSpace(selectedDirectory)||!Path.IsPathRooted(selectedDirectory))return false;
            string nativeDirectory=Path.GetFullPath(selectedDirectory);
            string directory=Path.Combine(nativeDirectory,"REBIRTH","NpcTradeOriginals",worldId.ToString("N"),owner.StorageKey);
            bool CurrentRoot()=>SameScope()&&Path.GetFullPath(GameIO.GetPlayerDataDir())==nativeDirectory;
            SafePath(directory);if(!CurrentRoot())return false;
            string path=Path.Combine(directory,quote.QuoteId.ToString("N")+".xml");
            if(File.Exists(path))
            {
                var retained=Read(path);
                if(!CurrentRoot()||retained.Account!=owner.CanonicalId||retained.Creation!=creation||retained.World!=world||
                    retained.Deaths!=deaths||retained.NativeId!=id||!retained.MatchesQuote(quote))return false;
                // Do not replan/reseed even when current native cells differ.
                // The owning protocol must classify and retain that state.
                original=retained;return true;
            }
            if(!HasOriginalCapacity(directory))return false;
            if(!RebirthNpcTradeOwnerPlan.TryCreate(quote,player.bag?.ItemGrid.items,player.inventory?.ItemGrid.items,
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length),
                (bag,slot)=>RebirthVehicleNativePolicy.IsLocked(player,bag,slot),out var plan)||!CurrentRoot())return false;
            var frozen=RebirthNpcTradeOriginal.Freeze(quote,plan,owner.CanonicalId,creation,world,deaths,id);
            if(!frozen.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false)||!CurrentRoot())return false;
            Directory.CreateDirectory(directory);SafePath(directory);
            if(!CurrentRoot())return false;
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(frozen.Image);
            if(bytes.Length<1||bytes.Length>MaximumBytes)return false;
            // CreateNew deliberately leaves a partial failed original in place.
            // Missing/truncated custody never permits overwrite or a new plan.
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            var saved=Read(path);
            if(!CurrentRoot()||saved.Image!=frozen.Image||!frozen.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false))return false;
            original=saved;return true;
        }
        catch{return false;}
    }
    private static bool Current(EntityPlayerLocal player)=>ThreadManager.IsMainThread()&&player!=null&&!player.IsDead()&&
        ReferenceEquals(player.world,GameManager.Instance?.World)&&ReferenceEquals(player,player.world?.GetPrimaryPlayer())&&
        ReferenceEquals(player,player.world?.GetEntity(player.entityId))&&player.bag?.ItemGrid.items!=null&&player.inventory?.ItemGrid.items!=null;
    private static void SafePath(string path)
    {
        for(var parent=new DirectoryInfo(path);parent!=null;parent=parent.Parent)
            if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Trade original directory redirected");
    }
    private static bool HasOriginalCapacity(string directory)
    {
        SafePath(directory);if(!Directory.Exists(directory))return true;
        int scanned=0,active=0;
        foreach(string path in Directory.EnumerateFiles(directory,"*.xml"))
        {
            // Closed history is retained on disk, but cannot permanently fill
            // the active-original admission limit. Unknown chains still count.
            if(++scanned>8192)return false;
            if(!IsClosedHistory(path)&&++active>=MaximumOriginals)return false;
        }
        SafePath(directory);return true;
    }
    private static bool IsClosedHistory(string path)
    {
        try
        {
            string name=Path.GetFileNameWithoutExtension(path);if(!Guid.TryParseExact(name,"N",out var operation))return false;
            string prefix=Path.Combine(Path.GetDirectoryName(path),name);
            string ReceiptText(string file)
            {
                SafePath(Path.GetDirectoryName(file));
                if((File.GetAttributes(file)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new InvalidDataException();
                using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
                if(stream.Length<1||stream.Length>4096)throw new InvalidDataException();
                using var text=new StreamReader(stream,new UTF8Encoding(false,true),false);return text.ReadToEnd();
            }
            string original;
            if(File.Exists(prefix+".unused"))
            {
                if(!RebirthNpcTradeOwnerReceipt.TryReadUnused(ReceiptText(prefix+".unused"),out var unusedOperation,out original,out _)||unusedOperation!=operation)return false;
            }
            else if(!RebirthNpcTradeOwnerReceipt.TryRead(ReceiptText(prefix+".ack"),out var ack,out original,out var terminal,out bool ackClosed)||ackClosed||ack!=operation||
                !RebirthNpcTradeOwnerReceipt.TryRead(ReceiptText(prefix+".closed"),out var done,out var doneOriginal,out var doneTerminal,out bool closed)||
                !closed||done!=operation||doneOriginal!=original||doneTerminal!=terminal)return false;
            SafePath(Path.GetDirectoryName(path));
            if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)return false;
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length<1||input.Length>MaximumBytes)return false;
            using(var sha=System.Security.Cryptography.SHA256.Create())
                if(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()!=original)return false;
            input.Position=0;
            using var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes});
            reader.MoveToContent();
            return reader.LocalName=="npcTradeOriginal"&&reader.NamespaceURI.Length==0&&reader.GetAttribute("operation")==name;
        }
        catch{return false;}
    }
    private static RebirthNpcTradeOriginal Read(string path)
    {
        SafePath(Path.GetDirectoryName(path));
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>MaximumBytes)throw new InvalidDataException();
            byte[] bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<=0)throw new EndOfStreamException();offset+=read;}
            if(stream.ReadByte()!=-1)throw new InvalidDataException();
            return RebirthNpcTradeOriginal.Read(new UTF8Encoding(false,true).GetString(bytes));
        }
    }
}
