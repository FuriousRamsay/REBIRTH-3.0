using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // A genuine native owner join corroborates an already observed unused
    // close. This object can exchange metadata only; it has no Original.
    private sealed class JoinedUnused:IDisposable
    {
        internal RecyclerJoining Joined;internal Guid Operation,Nonce;internal XElement Original;
        internal string Path,Digest,OriginalDigest;internal long Length;internal float Next;internal IDisposable Memory;
        internal bool QueryOnly;internal float ObserveUntil;
        internal bool Current()=>Memory!=null&&Joined?.Current()==true&&Joined.Pending.Contains(Operation)&&
            RebirthWorkstationCompletionPublication.PublishedLength(Path)==Length&&RecyclerFileDigest(Path)==Digest;
        public void Dispose(){joinedUnused.Remove(this);Original=null;Joined=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly List<JoinedUnused> joinedUnused=new List<JoinedUnused>();
    private static uint joinedUnusedCursor;
    private static void PumpJoinedRecyclerUnused(World world)
    {
        foreach(var value in joinedUnused.ToArray())
        {
            if(!ReferenceEquals(value.Joined.World,world)||value.Joined.Connection.IsDisconnected()){value.Dispose();continue;}
            // Rotate only metadata query observations. The actual joined
            // original/Pending custody and immutable files remain retained.
            if(value.QueryOnly&&UnityEngine.Time.realtimeSinceStartup>=value.ObserveUntil){value.Dispose();continue;}
            if(value.Current()&&UnityEngine.Time.realtimeSinceStartup>=value.Next)
            {
                value.Next=UnityEngine.Time.realtimeSinceStartup+1f;
                var command=new XElement("recyclerRemoteCommand",new XAttribute("operation",value.Operation.ToString("N")),
                    new XAttribute("nonce",value.Nonce.ToString("N")),new XAttribute("original",value.OriginalDigest),new XAttribute("command",value.QueryOnly?"query":"unused"));
                if(value.Nonce==Guid.Empty)command.Add(new XAttribute("beforeReply",true));SendPlacedText(command.ToString(SaveOptions.DisableFormatting));
            }
        }
        if(world?.IsRemote()!=true)return;
        foreach(var joined in joinedRecyclerOwners.ToArray())
        {
            if(!joined.Current())continue;
            int countPending=joined.Pending.Length;if(countPending==0)continue;
            int start=(int)(joinedUnusedCursor%(uint)countPending);
            foreach(var operation in joined.Pending.Skip(start).Concat(joined.Pending.Take(start)))
            {
                if(joinedUnused.Count>=4)return;
                unchecked{joinedUnusedCursor++;}
                if(joinedUnused.Any(value=>value.Operation==operation)||recyclerClients.Any(value=>value.Source?.Operation==operation))continue;
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-unused-request.xml");
                long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                bool queryOnly=length<1;
                // A native p0 join can query an already decided host outcome.
                // Lack of a close record never requests cancellation or
                // decides unusedness on either machine.
                if(queryOnly){path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-client.xml");length=RebirthWorkstationCompletionPublication.PublishedLength(path);}
                if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var memory))continue;
                try
                {
                    using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)continue;
                    var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=record?.Element("before")?.Elements().SingleOrDefault();
                    var close=record?.Element("recyclerRemoteUnusedClose");
                    if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=operation.ToString("N")||
                        (string)record.Attribute("world")!=joined.WorldKey||original?.Name!="placedRecyclerOriginal"||
                        (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("owner")!=joined.Owner||
                        (string)original.Attribute("account")!=joined.Account||(string)original.Attribute("creation")!=joined.Creation||
                        (int?)original.Attribute("deaths")!=joined.Deaths||(string)original.Attribute("world")!=joined.WorldKey||
                        !PhaseBytes(joined.Buffs,"rbRecyclerPlaced_"+operation.ToString("N"),out float phase)||(phase!=0&&phase!=1)||
                        !PhaseBytes(joined.Buffs,"rbRecyclerSlot_"+operation.ToString("N"),out float count)||count!=0||
                        !SameOwnerGrid(joined.Bag,RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024)))||
                        !SameOwnerGrid(joined.Belt,RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024))))continue;
                    Guid nonce=Guid.Empty;
                    if(queryOnly){if(phase!=0||record.Element("recyclerRemoteClientOriginal")==null||close!=null)continue;}
                    else
                    {
                        if((bool?)close?.Attribute("actualFinalClose")!=true||(bool?)close.Attribute("principalEntered")!=false||
                            (bool?)close.Attribute("ownerSlotsEntered")!=false||!Guid.TryParseExact((string)close.Attribute("nonce"),"N",out nonce)||
                            nonce==Guid.Empty&&(bool?)close.Attribute("beforeReply")!=true)continue;
                        var before=ColdBytes(original.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                        if((string)close.Attribute("nativeBefore")!=RebirthStationPublicationRecord.Digest(before))continue;
                    }
                    var value=new JoinedUnused{Joined=joined,Operation=operation,Nonce=nonce,Original=new XElement(original),Path=path,Length=length,
                        Digest=RecyclerFileDigest(path),OriginalDigest=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting))),Memory=memory,
                        QueryOnly=queryOnly,ObserveUntil=UnityEngine.Time.realtimeSinceStartup+10f};
                    memory=null;if(!value.Current()){value.Dispose();continue;}joinedUnused.Add(value);
                }
                catch{}
                finally{memory?.Dispose();}
            }
        }
    }
    private static bool ObserveJoinedRecyclerUnusedReply(XElement reply,INetConnection connection,Guid operation)
    {
        var value=joinedUnused.SingleOrDefault(entry=>entry.Operation==operation&&ReferenceEquals(entry.Joined.Connection,connection));
        if(value==null)return false;
        if(!value.Current()||value.Nonce!=Guid.Empty&&(string)reply.Attribute("nonce")!=value.Nonce.ToString("N")||
            !Guid.TryParseExact((string)reply.Attribute("nonce"),"N",out var savedNonce)||savedNonce==Guid.Empty||
            (string)reply.Attribute("original")!=value.OriginalDigest||
            (string)reply.Attribute("owner")!=value.Joined.Owner||(string)reply.Attribute("world")!=value.Joined.WorldKey||
            (string)reply.Attribute("creation")!=value.Joined.Creation||(int?)reply.Attribute("deaths")!=value.Joined.Deaths||(bool?)reply.Attribute("savedUnused")!=true)return true;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",value.Joined.WorldKey),
            new XElement("before",new XElement(value.Original)),new XElement("recyclerUnusedAdmission",new XAttribute("nativeStationEntered",false),
                new XAttribute("nativeOwnerEntered",false)),new XElement(reply));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".unused-admission.xml");
        if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!value.Current())return true;
        var joined=value.Joined;joined.Pending=joined.Pending.Where(entry=>entry!=operation).ToArray();joined.UnqualifiedStations.Remove(operation);joined.Unstarted.Remove(operation);
        value.Dispose();if(joined.Pending.Length==0)joined.Dispose();return true;
    }
}
