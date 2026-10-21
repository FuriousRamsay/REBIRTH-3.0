using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static bool FinishLocalActionTerminalMetadata(ActionPayment payment)
    {
        if(!ThreadManager.IsMainThread()||!payment.Terminal||payment.State!=ActionPaymentState.Used&&payment.State!=ActionPaymentState.Returned||payment.Retired||payment.RemoteOwner!=null||payment.Actor is not EntityPlayerLocal actor||
            !ReferenceEquals(GameManager.Instance?.World,payment.World)||payment.World.IsRemote()||!ReferenceEquals(payment.World.GetPrimaryPlayer(),actor)||
            !ReferenceEquals(payment.World.GetEntity(actor.entityId),actor)||actor.IsDead()||actor.Died<payment.Deaths||
            RebirthCamperCharacterAccess.CurrentCreation(actor)!=payment.Creation||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.CanonicalId!=payment.Account||
            Path.GetFullPath(GameIO.GetSaveGameDir())!=payment.Save||!payment.Events.ContainsKey(payment.State==ActionPaymentState.Used?"used":"returned"))return false;
        ValidateActionHead(payment);
        byte[] bag=RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items),belt=RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items);
        int deaths=actor.Died;
        bool Current()=>ReferenceEquals(GameManager.Instance?.World,payment.World)&&ReferenceEquals(payment.World.GetPrimaryPlayer(),actor)&&
            ReferenceEquals(payment.World.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==payment.Creation&&
            bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))&&belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items));
        if(!Current())return false;
        actor.Buffs.RemoveCustomVar(ActionOwnerMarker(payment.Operation));GameManager.Instance.SaveLocalPlayerData();
        if(!Current()||!RebirthGearNativePlayerFile.TryRead(identity,out var file)||file.deaths!=deaths||file.bDead||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(file,ActionOwnerMarker(payment.Operation),out _,out bool present)||present||
            !bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(file,true)))||
            !belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(file,false)))||!Current())return false;
        ValidateActionHead(payment);
        RetireActionPendingTarget(payment.Save,payment.Operation,payment.Original);
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>value.Spawn?.Local==true&&ReferenceEquals(value.Actor,actor)))source.TerminalMetadataDone.Add(payment.Operation);
        return true;
    }
    // A completed original and actual native loaded phase6 qualify only the
    // existing terminal acknowledgment courier. No ActionPayment, financial
    // leg, current target, setter, refund or service permit is reconstructed.
    private static bool TryRestoreCompletedActionTerminalCourier(ActionHostOwnerLoad source,Guid operation)
    {
        IDisposable scratch=null,retained=null;
        try
        {
            if(!ActionHostLoadedCurrent(source)||source.TerminalMetadataDone.Contains(operation)||actionTerminalCouriers.ContainsKey(operation)||actionRemoteHostOriginals.ContainsKey(operation)||actionPayments.Any(value=>value.Operation==operation)||
                (!source.Spawn.Local&&actionTerminalCouriers.Count>=8)||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present))return false;
            string directory=Path.Combine(source.Spawn.Save,"RebirthData","ActionPayments",operation.ToString("N"));ActionPath(directory);
            string headPath=Path.Combine(directory,"current.xml");long headLength=new FileInfo(headPath).Length;
            if(headLength<1||headLength>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*headLength+64L*1024*1024),out scratch))return false;
            byte[] headBytes=ActionBytes(headPath,(int)headLength);var head=ReadActionBoundedXml(headBytes,24*1024*1024);
            string terminal=(string)head.Attribute("stage"),originalText=head.Element("original")?.Value;int last=(int?)head.Attribute("sequence")??-1;
            if(head.Name!="actionPaymentEvent"||(int?)head.Attribute("version")!=1||terminal!="used"&&terminal!="returned"&&terminal!="owner-terminal-saved"||last<1||last>=256)return false;
            var original=ReadRemoteActionOriginal(originalText);
            if(original.Name!="resourceActionOriginal"||(int?)original.Attribute("version")!=1||(string)original.Attribute("operation")!=operation.ToString("N")||
                (string)original.Attribute("action")!="electrical-service"||(string)original.Attribute("account")!=source.Spawn.Account||
                (string)original.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(source.Actor)||(string)original.Attribute("world")!=source.Spawn.World.worldState.Guid||
                (string)original.Attribute("save")!=source.Spawn.Save||(ulong?)original.Attribute("recovery")!=RebirthNpcRecoveryGeneration.RequireCurrent())return false;
            int deaths=(int)original.Attribute("deaths"),nativeId=(int)original.Attribute("nativeId");
            if(deaths<0||source.Actor.Died<deaths||original.Element("electrical")==null)return false;
            bool ownerContribution=original.Element("ownerOriginal")!=null;
            if(ownerContribution?(!present||(source.Spawn.Local?phase!=2f&&phase!=3f:phase!=2f&&phase!=3f&&phase!=6f&&phase!=8f)):present)return false;
            ReadActionTraining(original,operation,out bool trainingRequired);
            var stages=new Dictionary<string,int>(StringComparer.Ordinal);var sequences=new HashSet<int>();int files=0;
            foreach(string path in Directory.EnumerateFiles(directory,"*.xml",SearchOption.TopDirectoryOnly))
            {
                string name=Path.GetFileName(path);if(name.Length<12||name[6]!='-'||!int.TryParse(name.Substring(0,6),out int sequence))continue;
                if(++files>256||sequence<0||sequence>last||name.Substring(0,6)!=sequence.ToString("D6"))return false;
                string stage=name.Substring(7,name.Length-11);
                if(stage.Length<1||stage.Any(value=>value!='-'&&(value<'a'||value>'z')&&(value<'0'||value>'9'))||stages.ContainsKey(stage)||!sequences.Add(sequence))return false;
                ActionPath(path);long length=new FileInfo(path).Length;
                if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*length+65536),out var eventMemory))return false;
                using(eventMemory)
                {
                    byte[] bytes=ActionBytes(path,(int)length);var image=ReadActionBoundedXml(bytes,24*1024*1024);
                    if(image.Name!="actionPaymentEvent"||(int?)image.Attribute("version")!=1||(int?)image.Attribute("sequence")!=sequence||(string)image.Attribute("stage")!=stage||
                        image.Element("original")?.Value!=originalText||sequence==last&&!bytes.SequenceEqual(headBytes))return false;
                }
                stages.Add(stage,sequence);
            }
            if(files!=last+1||Enumerable.Range(0,last+1).Any(index=>!sequences.Contains(index))||!stages.TryGetValue(terminal,out int terminalSequence)||terminalSequence!=last||
                !stages.ContainsKey("prepared")||ownerContribution&&!stages.ContainsKey("owner-before-saved")||!stages.ContainsKey("target-before-saved"))return false;
            if(stages.ContainsKey("used")&&stages.ContainsKey("returned")||stages["prepared"]!=0||
                ownerContribution&&(stages["owner-before-saved"]<=stages["prepared"]||stages["target-before-saved"]<=stages["owner-before-saved"]))return false;
            if(terminal=="owner-terminal-saved")
            {
                bool used=stages.TryGetValue("used",out int usedSequence),returned=stages.TryGetValue("returned",out int returnedSequence);
                if(used==returned||(used?usedSequence:returnedSequence)!=last-1)return false;
                terminal=used?"used":"returned";terminalSequence=last-1;
            }
            if(terminal=="used")
            {
                string[] required={"paid","publication-intent","publication-returned","service-after-saved","target-after-saved",trainingRequired?"training-saved":"training-not-required"};
                if(required.Any(stage=>!stages.ContainsKey(stage)||stages[stage]>=terminalSequence)||stages.ContainsKey("owner-return-intent"))return false;
                for(int index=1;index<required.Length;index++)if(stages[required[index]]<=stages[required[index-1]])return false;
            }
            else if(!stages.ContainsKey("returning")||stages["returning"]>=terminalSequence||stages.ContainsKey("publication-intent"))return false;
            if(ownerContribution&&phase!=6f&&(terminal=="used"?phase!=2f:phase!=3f&&phase!=8f))return false;
            if(!ownerContribution)
            {
                var rows=original.Element("sources")?.Elements("source").ToArray();
                if(rows==null||rows.Length<1||rows.Length>32||rows.Any(row=>(int?)row.Attribute("kind")<1||(int?)row.Attribute("kind")>5)||
                    !ActionHostLoadedCurrent(source)||!ActionEnvelopeFileEquals(headPath,headBytes))return false;
                RetireActionPendingTarget(source.Spawn.Save,operation,originalText);source.TerminalMetadataDone.Add(operation);return true;
            }
            if(source.Spawn.Local)return FinishCompletedLocalActionMetadata(source,operation,terminal,original,headBytes);
            string ownerDirectory=Path.Combine(source.Spawn.Save,"RebirthData","ActionOwnerOriginals",operation.ToString("N"));ActionPath(ownerDirectory);
            string confirmedPath=Path.Combine(ownerDirectory,"confirmed.native");long confirmedLength=new FileInfo(confirmedPath).Length;
            if(confirmedLength<14||confirmedLength>24L*1024*1024+12||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(8*headLength+4*confirmedLength+64L*1024*1024)))return false;
            ReadActionRemoteEnvelope(ActionBytes(confirmedPath,(int)confirmedLength),out string clickText,out byte[] beforeNative);
            var click=ReadRemoteActionOriginal(clickText);
            if(click.Name!="electricalOwnerClick"||(int?)click.Attribute("version")!=1||(string)click.Attribute("operation")!=operation.ToString("N")||
                (string)click.Attribute("account")!=source.Spawn.Account||(string)click.Attribute("creation")!=(string)original.Attribute("creation")||
                (string)click.Attribute("world")!=source.Spawn.World.worldState.Guid||(int?)click.Attribute("deaths")!=deaths||(int?)click.Attribute("nativeId")!=nativeId||
                !ActionEnvelopeFileEquals(Path.Combine(ownerDirectory,"before.native"),beforeNative)||!ActionEnvelopeFileEquals(Path.Combine(directory,"owner-before.native"),beforeNative))return false;
            var bagFrame=RemoteActionFrameBytes(click.Element("bag"));var beltFrame=RemoteActionFrameBytes(click.Element("belt"));
            long graph=checked(MeasureActionRetainedFrame(bagFrame)+MeasureActionRetainedFrame(beltFrame));
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(8*headLength+4*confirmedLength+graph+16L*(bagFrame.Length+beltFrame.Length)+65536)))return false;
            var bag=RebirthCamperInventorySnapshot.Restore(bagFrame);var belt=RebirthCamperInventorySnapshot.Restore(beltFrame);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out var decoder))return false;
            using(decoder)
                if(!ActionColdOwnerNativeMatches(beforeNative,nativeId,deaths,operation,1f,bag,belt)||
                    (phase==6f?!ActionArchivedTerminalMetadata(source.Native,operation,deaths):!ActionArchivedOwnerIdentity(source.Native,operation,phase,source.Deaths,out _)))return false;
            if(!ActionHostLoadedCurrent(source)||!ActionEnvelopeFileEquals(headPath,headBytes)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(ActionRemoteEnvelopeRetainedBytes(originalText,beforeNative),out retained))return false;
            var courier=new ActionTerminalCourier{Operation=operation,World=source.Spawn.World,Actor=source.Actor,Peer=source.Spawn.Peer,Connection=source.Spawn.Connection,
                Account=source.Spawn.Account,Creation=(string)original.Attribute("creation"),Deaths=deaths,NativeWorld=source.Spawn.World.worldState.Guid,Save=source.Spawn.Save,
                Body=new RebirthRemoteResourceFragments.Body(EncodeActionRemoteEnvelope(originalText,beforeNative)),Memory=retained,Phase=phase==6f?(byte)7:terminal=="used"?(byte)5:(byte)6};
            if(!courier.Current()||actionTerminalCouriers.ContainsKey(operation))return false;
            RetireActionPendingTarget(source.Spawn.Save,operation,originalText);
            actionTerminalCouriers.Add(operation,courier);source.TerminalMetadataDone.Add(operation);retained=null;return true;
        }
        catch{return false;}
        finally{retained?.Dispose();scratch?.Dispose();}
    }
    private static bool FinishCompletedLocalActionMetadata(ActionHostOwnerLoad source,Guid operation,string terminal,XElement original,byte[] head)
    {
        if(!ActionHostLoadedCurrent(source)||!source.Spawn.Local||source.Actor is not EntityPlayerLocal actor||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)||!present||phase!=(terminal=="used"?2f:3f))return false;
        string directory=Path.Combine(source.Spawn.Save,"RebirthData","ActionPayments",operation.ToString("N")),beforePath=Path.Combine(directory,"owner-before.native");ActionPath(beforePath);
        long length=new FileInfo(beforePath).Length;if(length<6||length>16L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*length+64L*1024*1024),out var memory))return false;
        using(memory)
        {
            var owner=original.Element("ownerOriginal");if(owner==null)return false;
            var bagFrame=RemoteActionFrameBytes(owner.Element("bag"));var beltFrame=RemoteActionFrameBytes(owner.Element("belt"));
            long graph=checked(MeasureActionRetainedFrame(bagFrame)+MeasureActionRetainedFrame(beltFrame));
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(4*length+graph+16L*(bagFrame.Length+beltFrame.Length)+65536)))return false;
            var bag=RebirthCamperInventorySnapshot.Restore(bagFrame);var belt=RebirthCamperInventorySnapshot.Restore(beltFrame);byte[] before=ActionBytes(beforePath,(int)length);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out var decoder))return false;
            using(decoder)
                if(!ActionColdOwnerNativeMatches(before,(int)original.Attribute("nativeId"),(int)original.Attribute("deaths"),operation,1f,bag,belt)||
                    !ActionArchivedOwnerIdentity(source.Native,operation,phase,source.Deaths,out int loadedId)||loadedId!=source.NativeId)return false;
            if(!ActionHostLoadedCurrent(source)||!ActionEnvelopeFileEquals(Path.Combine(directory,"current.xml"),head))return false;
            byte[] currentBag=RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items),currentBelt=RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items);
            bool Current()=>ActionHostLoadedCurrent(source)&&!actionPayments.Any(value=>value.Operation==operation)&&
                currentBag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))&&currentBelt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items))&&
                ActionEnvelopeFileEquals(Path.Combine(directory,"current.xml"),head);
            if(!Current())return false;
            actor.Buffs.RemoveCustomVar(ActionOwnerMarker(operation));
            if(!Current())return false;GameManager.Instance.SaveLocalPlayerData();
            if(!Current()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.CanonicalId!=source.Spawn.Account||
                !RebirthGearNativePlayerFile.TryRead(identity,out var file)||file.deaths!=actor.Died||file.bDead||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(file,ActionOwnerMarker(operation),out _,out bool savedPresent)||savedPresent||
                !currentBag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(file,true)))||
                !currentBelt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(file,false)))||!Current())return false;
            RetireActionPendingTarget(source.Spawn.Save,operation,original.ToString(SaveOptions.DisableFormatting));
            source.TerminalMetadataDone.Add(operation);return true;
        }
    }
    private static void PumpActionCompletedLocalTerminalRestart()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>value.Spawn?.Local==true&&ActionHostLoadedCurrent(value)))
        {
            long now=System.Diagnostics.Stopwatch.GetTimestamp();if(now<source.TerminalScanNext)continue;
            source.TerminalScanNext=now+System.Diagnostics.Stopwatch.Frequency;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(source.Buffs,out var operations))continue;
            foreach(var operation in operations.Concat(PendingActionOwnerOperations(source.Spawn)).Distinct())if(!source.TerminalMetadataDone.Contains(operation))TryRestoreCompletedActionTerminalCourier(source,operation);
            if(operations.All(source.TerminalMetadataDone.Contains)&&!actionPayments.Any(value=>ReferenceEquals(value.Actor,source.Actor))&&!HasPendingActionOwner(source.Spawn))
            {
#if REBIRTH_DEBUG
                if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&source.Actor.Buffs.GetCustomVar("rbActionRestartFixtureHold")==1f)continue;
#endif
                DropActionHostOwnerLoad(source);
            }
        }
    }
}
