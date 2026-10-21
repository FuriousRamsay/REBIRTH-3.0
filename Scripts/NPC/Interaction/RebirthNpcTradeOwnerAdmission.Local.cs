using System;
using System.IO;

public static partial class RebirthNpcTradeService
{
    internal sealed partial class OwnerAdmission
    {
        internal static bool TryCaptureLocalRetained(RebirthNpcTradeLocalOwnerLoad.Source ownerSource,HostPrepared.RetainedSource source,out OwnerAdmission admission)
        {
            admission=null;
            try
            {
                var player=ownerSource?.Player;var original=source?.Original;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(ownerSource==null||source==null||!source.IsRetained()||!ownerSource.Matches(original)||!ownerSource.PersistCurrent(original)||
                    manager?.IsServer!=true||!HostQuote.TryReadEntered(original,out var issued)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                    !RebirthGearNativePlayerFile.TryRead(owner,out var saved))return false;
                QuoteRecord record;
                lock(Sync)
                {
                    if(Quotes.TryGetValue(original.Operation,out record))
                    {if(!record.Consumed||record.Resolution!=0||record.OwnerUnused!=null||!original.MatchesQuote(record.Quote))return false;}
                    else record=new QuoteRecord{Quote=original.CopyQuote(),Issued=issued,Consumed=true};
                }
                var value=new OwnerAdmission(original,source.NpcPlan,source.SavedNpcBefore,player.world,player,null,null,manager,saved,null,
                    original.CopyQuote().ActorId,0,RebirthNpcNetworkEpoch.GetServerEpoch(),record,true,true);
                if(!value.LocalMetadataCurrent()||!ownerSource.Matches(original))return false;admission=value;return true;
            }
            catch{return false;}
        }
        internal static bool TryCaptureLocal(RebirthNpcInteractionContext context,RebirthNpcTradeOriginal original,out OwnerAdmission admission)
        {
            admission=null;
            try
            {
                var world=GameManager.Instance?.World;var player=world?.GetPrimaryPlayer();var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(!ThreadManager.IsMainThread()||context==null||original==null||!TradeScope()||world?.IsRemote()!=false||manager?.IsServer!=true||
                    player==null||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
                    original.NativeId!=player.entityId||original.Deaths!=player.Died||original.World!=world.worldState?.Guid||
                    original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=original.Account||
                    !RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original)||
                    RebirthNpcInteractionSessionService.Authorize(context.SessionKey,context.ActorId,RebirthNpcInteractionAction.Trade,out var actual)!=RebirthNpcInteractionResult.Allowed||
                    actual.NpcId!=context.NpcId||actual.NpcId!=original.CopyQuote().NpcId||context.ActorId!=original.CopyQuote().ActorId||
                    !RebirthNpcTradeReach.CanBegin(player,context.NpcId))return false;
                QuoteRecord record;
                lock(Sync)
                    if(!Quotes.TryGetValue(original.Operation,out record)||record.Consumed||record.Resolution!=0||!original.MatchesQuote(record.Quote)||
                        record.Quote.ExpiresClockTicks<=DateTime.UtcNow.Ticks||record.Quote.RecoveryGeneration!=RebirthNpcRecoveryGeneration.RequireCurrent())return false;
                if(!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||!Before(saved,original,player)||
                    !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out int step,out int phase)||step!=0||phase!=1||
                    !RebirthNpcTradeNpcPlan.TryCreate(original,RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId),out var plan))return false;
                string npcBefore=RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(original,plan,false);if(npcBefore==null)return false;
                var value=new OwnerAdmission(original,plan,npcBefore,world,player,null,null,manager,saved,context.SessionKey,context.ActorId,0,
                    RebirthNpcNetworkEpoch.GetServerEpoch(),record,false,true);
                if(!value.Current())return false;admission=value;return true;
            }
            catch{return false;}
        }
        private bool LocalMetadataCurrent()
        {
            try
            {
                return local&&ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world?.IsRemote()==false&&
                    ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&
                    ReferenceEquals(actor,world.GetPrimaryPlayer())&&ReferenceEquals(actor,world.GetEntity(nativeId))&&actor.entityId==nativeId&&
                    ReferenceEquals(actor.world,world)&&!actor.IsDead()&&actor.IsSpawned()&&actor.Died==Original.Deaths&&world.worldState?.Guid==Original.World&&
                    RebirthCamperCharacterAccess.CurrentCreation(actor)==Original.Creation&&Path.GetFullPath(GameIO.GetSaveGameDir())==directory&&
                    RebirthNpcNetworkEpoch.GetServerEpoch()==epoch&&epoch!=0&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==Original.Account&&
                    RebirthNpcTradeOwnerOriginalStore.IsRetained(actor as EntityPlayerLocal,Original);
            }
            catch{return false;}
        }
        private bool CurrentLocal(HostPrepared prepared,bool completed,PlayerDataFile afterHead,float expectedPhase)
        {
            try
            {
                if(!LocalMetadataCurrent()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved))return false;
                lock(Sync)
                    if(!Quotes.TryGetValue(Original.Operation,out var actual)||!ReferenceEquals(actual,quote)||actual.Resolution!=0||!Original.MatchesQuote(actual.Quote)||
                        (prepared==null?actual.Consumed:!actual.Consumed||!ReferenceEquals(actual.OwnerPreparation,prepared)))return false;
                if(prepared==null&&(quote.Quote.ExpiresClockTicks<=DateTime.UtcNow.Ticks||
                    RebirthNpcInteractionSessionService.Authorize(session,actorKey,RebirthNpcInteractionAction.Trade,out var context)!=RebirthNpcInteractionResult.Allowed||context.NpcId!=Original.CopyQuote().NpcId))return false;
                if(completed)return afterHead!=null&&After(afterHead,Original,actor,expectedPhase)&&After(saved,Original,actor,expectedPhase)&&
                    RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(actor as EntityPlayerLocal,Original,out int step,out int phase)&&phase==expectedPhase&&step==RebirthNpcTradePrefix.Changes(Original)*2;
                return (reattached?RetainedPrefix(saved,Original,actor):Before(saved,Original,actor))&&
                    RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(actor as EntityPlayerLocal,Original,out int beforeStep,out int beforePhase)&&(reattached||beforeStep==0)&&beforePhase==1&&
                    RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(Original,NpcPlan,false)!=null;
            }
            catch{return false;}
        }
        internal sealed class LocalPermission
        {
            internal readonly RebirthNpcTradeOriginal Original;
            internal readonly HostPrepared.HostGrant Grant;
            private readonly OwnerAdmission admission;
            internal OwnerAdmission Admission=>admission;
            private LocalPermission(OwnerAdmission admission,HostPrepared.HostGrant grant)
            {this.admission=admission;Grant=grant;Original=admission.Original;}
            internal static bool TryCaptureRetained(OwnerAdmission admission,HostPrepared prepared,RebirthNpcTradeLocalOwnerLoad.Source source,out LocalPermission permission)
            {
                permission=null;
                try
                {
                    if(admission?.local!=true||!admission.reattached||prepared==null||!ReferenceEquals(prepared.Original,admission.Original)||
                        source==null||!source.Matches(admission.Original)||!source.TryStage(out int step,out int phase))return false;
                    var grant=HostPrepared.HostGrant.Create(prepared);
                    if(!grant.IsRetained()&&(phase!=1||step!=0||!prepared.TryIssueGrant(out grant)))return false;
                    var value=new LocalPermission(admission,grant);if(!value.Current()||!source.Matches(admission.Original))return false;permission=value;return true;
                }
                catch{return false;}
            }
            internal static bool TryCapture(OwnerAdmission admission,HostPrepared prepared,out LocalPermission permission)
            {
                permission=null;
                try
                {
                    if(admission?.local!=true||prepared==null||!ReferenceEquals(prepared.Original,admission.Original)||!admission.CurrentReserved(prepared)||
                        !prepared.TryIssueGrant(out var grant))return false;
                    var value=new LocalPermission(admission,grant);if(!value.Current())return false;permission=value;return true;
                }
                catch{return false;}
            }
            internal bool Current()
            {
                if(!admission.LocalMetadataCurrent()||!Grant.IsRetained())return false;
                lock(Sync)return Quotes.TryGetValue(Original.Operation,out var record)&&ReferenceEquals(record,admission.quote)&&record.Consumed&&record.Resolution==0&&
                    ReferenceEquals(record.OwnerPreparation,Grant.Prepared)&&Original.MatchesQuote(record.Quote);
            }
            internal bool AcknowledgementCurrent()
            {
                try{return Current()&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&RebirthGearNativePlayerFile.TryRead(owner,out var saved)&&
                    admission.CurrentLocal(Grant.Prepared,true,saved,3f);}catch{return false;}
            }
            internal bool ClosedCurrent(HostClosed closed)
            {
                try
                {
                    if(closed==null||!ReferenceEquals(closed.Prepared,Grant.Prepared)||!admission.LocalMetadataCurrent()||!Grant.IsRetained()||!closed.CanRetireFromQuote)return false;
                    lock(Sync)return Quotes.TryGetValue(Original.Operation,out var record)&&ReferenceEquals(record,admission.quote)&&record.Consumed&&record.Resolution==1&&
                        ReferenceEquals(record.OwnerPreparation,Grant.Prepared)&&ReferenceEquals(Grant.Prepared.ClosedRecord,closed);
                }
                catch{return false;}
            }
        }
        internal sealed partial class SavedAfter
        {
            internal static bool TryCaptureLocal(LocalPermission permission,out SavedAfter observation)
            {
                observation=null;
                try
                {
                    if(permission==null||!permission.Current()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                        !RebirthGearNativePlayerFile.TryRead(owner,out var saved))return false;
                    var value=new SavedAfter(permission.Admission,permission.Grant,saved);
                    if(!value.Current())return false;observation=value;return true;
                }
                catch{return false;}
            }
        }
    }
}
