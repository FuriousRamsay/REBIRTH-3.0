using System;
using System.Linq;
using System.IO;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    internal static bool FindElectricalOriginal(EntityPlayer actor,Vector3i position,out ActionPayment original)
    {
        original=null;if(!ThreadManager.IsMainThread()||actor==null)return false;
        lock(actionOwnerGate)original=actionPayments.FirstOrDefault(value=>!value.Terminal&&!value.Retired&&ReferenceEquals(value.Actor,actor)&&ReferenceEquals(value.World,actor.world)&&value.ServiceCandidate.Position==position);
        return original!=null;
    }
    internal static bool ActionElectricalHeld(WorldBase world,Vector3i position)
    {lock(actionOwnerGate)return HasDetachedActionTarget(world,position)||actionPayments.Any(value=>!value.Terminal&&!value.Retired&&ReferenceEquals(value.World,world)&&value.ServiceCandidate.Position==position);}
    internal static void PumpActionPayments()
    {
        if(!ThreadManager.IsMainThread())return;
#if REBIRTH_DEBUG
        RetireActionNativeInputWatch();
#endif
        RetireReplacedActionRemoteObservation();
        RetireReplacedActionRemoteHosts();
        RetireDeferredActionHostOwnerLoads();
        ObserveJoinedActionLocalOwnerLoads();
        PumpActionCompletedLocalTerminalRestart();
        PumpActionRemoteRequests();
        PumpRemoteActionAdmissions();
        PumpActionPaidRestart();
        PumpActionNeverAdmittedTerminalRestart();
        ActionPayment[] originals;lock(actionOwnerGate)originals=actionPayments.ToArray();
        foreach(var payment in originals)
        {
            // An actual world/roster replacement retires observations only. It
            // cannot declare a financial outcome or touch the departed actor.
            if(!payment.Retired&&(!ReferenceEquals(GameManager.Instance?.World,payment.World)||payment.World?.worldState.Guid!=payment.NativeWorld||!ReferenceEquals(payment.World?.GetEntity(payment.Actor.entityId),payment.Actor)||payment.RemoteOwner?.Retired==true))
            {DetachPublishedActionTarget(payment);payment.Retired=true;}
            if(payment.Terminal||payment.Retired)
            {
                if(payment.Terminal&&!payment.Retired&&payment.RemoteOwner!=null&&!payment.RemoteTerminalReleased)
                {try{if(!FinishRemoteActionTerminal(payment))continue;}catch(Exception error){RemoteResourceDiagnostics.Write("Electrical terminal metadata retained: "+error.Message);continue;}}
                if(payment.Terminal&&!payment.Retired&&payment.RemoteOwner==null&&payment.OwnerBagBefore!=null)
                {try{if(!FinishLocalActionTerminalMetadata(payment))continue;}catch(Exception error){RemoteResourceDiagnostics.Write("Electrical local terminal metadata retained: "+error.Message);continue;}}
                if(payment.Terminal&&!payment.Retired)
                {try{RetireActionPendingTarget(payment.Save,payment.Operation,payment.Original);}catch(Exception error){RemoteResourceDiagnostics.Write("Electrical pending target metadata retained: "+error.Message);continue;}}
                payment.RemoteGrantBody=null;payment.RemoteGrantMemory?.Dispose();payment.RemoteGrantMemory=null;
                RetireCompletedActionTarget(payment);
                RebirthInfrastructureWorkService.ReleasePaidAction(payment);
                if(payment.Terminal&&!payment.Retired&&!payment.Notified&&ReferenceEquals(GameManager.Instance?.World,payment.World))
                {
                    payment.Notified=true;
                    try{RebirthInfrastructureWorkService.NotifyPaidActionOutcome(payment);}
                    catch(Exception error){RemoteResourceDiagnostics.Write("Electrical terminal feedback unavailable: "+error.Message);}
                    if(payment.RemoteOwner==null&&payment.OwnerBagBefore!=null&&ReferenceEquals(payment.World.GetEntity(payment.Actor.entityId),payment.Actor)&&!payment.Actor.IsDead()&&payment.Actor.Died==payment.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(payment.Actor)==payment.Creation)
                        try{payment.Actor.Buffs.RemoveCustomVar(ActionOwnerMarker(payment.Operation));}catch(Exception error){RemoteResourceDiagnostics.Write("Saved action marker retirement failed: "+error.Message);}
                    foreach(var leg in payment.Legs)
                        if(!leg.Local&&leg.Npc==null&&leg.Native?.Current()==true)
                            try{leg.Source.MarkModified();}catch(Exception error){RemoteResourceDiagnostics.Write("Saved action source notification failed: "+error.Message);}
                }
                payment.TargetPublication?.Dispose();payment.TargetPublication=null;
                payment.TargetMaterial?.Dispose();payment.TargetMaterial=null;
                payment.ServiceMaterial?.Dispose();payment.ServiceMaterial=null;
                foreach(var leg in payment.Legs){leg.SavedMaterial?.Dispose();leg.SavedMaterial=null;leg.Publication?.Dispose();leg.Publication=null;leg.StationIdentity?.Dispose();leg.StationIdentity=null;leg.VehicleIdentity?.Dispose();leg.VehicleIdentity=null;leg.DroneIdentity?.Dispose();leg.DroneIdentity=null;}
                ReleaseActionOwner(payment);
#if REBIRTH_DEBUG
                if(payment.ScenarioRetain&&!payment.Retired)continue;
#endif
                if(payment.OwnerObservationsReleased&&!payment.MemoryReleased)
                {
                    lock(actionOwnerGate){payment.Legs.Clear();payment.OwnerBagGrid=null;payment.OwnerBeltGrid=null;payment.OwnerBagBefore=null;payment.OwnerBeltBefore=null;payment.World=null;payment.Actor=null;payment.ServiceTarget=null;payment.Original=null;payment.MemoryReleased=true;payment.Memory.Dispose();}
                }
                if(payment.MemoryReleased)lock(actionOwnerGate)actionPayments.Remove(payment);continue;
            }
#if REBIRTH_DEBUG
            if(payment.ScenarioPaused)continue;
#endif
            if(payment.State==ActionPaymentState.Returning)AdvanceElectricalReturn(payment);
            else if(payment.State==ActionPaymentState.Paid)CompleteElectricalPayment(payment);
            else AdvanceElectricalPayment(payment);
        }
    }
    internal static bool CompleteElectricalPayment(ActionPayment payment)
    {
        if(payment?.TargetCurrent()!=true||payment.State!=ActionPaymentState.Paid||!payment.OwnerAfterSaved||payment.Legs.Any(leg=>!leg.AfterSaved))return false;
        try
        {
            if(!FlushActionRefusals(payment))return false;
            if(payment.ReturnedPublicationNeedsRestore)
            {
                if(!RebirthInfrastructureWorkService.RestoreReturnedPaidAction(payment,payment.ServiceBefore))return false;
                if(!AdoptReturnedActionSavedMaterials(payment))return false;
                payment.ReturnedPublicationNeedsRestore=false;
            }
            if(!payment.PublicationEntered)
            {
                ActionRecord(payment,"publication-intent",ElectricalCandidate(payment.ServiceCandidate));
                payment.PublicationEntered=true;
                if(!RebirthInfrastructureWorkService.PublishPaidAction(payment))return false;
            }
            // If the original publication threw before a positive return, no
            // second invocation is issued. Metadata retry needs that actual event.
            if(!payment.PublicationReturned)return false;
            ActionRecord(payment,"publication-returned",ElectricalCandidate(payment.ServiceCandidate));
            if(!SaveActionServiceMaterial(payment))return false;
            if(!SaveActionTarget(payment,true))return false;
            if(!CompleteActionTraining(payment))return false;
            ActionRecord(payment,"used");payment.State=ActionPaymentState.Used;payment.Terminal=true;ReleaseActionOwner(payment);return true;
        }
        catch(Exception error){RemoteResourceDiagnostics.Write("Paid service original retained "+payment.Operation+": "+error.Message);return false;}
    }
    private static bool SaveActionServiceMaterial(ActionPayment payment)
    {
        if(payment.ServiceAfterSaved)return true;
        if(payment.ServiceMaterial==null)
        {
            string live=RebirthInfrastructureWorkService.SavePaidAction(payment);if(live==null)return false;
            var info=new FileInfo(live);if(info.Length<1||info.Length>24L*1024*1024)return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return false;
            using(memory)
            {
                var bytes=ActionBytes(live,checked((int)info.Length));string archive=Path.Combine(payment.Directory,"service-after.native");
                if(!File.Exists(archive))using(var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                if(!ActionBytes(archive,bytes.Length).SequenceEqual(bytes)||!RebirthInfrastructureWorkService.ValidatePaidActionArtifact(payment,archive))return false;
                var material=RememberActionMaterial("service",archive,24*1024*1024);
                if(!material.Bytes.SequenceEqual(bytes)){material.Dispose();throw new IOException("Saved service material changed during retention");}payment.ServiceMaterial=material;
            }
        }
        if(!FinishActionMaterial(payment,payment.ServiceMaterial,"service","service-after-saved"))return false;
        payment.ServiceAfterSaved=true;payment.ServiceMaterial.Dispose();payment.ServiceMaterial=null;return true;
    }
    // Called only by the owning service. A retained attempted invocation is never
    // retried merely because the current slots happen to equal its old Before.
    private static bool ActionNpcImage(ActionLeg leg,bool after,bool returned)
    {
        if(leg.Npc.IsDogStorage)
        {
            var value=RebirthDogInventoryService.GetSnapshot(leg.Npc.NativeIdentity);
            var expected=returned?leg.Before:after?leg.After:leg.Before;
            if(value.Revision!=checked((uint)leg.Revision+(returned?2u:after?1u:0u))||!SameActionItems(value.Slots,expected)||value.Locks==null||value.Locks.Length!=leg.Locks.Length)return false;
            for(int i=0;i<leg.Locks.Length;i++)if(value.Locks[i]!=leg.Locks[i])return false;
            return true;
        }
        return ActionSameNpc(RebirthNpcInventoryTransactionService.GetSnapshot(leg.Npc.NativeIdentity),ActionExpectedNpc(leg,after,returned));
    }
    private static bool ActionFrameCurrent(ActionPayment payment,ActionLeg leg)
    {
        if(!ActionLegCurrent(payment,leg))return false;
        if(leg.RemoteOwner)return RemoteActionOriginalCurrent(payment.RemoteOwner);
        if(leg.Npc!=null)return ActionNpcImage(leg,leg.State==ActionLegState.Debited,leg.State==ActionLegState.Returned);
        if(!SameActionItems(ActionLive(payment,leg),leg.Progress))return false;
        var locks=leg.Source?.SlotLocks??(leg.Local?leg.Grid.SlotLocks:null);
        if(locks!=null){if(locks.Length!=leg.Locks.Length)return false;for(int i=0;i<locks.Length;i++)if(locks[i]!=leg.Locks[i])return false;}
        return true;
    }
    private static void RecordActionProgress(ActionPayment payment,ActionLeg leg,int index,int slot,bool returned)
    {
        string stage="source-"+index+"-slot-"+slot+(returned?"-returned":"-debited");
        if(payment.Events.ContainsKey(stage))return;
        ActionRecord(payment,stage,
            new XElement("nativeReturned",new XElement("full",ActionFrame(leg.Progress))));
    }
    private static void RecordActionRefusal(ActionPayment payment,ActionLeg leg)
    {
        if(leg.RefusalStage==null||leg.RefusalDetail==null)throw new InvalidOperationException("Actual source refusal event is missing");
        ActionRecord(payment,leg.RefusalStage,new XElement(leg.RefusalDetail));
    }
    private static bool InvokeActionSlot(ActionPayment payment,ActionLeg leg,int index,int take,bool returned)
    {
        if(leg.RemoteOwner)return AdvanceRemoteActionOwner(payment,returned);
        var done=returned?leg.ReturnedSlots:leg.DebitedSlots;int slot=leg.Slots[take];
        if(done[take]){RecordActionProgress(payment,leg,index,slot,returned);return true;}
        if(!ActionFrameCurrent(payment,leg))return false;
        // Each slot has a separate immutable intent, including all other rows.
        string intent="source-"+index+"-slot-"+slot+(returned?"-return-intent":"-debit-intent");
        using var frozen=new ActionFrozenDesired(returned?leg.Before[slot]:leg.After[slot]);var desired=frozen.Argument;
        var expected=ItemStack.Clone(leg.Progress);expected[slot]=frozen.Expected;
        var detail=new XElement("nativeIntent",new XElement("before",ActionFrame(leg.Progress)),new XElement("desired",ActionFrame(new[]{frozen.Expected})));
        if(leg.NeverEnteredIntent==intent)
        {
            ActionRecord(payment,intent,detail);
            leg.RefusalStage=intent+"-refused";leg.RefusalDetail=new XElement("actualInvocationNeverEntered",new XElement("unchanged",ActionFrame(leg.Progress)));
            leg.State=ActionLegState.Refused;RecordActionRefusal(payment,leg);return false;
        }
        if(payment.Events.ContainsKey(intent)){leg.State=ActionLegState.Unknown;return false;}
        try{ActionRecord(payment,intent,detail);}catch{leg.NeverEnteredIntent=intent;throw;}
        leg.State=returned?ActionLegState.ReturnAttempted:ActionLegState.DebitAttempted;
        using(var native=new ActionNativeWrite(payment,leg,slot,frozen))
        {
            try
            {
                if(leg.Local){if(leg.Belt)payment.Actor.inventory.SetItem(slot,desired.itemValue,desired.count);else payment.Actor.bag.SetSlot(slot,desired);}
                else leg.Source.SetSlot(slot,desired);
            }
            catch{leg.State=ActionLegState.Unknown;throw;}
            if(native.Observed&&native.Ran&&native.Entered&&native.Consumed&&frozen.Matches(ActionLive(payment,leg)[slot])&&SameActionItems(ActionLive(payment,leg),expected))
            {
                // Retain actual-return evidence before any fallible journal or
                // notification. Retry may publish this receipt, never the setter.
                done[take]=true;leg.Progress=expected;
                RecordActionProgress(payment,leg,index,slot,returned);return true;
            }
            if(native.Observed&&!native.Ran&&SameActionItems(ActionLive(payment,leg),leg.Progress))
            {
                leg.RefusalStage=intent+"-refused";leg.RefusalDetail=new XElement("nativeOriginalNotRun",new XElement("unchanged",ActionFrame(leg.Progress)));
                leg.State=ActionLegState.Refused;
                RecordActionRefusal(payment,leg);return false;
            }
            leg.State=ActionLegState.Unknown;return false;
        }
    }
    private static bool InvokeActionNpcDebit(ActionPayment payment,ActionLeg leg,int index)
    {
        string intent="source-"+index+"-debit-intent";
        if(leg.Commit==null)
        {
            if(leg.NeverEnteredIntent==intent&&ActionFrameCurrent(payment,leg))
            {ActionRecord(payment,intent,new XElement("fullBefore",ActionFrame(leg.Before)));leg.RefusalStage=intent+"-refused";leg.RefusalDetail=new XElement("actualNpcInvocationNeverEntered",new XElement("unchanged",ActionFrame(leg.Before)));leg.State=ActionLegState.Refused;RecordActionRefusal(payment,leg);return false;}
            if(payment.Events.ContainsKey(intent)||!ActionFrameCurrent(payment,leg)){leg.State=ActionLegState.Unknown;return false;}
            try{ActionRecord(payment,intent,new XElement("fullBefore",ActionFrame(leg.Before)));}catch{leg.NeverEnteredIntent=intent;throw;}
            leg.State=ActionLegState.DebitAttempted;actionNpcPayment=payment;actionNpcLeg=leg;actionNpcPermitUsed=false;
            try
            {
                foreach(int slot in leg.Slots)leg.Npc.SetSlot(slot,leg.After[slot]);
                AuditActionInvocation(payment,leg,-1,false);
                leg.Npc.MarkModifiedOriginal(leg.DebitMutation);leg.Commit=leg.Npc.LastCommitEvent;
            }
            catch{leg.Commit=leg.Npc.LastCommitEvent;leg.State=ActionLegState.Unknown;throw;}
            finally{actionNpcPayment=null;actionNpcLeg=null;actionNpcPermitUsed=false;}
        }
        var receipt=leg.Commit;
        if(receipt==null||!ReferenceEquals(receipt.Source,leg.Npc)||receipt.Mutation!=leg.DebitMutation||receipt.BeforeRevision!=(uint)leg.Revision){leg.State=ActionLegState.Unknown;return false;}
        if(receipt.Disposition==RemoteNpcResourceSource.CommitDisposition.Refused&&ActionNpcImage(leg,false,false))
        {leg.RefusalStage=intent+"-refused";leg.RefusalDetail=new XElement("npcRefused",new XAttribute("mutation",receipt.Mutation),new XAttribute("beforeRevision",receipt.BeforeRevision),new XAttribute("afterRevision",receipt.AfterRevision),new XElement("unchanged",ActionFrame(leg.Before)));leg.State=ActionLegState.Refused;RecordActionRefusal(payment,leg);return false;}
        if(receipt.Disposition!=RemoteNpcResourceSource.CommitDisposition.Published||receipt.AfterRevision!=checked((uint)leg.Revision+1)||!ActionNpcImage(leg,true,false))
        {leg.State=ActionLegState.Unknown;return false;}
        leg.State=ActionLegState.Debited;leg.Progress=ItemStack.Clone(leg.After);
        ActionRecord(payment,"source-"+index+"-debited",new XElement("published",new XAttribute("mutation",leg.DebitMutation),new XAttribute("revision",receipt.AfterRevision)));return true;
    }
    private static bool SaveActionLeg(ActionPayment payment,ActionLeg leg,string phase)
    {
        if(leg.Local)return true;
        return leg.Npc!=null?SaveActionNpc(payment,leg,phase):SaveActionNative(payment,leg,phase,phase=="before"||phase=="return"?leg.Before:leg.After);
    }
    private static bool InvokeActionNpcReturn(ActionPayment payment,ActionLeg leg,int index)
    {
        string intent="source-"+index+"-return-intent";
        if(leg.ReturnCommit==null)
        {
            if(payment.Events.ContainsKey(intent)||!ActionLegCurrent(payment,leg)||!ActionNpcImage(leg,true,false)){leg.State=ActionLegState.Unknown;return false;}
            ActionRecord(payment,intent,new XElement("fullOriginal",ActionFrame(leg.Before)));
            leg.State=ActionLegState.ReturnAttempted;actionNpcPayment=payment;actionNpcLeg=leg;actionNpcPermitUsed=false;
            try
            {
                AuditActionInvocation(payment,leg,-1,true);
                if(leg.Npc.IsDogStorage)
                {
                    // Fresh canonical revision, never the pre-debit staging adapter.
                    var current=new RemoteNpcResourceSource(leg.Npc.NativeActor);
                    foreach(int slot in leg.Slots)current.SetSlot(slot,leg.Before[slot]);
                    current.MarkModifiedOriginal(leg.ReturnMutation);leg.ReturnCommit=current.LastCommitEvent;
                }
                else
                {
                    var additions=leg.Paid.Select(stack=>new RebirthNpcInventoryMutation(stack.itemValue.ItemClass.GetItemName(),stack.count)).ToArray();
                    using(var authority=RebirthNpcInventoryAuthorityService.Issue("remote.resources",RebirthNpcInventoryAuthorityOperations.Mutate,TimeSpan.FromSeconds(10),leg.Npc.NativeIdentity))
                    {
                        var result=RebirthNpcInventoryTransactionService.Apply(new RebirthNpcInventoryTransaction(leg.ReturnMutation,leg.Npc.NativeIdentity,checked((uint)leg.Revision+1),authority.AuthorityKey,additions),out uint revision);
                        bool published=result==RebirthNpcInventoryTransactionResult.Applied||result==RebirthNpcInventoryTransactionResult.Replayed;
                        leg.ReturnCommit=new RemoteNpcResourceSource.CommitEvent(leg.Npc,leg.ReturnMutation,checked((uint)leg.Revision+1),revision,published?RemoteNpcResourceSource.CommitDisposition.Published:RemoteNpcResourceSource.CommitDisposition.Refused);
                    }
                }
            }
            catch{leg.State=ActionLegState.Unknown;throw;}
            finally{actionNpcPayment=null;actionNpcLeg=null;actionNpcPermitUsed=false;}
        }
        var receipt=leg.ReturnCommit;
        if(receipt==null||receipt.Mutation!=leg.ReturnMutation||receipt.BeforeRevision!=checked((uint)leg.Revision+1)||receipt.AfterRevision!=checked((uint)leg.Revision+2)||receipt.Disposition!=RemoteNpcResourceSource.CommitDisposition.Published||!ActionNpcImage(leg,false,true))
        {leg.State=ActionLegState.Unknown;return false;}
        leg.State=ActionLegState.Returned;leg.Progress=ItemStack.Clone(leg.Before);
        ActionRecord(payment,"source-"+index+"-returned",new XElement("mutation",leg.ReturnMutation));return true;
    }
    internal static bool AdvanceElectricalReturn(ActionPayment payment)
    {
        if(payment?.ActorCurrent()!=true||payment.PublicationEntered||payment.State!=ActionPaymentState.Returning)return false;
        try
        {
            if(!FlushActionRefusals(payment))return false;
            for(int index=payment.Legs.Count-1;index>=0;index--)
            {
                var leg=payment.Legs[index];
                if(leg.State==ActionLegState.Unknown)return false;
                if(leg.State==ActionLegState.Refused)RecordActionRefusal(payment,leg);
                if(leg.Npc!=null)
                {
                    if(leg.State==ActionLegState.Debited||leg.State==ActionLegState.ReturnAttempted)
                        if(!InvokeActionNpcReturn(payment,leg,index))return false;
                }
                else
                {
                    for(int take=leg.Slots.Length-1;take>=0;take--)
                    {
                        if(!leg.DebitedSlots[take])continue;
                        if(!InvokeActionSlot(payment,leg,index,take,true))return false;
                    }
                    if(leg.DebitedSlots.Any(value=>value))leg.State=ActionLegState.Returned;
                }
                if(leg.State==ActionLegState.Returned&&!leg.ReturnSaved){if(!SaveActionLeg(payment,leg,"return"))return false;leg.ReturnSaved=true;}
            }
            if(!payment.OwnerReturnSaved){if(!SaveActionOwner(payment,"return"))return false;payment.OwnerReturnSaved=true;}
            ActionRecord(payment,"returned");payment.State=ActionPaymentState.Returned;payment.Terminal=true;ReleaseActionOwner(payment);return true;
        }
        catch(Exception error){RemoteResourceDiagnostics.Write("Action return retained "+payment.Operation+": "+error.Message);return false;}
    }
    internal static bool AdvanceElectricalPayment(ActionPayment payment)
    {
        if(payment==null||!payment.TargetCurrent()||payment.State==ActionPaymentState.Uncertain||payment.State==ActionPaymentState.Returning||payment.State==ActionPaymentState.Returned)return false;
        try
        {
            if(payment.Original!=null&&!FlushActionRefusals(payment))return false;
            if(payment.State==ActionPaymentState.Prepared)
            {
                foreach(var leg in payment.Legs)if(!EnsureActionIdentity(payment,leg))return false;
                PrepareActionOriginal(payment);
                if(!FlushActionRefusals(payment))return false;
                foreach(var leg in payment.Legs)if(!leg.BeforeSaved){if(!SaveActionLeg(payment,leg,"before"))return false;leg.BeforeSaved=true;}
                if(!payment.OwnerBeforeSaved){if(!SaveActionOwner(payment,"before"))return false;payment.OwnerBeforeSaved=true;}
                // Native target Before must be published by the actual region
                // writer before the first source debit can be invoked.
                if(!SaveActionTarget(payment,false))return false;
                ActionRecord(payment,"paying");payment.State=ActionPaymentState.Paying;
            }
            for(int index=0;index<payment.Legs.Count;index++)
            {
                var leg=payment.Legs[index];
                if(leg.State==ActionLegState.Unknown){payment.State=ActionPaymentState.Uncertain;return false;}
                if(leg.State==ActionLegState.Refused){RecordActionRefusal(payment,leg);ActionRecord(payment,"returning");payment.State=ActionPaymentState.Returning;return false;}
                if(leg.State!=ActionLegState.Debited)
                {
                    if(leg.Npc!=null){if(!InvokeActionNpcDebit(payment,leg,index))return false;}
                    else
                    {
                        for(int take=0;take<leg.Slots.Length;take++)if(!InvokeActionSlot(payment,leg,index,take,false))return false;
                        leg.State=ActionLegState.Debited;
                    }
                }
                if(!leg.AfterSaved){if(!SaveActionLeg(payment,leg,"after"))return false;leg.AfterSaved=true;}
            }
            if(!payment.OwnerAfterSaved){if(!SaveActionOwner(payment,"after"))return false;payment.OwnerAfterSaved=true;}
            ActionRecord(payment,"paid");payment.State=ActionPaymentState.Paid;return true;
        }
        catch(Exception error){RemoteResourceDiagnostics.Write("Action payment retained "+payment.Operation+": "+error.Message);return false;}
    }
}
