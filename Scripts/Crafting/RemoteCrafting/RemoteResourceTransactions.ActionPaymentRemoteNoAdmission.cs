using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static void RecordRemoteActionNoAdmission(ActionRemoteHostOriginal source,string reason)
    {
        if(source.NoAdmission!=null)return;
        if(!source.NoAdmissionIssued){source.NoAdmissionIssued=true;source.NoAdmissionReason=(reason??"").Substring(0,Math.Min(512,(reason??"").Length));}
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return;
        using(scratch)
        {
        // Issued only immediately after the actual synchronous owning
        // preparation returned false, with no original reserved or native
        // payment call. No missing receipt/current equality produces this fact.
        source.NoAdmission=new XElement("electricalNeverAdmitted",new XAttribute("version",1),
            new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("account",source.Account),new XAttribute("creation",source.Creation),
            new XAttribute("world",source.NativeWorld),new XAttribute("save",source.Save),new XAttribute("recovery",source.Recovery),
            new XElement("click",source.Click),new XElement("reason",source.NoAdmissionReason)).ToString(SaveOptions.DisableFormatting);
        if(!NarrowActionRemoteHostMemory(source))source.NoAdmission=null;
        }
    }
    private static void PumpRemoteActionNoAdmission(ActionRemoteHostOriginal source)
    {
        if(source.NoAdmission==null||!source.MetadataCurrent()||!source.Confirmed)return;
        if(source.RefusalBody==null)
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(ActionRemoteEnvelopeRetainedBytes(source.NoAdmission,source.NativeBefore),out var retained))return;
            try
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return;
                using(scratch)
                {
                    byte[] record=new UTF8Encoding(false,true).GetBytes(source.NoAdmission);if(record.Length>8*1024*1024)throw new InvalidDataException();
                    string path=Path.Combine(source.Directory,"never-admitted.xml");ActionPath(path);
                    if(!File.Exists(path)){using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(record,0,record.Length);output.Flush(true);}
                    if(!ActionBytes(path,record.Length).SequenceEqual(record))return;
                    source.RefusalBody=new RebirthRemoteResourceFragments.Body(EncodeActionRemoteEnvelope(source.NoAdmission,source.NativeBefore));
                }
                source.RefusalMemory=retained;retained=null;
            }
            finally{retained?.Dispose();}
        }
        if(source.TerminalAccepted&&source.TerminalSaved)
        {
            if(actionTerminalCouriers.Count>=8)return;
            if(!ActionBytes(Path.Combine(source.Directory,"terminal.native"),source.NativeTerminal.Length).SequenceEqual(source.NativeTerminal))return;
            var courier=new ActionTerminalCourier{Operation=source.Operation,World=source.World,Actor=source.Actor,Peer=source.Peer,Connection=source.Connection,
                Account=source.Account,Creation=source.Creation,Deaths=source.Deaths,NativeWorld=source.NativeWorld,Save=source.Save,Body=source.RefusalBody,Memory=source.RefusalMemory};
            actionTerminalCouriers.Add(courier.Operation,courier);source.RefusalBody=null;source.RefusalMemory=null;
            source.Retired=true;lock(actionOwnerGate)if(source.Workers==0)DropRetiredActionRemoteHost(source);return;
        }
        string key="electrical-never-admitted:"+source.Operation.ToString("N");
        if(actionGrantWire.IsQueued(key)||System.Diagnostics.Stopwatch.GetTimestamp()<source.RefusalNext)return;
        bool Current()=>source.NoAdmission!=null&&source.MetadataCurrent()&&!source.TerminalSaved;
        bool Ready()=>Current()&&!source.Connection.IsDisconnected()&&(source.Connection is NetConnectionSimple||source.Connection is NetConnectionSteam);
        if(actionGrantWire.Enqueue(key,source.RefusalBody,Current,Ready,index=>source.Peer.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthActionPaymentOwnerGrant>().Setup(source.Actor.entityId,source.Operation,8,source.RefusalBody,index))))
            source.RefusalNext=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency;
    }
    private static void ReceiveRemoteActionNoAdmission(ActionRemoteClick click,string original,byte[] native)
    {
        if(click?.OwnerMetadataCurrent()==true&&click.Plan!=null&&click.Plan.NoOwnerContribution&&click.Plan.Original==original&&click.Plan.HostNativeBefore.SequenceEqual(native))
        {FinishActionRemoteTerminal(click,6);return;}
        if(click?.Current()!=true||click.Plan!=null||click.DebitEntered||click.RefundEntered||click.Debited||click.Refunded||click.DebitRefused||
            click.Actor.Buffs.GetCustomVar(ActionOwnerMarker(click.Operation))!=1f||!ActionRemoteImage(click,click.BagBefore,click.BeltBefore,click.BagReferences,click.BeltReferences))return;
        var image=ReadRemoteActionOriginal(original);
        if(image.Name!="electricalNeverAdmitted"||(int?)image.Attribute("version")!=1||(string)image.Attribute("operation")!=click.Operation.ToString("N")||
            (string)image.Attribute("account")!=click.Account||(string)image.Attribute("creation")!=click.Creation||(string)image.Attribute("world")!=click.NativeWorld||
            (string)image.Element("click")!=click.Original||!RemoteActionNativeBeforeMatches(click,native))return;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var memory))return;
        try
        {
            click.Plan=new ActionRemoteDebitPlan{Click=click,Original=original,HostNativeBefore=native,NoOwnerContribution=true};
            click.GrantMemory=memory;memory=null;FinishActionRemoteTerminal(click,6);
        }
        finally{memory?.Dispose();}
    }
}
