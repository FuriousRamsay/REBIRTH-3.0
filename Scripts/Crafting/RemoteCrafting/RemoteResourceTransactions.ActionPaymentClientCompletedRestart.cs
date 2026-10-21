using System;
using System.IO;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private static void RestoreCompletedActionClient(ActionClientNativeRead source,Guid operation,float phase,string directory,string head,byte[] headNative,IDisposable scratch)
    {
        IDisposable memory=null,grantMemory=null,outcomeMemory=null;ActionRemoteClick restored=null;
        try
        {
            int newline=head.IndexOf('\n');if(newline<1||newline>32)return;string stage=head.Substring(0,newline),grant=head.Substring(newline+1);
            bool terminal=stage=="used-terminal"||stage=="returned-terminal";
            bool invoking=stage=="debit-invoking"||stage=="refund-invoking";
            if(phase==6f?!terminal:phase==2f?stage!="debit-returned"&&stage!="debit-invoking"&&!terminal:
                phase==3f?stage!="refund-returned"&&stage!="refund-invoking"&&!terminal:
                phase==8f?stage!="debit-refused"&&stage!="debit-invoking"&&!terminal:true)return;
            if(!ActionClientLoadedCurrent(source)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var account))return;
            ReadActionClientRecord(Path.Combine(directory,"original.bin"),out string original,out byte[] initialNative);
            if(!ActionEnvelopeFileEquals(Path.Combine(directory,"initial-before.native"),initialNative))return;
            var image=ReadRemoteActionOriginal(original);int deaths=(int)image.Attribute("deaths"),oldId=(int)image.Attribute("nativeId");
            if(image.Name!="electricalOwnerClick"||(int?)image.Attribute("version")!=1||(string)image.Attribute("operation")!=operation.ToString("N")||
                (string)image.Attribute("world")!=source.World.worldState.Guid||(string)image.Attribute("account")!=account.CanonicalId||
                (string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(source.Actor)||source.Deaths<deaths)return;
            bool priorLife=source.NativeLifeEnded||source.Actor.Died!=deaths;
            byte[] bag=RemoteActionFrameBytes(image.Element("bag")),belt=RemoteActionFrameBytes(image.Element("belt")),price=RemoteActionFrameBytes(image.Element("price"));
            long graph=checked(MeasureActionRetainedFrame(bag)+MeasureActionRetainedFrame(belt)+MeasureActionRetainedFrame(price));
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(graph+initialNative.LongLength+2L*original.Length+65536),out memory))return;
            var position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
            var block=source.World.GetBlock(position);block.type=(ushort)(int)image.Attribute("block");
            restored=new ActionRemoteClick{Operation=operation,World=source.World,Actor=source.Actor,Connection=source.Connection,Target=source.World.GetTileEntity(position) as TileEntityPowered,Position=position,
                Block=block,Account=account.CanonicalId,Creation=(string)image.Attribute("creation"),Deaths=deaths,NativeEntityId=source.Actor.entityId,OriginalNativeEntityId=oldId,NativeWorld=source.World.worldState.Guid,
                Original=original,NativeBefore=initialNative,BeforeObserved=true,InitialSaved=true,ClientHeadStage=stage,RestartOwnerSource=source,
                Bag=source.BagGrid,Belt=source.BeltGrid,BagReferences=source.BagReferences,BeltReferences=source.BeltReferences,
                BagBefore=RebirthCamperInventorySnapshot.Restore(bag),BeltBefore=RebirthCamperInventorySnapshot.Restore(belt),Price=RebirthCamperInventorySnapshot.Restore(price),Memory=memory,NativeLifeEnded=priorLife};
            var grantImage=ReadRemoteActionOriginal(grant);bool noOwner=grantImage.Element("ownerOriginal")==null;
            byte[] hostBefore;
            if(noOwner){if(!terminal)return;hostBefore=headNative;}
            else
            {
                ReadActionClientRecord(Path.Combine(directory,"debit-grant.bin"),out string savedGrant,out hostBefore);
                if(savedGrant!=grant)return;
            }
            long transient=checked(headNative.LongLength+hostBefore.LongLength+2L*(head.Length+grant.Length)+bag.Length+belt.Length+price.Length+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,transient))return;
            ActionRemoteDebitPlan plan;
            if(grantImage.Name=="electricalNeverAdmitted")
            {
                // Positive no-admission is a distinct historical outcome. It
                // has no electrical payment rows and cannot qualify a debit.
                if(!terminal||!noOwner||(int?)grantImage.Attribute("version")!=1||
                    (string)grantImage.Attribute("operation")!=operation.ToString("N")||(string)grantImage.Attribute("account")!=restored.Account||
                    (string)grantImage.Attribute("creation")!=restored.Creation||(string)grantImage.Attribute("world")!=restored.NativeWorld||
                    (string)grantImage.Element("click")!=restored.Original||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(hostBefore.LongLength+2L*grant.Length+65536),out grantMemory))return;
                plan=new ActionRemoteDebitPlan{Click=restored,Original=grant,ServiceSnapshot=ReadActionServiceSnapshot(grantImage.Element("electrical")),HostNativeBefore=hostBefore,NoOwnerContribution=true};
            }
            else plan=QualifyRemoteActionDebit(restored,grant,hostBefore,out grantMemory,noOwner,source);
            if(plan==null)return;
            restored.Plan=plan;restored.GrantMemory=grantMemory;grantMemory=null;
            byte[] outcome=headNative;
            float outcomePhase=stage=="used-terminal"?2f:stage=="returned-terminal"?3f:phase;
            if(invoking)
            {
                string name=phase==2f?"debit-after.native":phase==3f?"refund-after.native":"debit-refused.native";string path=Path.Combine(directory,name);ActionPath(path);
                long length=new FileInfo(path).Length;if(length<6||length>16L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(transient+length)))return;
                outcome=ActionBytes(path,(int)length);
            }
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out var decoder))return;
            using(decoder)
            {
                if(!ActionColdOwnerNativeMatches(initialNative,oldId,deaths,operation,1f,restored.BagBefore,restored.BeltBefore))return;
                if(noOwner&&!ActionColdOwnerNativeMatches(hostBefore,oldId,deaths,operation,1f,restored.BagBefore,restored.BeltBefore))return;
                if(!noOwner)
                {
                    if(stage=="returned-terminal")
                    {
                        // The already-saved full terminal can follow a positive
                        // native refusal or a real original refund. Neither
                        // branch can authorize another inventory invocation.
                        if(ActionArchivedOwnerIdentity(outcome,operation,8f,deaths,out _))outcomePhase=8f;
                    }
                    // The native entity id may have been reassigned by an
                    // earlier actual join before this original invocation.
                    // Bind the saved outcome to its full saved pre-setter
                    // receipt, never to a current id lookup or an old click id.
                    string preimagePath=Path.Combine(directory,outcomePhase==3f?"refund-before.native":"debit-before.native");ActionPath(preimagePath);
                    long preimageLength=new FileInfo(preimagePath).Length;
                    if(preimageLength<6||preimageLength>16L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(transient+outcome.LongLength+preimageLength+65536)))return;
                    byte[] preimage=ActionBytes(preimagePath,(int)preimageLength);float enteredPhase=outcomePhase==3f?5f:4f;
                    if(!ActionArchivedOwnerIdentity(preimage,operation,enteredPhase,deaths,out int invokedId)||
                        !ActionColdOwnerNativeMatches(preimage,invokedId,deaths,operation,enteredPhase,enteredPhase==5f?plan.BagAfter:restored.BagBefore,enteredPhase==5f?plan.BeltAfter:restored.BeltBefore)||
                        !ActionColdOwnerNativeMatches(outcome,invokedId,deaths,operation,outcomePhase,outcomePhase==2f?plan.BagAfter:restored.BagBefore,outcomePhase==2f?plan.BeltAfter:restored.BeltBefore))return;
                    if(invoking&&!headNative.SequenceEqual(preimage))return;
                }
            }
            if((!terminal||phase!=6f)&&!priorLife)
            {
                var expectedBag=phase==2f?plan.BagAfter:restored.BagBefore;var expectedBelt=phase==2f?plan.BeltAfter:restored.BeltBefore;
                if(!ActionClientLoadedCurrent(source)||!SameActionItems(source.BagGrid.items,expectedBag)||!SameActionItems(source.BeltGrid.items,expectedBelt)||
                    source.BagReferences.Where((value,index)=>!ReferenceEquals(value,source.BagGrid.items[index])).Any()||source.BeltReferences.Where((value,index)=>!ReferenceEquals(value,source.BeltGrid.items[index])).Any())return;
            }
            restored.DebitEntered=!noOwner;restored.Debited=!noOwner&&outcomePhase!=8f;restored.RefundEntered=restored.Refunded=!noOwner&&outcomePhase==3f;restored.DebitRefused=!noOwner&&outcomePhase==8f;
            restored.AppliedBagReferences=source.BagReferences;restored.AppliedBeltReferences=source.BeltReferences;
            restored.TerminalMetadataOnly=terminal;
            if(!noOwner)
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(outcome.LongLength+4096),out outcomeMemory))return;
                var saved=new ActionClientCheckpoint{Click=restored,Outcome=true,Refund=outcomePhase==3f,Refused=outcomePhase==8f,NativeSaved=true,Saved=true,Native=outcome,Memory=outcomeMemory,Directory=directory,
                    Path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),restored.Account+"."+PlayerDataFile.EXT))};outcomeMemory=null;
                if(saved.Refund)restored.RefundOutcome=saved;else restored.DebitOutcome=saved;
            }
            if(!restored.OwnerMetadataCurrent()||!SameActionClientNativeRead(source))return;
            if(terminal&&phase==6f)
            {
                if(actionRemoteTerminalReceipts.ContainsKey(operation)||actionRemoteTerminalReceipts.Count>=8)return;
                // Actual native phase6 +full historical terminal needs only
                // courier restoration. Preserve a different pending operation's
                // global Held stamp and all current inventory contents.
                restored.Debited=stage=="used-terminal"||restored.Debited;
                var body=PrepareActionRemoteRequestBody(restored);if(body==null)return;
                long bytes=checked(body.Length+2L*plan.Original.Length+2L*(restored.Account.Length+restored.Creation.Length+restored.NativeWorld.Length)+8192);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out var receiptMemory))return;
                actionRemoteTerminalReceipts.Add(operation,new ActionRemoteTerminalReceipt{Operation=operation,World=source.World,Actor=source.Actor,Connection=source.Connection,Account=restored.Account,Creation=restored.Creation,
                    Deaths=deaths,NativeWorld=restored.NativeWorld,Original=plan.Original,Outcome=stage=="used-terminal"?(byte)5:(byte)6,Request=body,Memory=receiptMemory});
                source.MetadataDone.Add(operation);return;
            }
            lock(actionOwnerGate){if(actionRemoteClick!=null)return;actionRemoteClick=restored;memory=null;}
            // An actual fresh authenticated host terminal grant must still
            // precede phase6/global-Held mutation after native phase2/3/8 load.
            // An old client terminal cannot cross a server timeline restore.
        }
        catch{/* Missing/uncertain original remains held, with no setter replay. */}
        finally
        {
            grantMemory?.Dispose();outcomeMemory?.Dispose();
            if(memory!=null){ReleaseActionClientCheckpoint(restored?.DebitOutcome);ReleaseActionClientCheckpoint(restored?.RefundOutcome);restored?.GrantMemory?.Dispose();memory.Dispose();}
        }
    }
}
