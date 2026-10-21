using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static bool ActionArchivedOwnerIdentity(byte[] native,Guid operation,float expectedPhase,int expectedDeaths,out int nativeId)
    {
        nativeId=0;if(native==null||native.Length<6||native.Length>16*1024*1024)return false;
        var data=new PlayerDataFile();
        using(var capture=new RebirthCamperEnergySavedOwner.Capture())
        using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0'||reader.ReadByte()!=PlayerDataFile.cFileVersion)return false;
            data.Read(reader,PlayerDataFile.cFileVersion,global::StreamModeRead.Persistency);
            if(capture.Failed||stream.Position!=stream.Length||data.deaths!=expectedDeaths||data.bDead||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(operation),out float phase,out bool present)||!present||phase!=expectedPhase)return false;
            nativeId=data.id;return true;
        }
    }
    private static bool ActionArchivedTerminalMetadata(byte[] native,Guid operation,int minimumDeaths)
    {
        if(native==null||native.Length<6||native.Length>16*1024*1024)return false;
        var data=new PlayerDataFile();
        using(var capture=new RebirthCamperEnergySavedOwner.Capture())
        using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0'||reader.ReadByte()!=PlayerDataFile.cFileVersion)return false;
            data.Read(reader,PlayerDataFile.cFileVersion,global::StreamModeRead.Persistency);
            return !capture.Failed&&stream.Position==stream.Length&&data.deaths>=minimumDeaths&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(operation),out float phase,out bool present)&&present&&phase==6f;
        }
    }
    // Already-saved no-effect terminal metadata only. The actual native owner
    // Load/Read/ToPlayer/Spawn lineage is mandatory; no target, payment graph,
    // financial setter, or first-grant permit can be constructed here.
    private static void PumpActionNeverAdmittedTerminalRestart()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>value.Spawn?.Local!=true&&ActionHostLoadedCurrent(value)))
        {
            long now=System.Diagnostics.Stopwatch.GetTimestamp();if(now<source.TerminalScanNext)continue;
            source.TerminalScanNext=now+System.Diagnostics.Stopwatch.Frequency;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(source.Buffs,out var operations))continue;
            foreach(var operation in operations.Concat(PendingActionOwnerOperations(source.Spawn)).Distinct())
            {
                if(source.TerminalMetadataDone.Contains(operation)||actionTerminalCouriers.ContainsKey(operation)||actionRemoteHostOriginals.ContainsKey(operation)||
                    actionPayments.Any(value=>value.Operation==operation)||actionTerminalCouriers.Count>=8||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present))continue;
                if(!present){TryRestoreCompletedActionTerminalCourier(source,operation);continue;}
                if(phase!=6f){TryRestoreCompletedActionTerminalCourier(source,operation);continue;}
                TryRestoreNeverAdmittedTerminalCourier(source,operation);
                if(!actionTerminalCouriers.ContainsKey(operation))TryRestoreCompletedActionTerminalCourier(source,operation);
            }
            if(operations.All(source.TerminalMetadataDone.Contains)&&!HasPendingActionOwner(source.Spawn)&&
                !actionPayments.Any(value=>ReferenceEquals(value.Actor,source.Actor))&&!actionTerminalCouriers.Values.Any(value=>ReferenceEquals(value.Actor,source.Actor)))
            {
#if REBIRTH_DEBUG
                if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&source.Actor.Buffs.GetCustomVar("rbActionRestartFixtureHold")==1f)continue;
#endif
                DropActionHostOwnerLoad(source);
            }
        }
    }
    private static void TryRestoreNeverAdmittedTerminalCourier(ActionHostOwnerLoad source,Guid operation)
    {
        IDisposable scratch=null,retained=null;
        try
        {
            string directory=Path.Combine(source.Spawn.Save,"RebirthData","ActionOwnerOriginals",operation.ToString("N"));ActionPath(directory);
            string outcomePath=Path.Combine(directory,"never-admitted.xml"),confirmedPath=Path.Combine(directory,"confirmed.native"),terminalPath=Path.Combine(directory,"terminal.native");
            ActionPath(outcomePath);ActionPath(confirmedPath);ActionPath(terminalPath);
            long outcomeSize=new FileInfo(outcomePath).Length,confirmedSize=new FileInfo(confirmedPath).Length,terminalSize=new FileInfo(terminalPath).Length;
            if(outcomeSize<1||outcomeSize>8L*1024*1024||confirmedSize<14||confirmedSize>24L*1024*1024+12||terminalSize<6||terminalSize>16L*1024*1024||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*outcomeSize+4*confirmedSize+terminalSize+65536),out scratch))return;
            byte[] outcomeBytes=ActionBytes(outcomePath,(int)outcomeSize),confirmed=ActionBytes(confirmedPath,(int)confirmedSize),terminal=ActionBytes(terminalPath,(int)terminalSize);
            var outcome=ReadActionBoundedXml(outcomeBytes,8*1024*1024);
            ReadActionRemoteEnvelope(confirmed,out string clickText,out byte[] nativeBefore);
            if(!ActionEnvelopeFileEquals(Path.Combine(directory,"before.native"),nativeBefore))return;
            var click=ReadRemoteActionOriginal(clickText);
            if(outcome.Name!="electricalNeverAdmitted"||(int?)outcome.Attribute("version")!=1||(string)outcome.Attribute("operation")!=operation.ToString("N")||
                (string)outcome.Attribute("account")!=source.Spawn.Account||(string)outcome.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(source.Actor)||
                (string)outcome.Attribute("world")!=source.Spawn.World.worldState.Guid||(string)outcome.Attribute("save")!=source.Spawn.Save||
                (ulong?)outcome.Attribute("recovery")!=RebirthNpcRecoveryGeneration.RequireCurrent()||(string)outcome.Element("click")!=clickText||
                click.Name!="electricalOwnerClick"||(int?)click.Attribute("version")!=1||(string)click.Attribute("operation")!=operation.ToString("N")||
                (string)click.Attribute("account")!=source.Spawn.Account||(string)click.Attribute("creation")!=(string)outcome.Attribute("creation")||
                (string)click.Attribute("world")!=source.Spawn.World.worldState.Guid||(int?)click.Attribute("deaths")>source.Actor.Died)return;
            int deaths=(int)click.Attribute("deaths"),nativeId=(int)click.Attribute("nativeId");
            var encoding=new UTF8Encoding(false,true);
            var exactOutcome=new XElement("electricalNeverAdmitted",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("account",source.Spawn.Account),new XAttribute("creation",(string)outcome.Attribute("creation")),new XAttribute("world",source.Spawn.World.worldState.Guid),
                new XAttribute("save",source.Spawn.Save),new XAttribute("recovery",RebirthNpcRecoveryGeneration.RequireCurrent()),new XElement("click",clickText),new XElement("reason",(string)outcome.Element("reason")));
            if(!outcomeBytes.SequenceEqual(encoding.GetBytes(exactOutcome.ToString(SaveOptions.DisableFormatting))))return;
            var exactOriginal=new XElement("electricalOwnerNativeOriginal",new XAttribute("version",1),new XAttribute("world",source.Spawn.World.worldState.Guid),
                new XAttribute("save",source.Spawn.Save),new XAttribute("recovery",RebirthNpcRecoveryGeneration.RequireCurrent()),new XAttribute("account",source.Spawn.Account),
                new XAttribute("creation",(string)outcome.Attribute("creation")),new XAttribute("deaths",deaths),new XAttribute("operation",operation.ToString("N")),new XElement("click",clickText));
            if(!ActionEnvelopeFileEquals(Path.Combine(directory,"original.xml"),encoding.GetBytes(exactOriginal.ToString(SaveOptions.DisableFormatting))))return;
            var bagFrame=RemoteActionFrameBytes(click.Element("bag"));var beltFrame=RemoteActionFrameBytes(click.Element("belt"));
            long graph=checked(MeasureActionRetainedFrame(bagFrame)+MeasureActionRetainedFrame(beltFrame));
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(8*outcomeSize+confirmedSize+terminalSize+nativeBefore.LongLength+graph+16L*(bagFrame.Length+beltFrame.Length)+65536)))return;
            var bag=RebirthCamperInventorySnapshot.Restore(bagFrame);var belt=RebirthCamperInventorySnapshot.Restore(beltFrame);
            confirmed=null;bagFrame=null;beltFrame=null;
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(8*outcomeSize+terminalSize+nativeBefore.LongLength+graph+2L*clickText.Length+65536))||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out var decoder))return;
            using(decoder)
                if(!ActionColdOwnerNativeMatches(nativeBefore,nativeId,deaths,operation,1f,bag,belt)||!ActionArchivedTerminalMetadata(terminal,operation,deaths))return;
            string outcomeText=new System.Text.UTF8Encoding(false,true).GetString(outcomeBytes);
            if(!ActionHostLoadedCurrent(source)||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(8*outcomeSize+terminalSize+4*nativeBefore.LongLength+graph+2L*clickText.Length+65536))||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(ActionRemoteEnvelopeRetainedBytes(outcomeText,nativeBefore),out retained))return;
            var body=new RebirthRemoteResourceFragments.Body(EncodeActionRemoteEnvelope(outcomeText,nativeBefore));
            var courier=new ActionTerminalCourier{Operation=operation,World=source.Spawn.World,Actor=source.Actor,Peer=source.Spawn.Peer,Connection=source.Spawn.Connection,
                Account=source.Spawn.Account,Creation=(string)outcome.Attribute("creation"),Deaths=deaths,NativeWorld=source.Spawn.World.worldState.Guid,Save=source.Spawn.Save,Body=body,Memory=retained};
            if(!courier.Current()||actionTerminalCouriers.ContainsKey(operation))return;
            actionTerminalCouriers.Add(operation,courier);source.TerminalMetadataDone.Add(operation);retained=null;
        }
        catch{/* Missing, partial, or conflicting historical material retains metadata; never replay financial work. */}
        finally{retained?.Dispose();scratch?.Dispose();}
    }
}
