using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class RecyclerJoinedTerminal:IDisposable
    {
        internal RecyclerJoining Joined;internal Guid Operation,Nonce;internal XElement Original;internal string Path,Digest,OriginalDigest;internal long Length;
        internal int Slots;internal float Next;internal IDisposable Memory;
        internal bool Current()=>Memory!=null&&Joined?.Pending?.Contains(Operation)==true&&ReferenceEquals(GameManager.Instance?.World,Joined.World)&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(Joined.World)==Joined.WorldKey&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&
            identity.StorageKey==Joined.Owner&&identity.CanonicalId==Joined.Account&&!Joined.Connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Joined.Connection)&&
            RebirthWorkstationCompletionPublication.PublishedLength(Path)==Length&&RecyclerFileDigest(Path)==Digest;
        public void Dispose(){joinedRecyclerTerminals.Remove(this);Original=null;Joined=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly List<RecyclerJoinedTerminal> joinedRecyclerTerminals=new List<RecyclerJoinedTerminal>();
    private static void PumpJoinedRecyclerTerminals(World world)
    {
        foreach(var terminal in joinedRecyclerTerminals.ToArray())
        {
            if(!ReferenceEquals(terminal.Joined.World,world)||terminal.Joined.Connection.IsDisconnected()){terminal.Dispose();continue;}
            if(terminal.Current()&&UnityEngine.Time.realtimeSinceStartup>=terminal.Next)
            {
                terminal.Next=UnityEngine.Time.realtimeSinceStartup+1f;
                SendPlacedText(new XElement("recyclerRemoteCommand",new XAttribute("operation",terminal.Operation.ToString("N")),new XAttribute("nonce",terminal.Nonce.ToString("N")),
                    new XAttribute("original",terminal.OriginalDigest),new XAttribute("command","ack")).ToString(SaveOptions.DisableFormatting));
            }
        }
        if(world?.IsRemote()!=true||joinedRecyclerTerminals.Count>=4)return;
        foreach(var joined in joinedRecyclerOwners.ToArray())
        {
            if(!joined.Current())continue;
            foreach(Guid operation in joined.Pending)
            {
                if(joinedRecyclerTerminals.Any(value=>value.Operation==operation)||!PhaseBytes(joined.Buffs,"rbRecyclerPlaced_"+operation.ToString("N"),out float phase)||phase!=3)continue;
                var terminal=TryCaptureJoinedRecyclerTerminal(joined,operation);if(terminal!=null)joinedRecyclerTerminals.Add(terminal);
                if(joinedRecyclerTerminals.Count>=4)return;
            }
        }
    }
    private static RecyclerJoinedTerminal TryCaptureJoinedRecyclerTerminal(RecyclerJoining joined,Guid operation)
    {
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
        foreach(string path in Directory.EnumerateFiles(root,operation.ToString("N")+"*.remote-client-terminal.xml"))
        {
            long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var memory))continue;
            try
            {
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=record?.Element("before")?.Elements().SingleOrDefault();
                var terminal=record?.Element("recyclerRemoteClientTerminal");var owner=terminal?.Element("actualOwnerLocalSaved");var reply=terminal?.Element("recyclerRemoteStatus");
                if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=operation.ToString("N")||original?.Name!="placedRecyclerOriginal"||
                    (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("owner")!=joined.Owner||(string)original.Attribute("account")!=joined.Account||
                    (string)original.Attribute("creation")!=joined.Creation||(int?)original.Attribute("deaths")!=joined.Deaths||(string)original.Attribute("world")!=joined.WorldKey||
                    !Guid.TryParseExact((string)reply?.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||(int?)reply.Attribute("stage")<5||
                    (int?)reply.Attribute("phase")!=3||(bool?)reply.Attribute("ownerSaved")!=true)return null;
                var bag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBag"),4*1024*1024));
                var belt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBelt"),4*1024*1024));
                var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024));
                var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024));
                if(!SameOwnerGrid(bag,joined.Bag)||!SameOwnerGrid(belt,joined.Belt)||beforeBag?.Length!=bag.Length||beforeBelt?.Length!=belt.Length)continue;
                int slots=beforeBag.Zip(bag,(a,b)=>Same(a,b)?0:1).Sum()+beforeBelt.Zip(belt,(a,b)=>Same(a,b)?0:1).Sum();
                string digest=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting)));
                if(!PhaseBytes(joined.Buffs,"rbRecyclerSlot_"+operation.ToString("N"),out float count)||count!=slots||
                    (string)reply.Attribute("operation")!=operation.ToString("N")||(string)reply.Attribute("original")!=digest||(int?)reply.Attribute("completed")!=slots||
                    !ValidateJoinedLocalTerminalOwner(original,owner,operation,slots)||!joined.Current())continue;
                var result=new RecyclerJoinedTerminal{Joined=joined,Operation=operation,Nonce=nonce,Original=new XElement(original),OriginalDigest=digest,Path=path,
                    Length=length,Digest=RecyclerFileDigest(path),Slots=slots,Memory=memory};
                if(!result.Current())continue;memory=null;return result;
            }
            finally{memory?.Dispose();}
        }
        return null;
    }
    private static bool ValidateJoinedLocalTerminalOwner(XElement original,XElement descriptor,Guid operation,int slots)
    {
        if(descriptor?.Name!="actualOwnerLocalSaved"||(int?)descriptor.Attribute("phase")!=3||(int?)descriptor.Attribute("completed")!=slots)return false;
        string path=Path.GetFullPath((string)descriptor.Attribute("path")),root=Path.GetFullPath(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"))+Path.DirectorySeparatorChar;
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||length<1||length>24L*1024*1024||
            length!=(long?)descriptor.Attribute("length")||RecyclerFileDigest(path)!=(string)descriptor.Attribute("digest")||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))return false;
        using(memory)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=record?.Element("recyclerNativeOwnerSaved");
            var native=ColdBytes(saved?.Element("native"),16*1024*1024);
            return record?.Name=="workstationCompletionIntent"&&(string)record.Attribute("operation")==operation.ToString("N")&&
                XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),original)&&(int?)saved?.Attribute("phase")==3&&(int?)saved.Attribute("completed")==slots&&
                native?.Length>5&&native[0]=='t'&&native[1]=='t'&&native[2]=='p'&&native[3]==0&&native[4]==PlayerDataFile.cFileVersion&&
                length==RebirthWorkstationCompletionPublication.PublishedLength(path)&&RecyclerFileDigest(path)==(string)descriptor.Attribute("digest");
        }
    }
    private static bool ObserveJoinedRecyclerTerminalReply(XElement reply,INetConnection connection)
    {
        if(!Guid.TryParseExact((string)reply.Attribute("operation"),"N",out var operation))return false;
        var terminal=joinedRecyclerTerminals.SingleOrDefault(value=>value.Operation==operation&&ReferenceEquals(value.Joined.Connection,connection));
        if(terminal==null)return false;
        if(!terminal.Current()||(string)reply.Attribute("nonce")!=terminal.Nonce.ToString("N")||(string)reply.Attribute("original")!=terminal.OriginalDigest||
            (int?)reply.Attribute("stage")!=6||(int?)reply.Attribute("phase")!=3||(int?)reply.Attribute("completed")!=terminal.Slots||(bool?)reply.Attribute("ownerSaved")!=true||
            (string)reply.Attribute("world")!=terminal.Joined.WorldKey||(string)reply.Attribute("owner")!=terminal.Joined.Owner||
            (string)reply.Attribute("creation")!=terminal.Joined.Creation||(int?)reply.Attribute("deaths")!=terminal.Joined.Deaths)return true;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",terminal.Joined.WorldKey),
            new XElement("before",new XElement(terminal.Original)),new XElement("recyclerRemoteRetired",new XElement(reply)));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-retired.xml");
        if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return true;
        var joined=terminal.Joined;joined.Pending=joined.Pending.Where(value=>value!=operation).ToArray();joined.UnqualifiedStations.Remove(operation);joined.Unstarted.Remove(operation);
        terminal.Dispose();if(joined.Pending.Length==0)joined.Dispose();return true;
    }
}
