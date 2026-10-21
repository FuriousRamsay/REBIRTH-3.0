using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static partial class RebirthNpcTradeService
{
    private sealed class LocalAttempt
    {
        internal EntityPlayerLocal Player;internal RebirthNpcInteractionContext Context;internal RebirthNpcTradeOriginal Original;
        internal OwnerAdmission Admission;internal HostPrepared Prepared;internal OwnerAdmission.LocalPermission Permission;
        internal RebirthNpcTradeOwnerExchange.LocalUnusedBefore UnusedBefore;internal HostUnused UnusedReceipt;internal bool UnusedCompleted;
        internal int Stage;internal long Next;
    }
    private static Dictionary<Guid,LocalAttempt> localAttempts=new Dictionary<Guid,LocalAttempt>();
    internal static Func<RebirthNpcTradeOwnerExchange.Release,bool>[] CaptureLocalOriginalRetirement(World originalWorld)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=false||localAttempts.Count>64||
            localAttempts.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.Player?.world,originalWorld)))
            throw new InvalidOperationException("Local trade retirement requires the exact outgoing originals");
        return localAttempts.Values.Select(value=>{var original=value.Original;
            return new Func<RebirthNpcTradeOwnerExchange.Release,bool>(release=>release.Matches(original));}).ToArray();
    }
    internal static RebirthNpcTradeOriginal[] CaptureLocalNativeReentryMembership(World originalWorld)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=false||localAttempts.Count>64||
            localAttempts.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.Player?.world,originalWorld)))
            throw new InvalidOperationException("Exact outgoing local native trade originals required");
        return localAttempts.Values.Select(value=>value.Original).ToArray();
    }
    internal static object CaptureLocalOriginalAuditState(EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread()||player==null||player.world?.IsRemote()!=false)return new{schema=1,available=false};
        return new{schema=1,available=true,topology="local",owner=RebirthNpcTradeOwnerExchange.CaptureAuditState(player),
            nativeJoin=RebirthNpcTradeLocalOwnerLoad.CaptureAuditState(player),
            attempts=localAttempts.Values.Where(value=>ReferenceEquals(value.Player,player)).Select(value=>new{
                operation=value.Original.Operation.ToString("N"),original=value.Original.Digest,stage=value.Stage,
                terminal=value.Prepared?.Terminal?.Digest??string.Empty,closed=value.Prepared?.ClosedRecord?.TerminalDigest??string.Empty}).ToArray()};
    }
    private static RebirthNpcSaveQuarantineDomain PrepareLocalOriginalQuarantine(World world)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||localAttempts.Count>64||
            localAttempts.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.Player?.world,world)))throw new InvalidOperationException("Local trade original scope unavailable");
        var records=localAttempts;var empty=new Dictionary<Guid,LocalAttempt>();var doc=LocalOriginalImage();string image=doc.OuterXml;
        var identity=records.Select(pair=>new{Key=pair.Key,Value=pair.Value,Player=pair.Value.Player,Original=pair.Value.Original,Context=pair.Value.Context,
            Admission=pair.Value.Admission,Prepared=pair.Value.Prepared,Permission=pair.Value.Permission,UnusedBefore=pair.Value.UnusedBefore,UnusedReceipt=pair.Value.UnusedReceipt}).ToArray();
        return new RebirthNpcSaveQuarantineDomain("trade-local-originals",doc,()=>ReferenceEquals(localAttempts,records)&&records.Count==identity.Length&&
            identity.All(row=>records.TryGetValue(row.Key,out var value)&&ReferenceEquals(value,row.Value)&&ReferenceEquals(value.Player,row.Player)&&
                ReferenceEquals(value.Original,row.Original)&&ReferenceEquals(value.Context,row.Context)&&ReferenceEquals(value.Admission,row.Admission)&&
                ReferenceEquals(value.Prepared,row.Prepared)&&ReferenceEquals(value.Permission,row.Permission)&&ReferenceEquals(value.UnusedBefore,row.UnusedBefore)&&
                ReferenceEquals(value.UnusedReceipt,row.UnusedReceipt))&&LocalOriginalImage().OuterXml==image,
            ()=>localAttempts=empty,()=>localAttempts=records);
    }
    private static System.Xml.XmlDocument LocalOriginalImage()
    {
        var doc=new System.Xml.XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeLocalOriginals");doc.AppendChild(root);
        foreach(var pair in localAttempts.OrderBy(value=>value.Key))
        {
            var value=pair.Value;if(value?.Original==null||value.Original.Operation!=pair.Key||value.Stage<0||value.Stage>6)throw new InvalidOperationException("Local trade original missing");
            var row=doc.CreateElement("attempt");root.AppendChild(row);row.SetAttribute("operation",pair.Key.ToString("N"));row.SetAttribute("digest",value.Original.Digest);
            row.SetAttribute("stage",value.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture));row.SetAttribute("next",value.Next.ToString(System.Globalization.CultureInfo.InvariantCulture));
            row.SetAttribute("session",value.Context?.SessionKey??string.Empty);row.SetAttribute("prepared",value.Prepared?.PathName??string.Empty);
            row.SetAttribute("terminal",value.Prepared?.Terminal?.Digest??string.Empty);row.SetAttribute("closed",value.Prepared?.ClosedRecord?.TerminalDigest??string.Empty);
            row.SetAttribute("unused",value.UnusedReceipt?.Digest??string.Empty);row.SetAttribute("unusedCompleted",value.UnusedCompleted?"1":"0");
            row.InnerText=value.Original.Image;
        }
        return doc;
    }
    private static RebirthNpcInteractionCommandResponse CommitLocalOriginal(RebirthNpcInteractionContext context,Dictionary<string,string> values)
    {
        try
        {
            var player=GameManager.Instance?.World?.GetPrimaryPlayer();
            if(!ThreadManager.IsMainThread()||!TradeScope()||player==null||player.world.IsRemote()||context==null||
                !TryGet(values,"quote",out string text)||!Guid.TryParse(text,out var operation)||operation==Guid.Empty)return Reject("Local native trade original is unavailable.");
            if(localAttempts.TryGetValue(operation,out var attempt))
            {
                if(!ReferenceEquals(attempt.Player,player)||attempt.Original.CopyQuote().ActorId!=context.ActorId||attempt.Original.CopyQuote().NpcId!=context.NpcId)return Reject("Trade original belongs to another native owner.");
            }
            else
            {
                if(localAttempts.Count>=64||localAttempts.Values.Any(value=>ReferenceEquals(value.Player,player)))return Reject("Another local trade original remains held.");
                RebirthNpcTradeQuote quote;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(operation,out var record)||record.Consumed||record.Resolution!=0||record.Quote.ExpiresClockTicks<=DateTime.UtcNow.Ticks||
                        record.Quote.ActorId!=context.ActorId||record.Quote.NpcId!=context.NpcId)return Reject("Trade quotation is unavailable.");
                    quote=record.Quote;
                }
                if(!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out var original))return Reject("Native trade original could not be retained.");
                attempt=new LocalAttempt{Player=player,Context=context,Original=original};localAttempts.Add(operation,attempt);
            }
            bool finished=PumpLocalOriginal(attempt);
            return finished&&attempt.UnusedCompleted?Reject("Expired trade quotation released unused."):
                finished?Accept("Native trade original committed.","quote="+operation.ToString("N")+";total="+attempt.Original.CopyQuote().TotalPrice):
                Reject("Native trade original is retained for continuation. Quote "+operation.ToString("N"));
        }
        catch{return Reject("Native trade original is retained for continuation.");}
    }
    internal static void TickLocalOriginals()
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return;
        TickClosedSocial();
        RebirthNpcTradeLocalOwnerLoad.Update();
        long now=DateTime.UtcNow.Ticks;
        foreach(var value in localAttempts.Values.Where(value=>value.Next<=now).Take(4).ToArray())PumpLocalOriginal(value);
    }
    internal static bool ResumeLocalLoaded(RebirthNpcTradeLocalOwnerLoad.Source source)
    {
        try
        {
            var player=source?.Player;
            if(source==null||!TradeScope()||!source.Current(player)||!RebirthNpcTradeOwnerOriginalStore.TryReadRetained(player,source.Operation,out var original)||
                !source.Matches(original)||localAttempts.Values.Any(value=>value.Original.Operation!=original.Operation&&ReferenceEquals(value.Player,player)))return false;
            if(localAttempts.TryGetValue(original.Operation,out var prior)&&prior.Original.Image!=original.Image)return false;
            if(RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(player,original,out bool wasUnused))
            {
                if(!wasUnused&&(!RebirthNpcTradeClosedHistory.TryReadLocal(source,original,out var finalHistory)||!finalHistory.TrySaveSocialCompletion()))return false;
                if(!RebirthNpcTradeOwnerExchange.TryReleaseHistorical(source,original))return false;
                localAttempts.Remove(original.Operation);return true;
            }
            if(RebirthNpcTradeClosedHistory.TryReadLocal(source,original,out var closedHistory))
            {
                if(!closedHistory.TrySaveSocialCompletion())return false;
                if(!RebirthNpcTradeOwnerExchange.TryReleaseLocalHostHistory(source,original,closedHistory))return false;
                localAttempts.Remove(original.Operation);return true;
            }
            if(HostUnused.TryRetainLocal(source,original,out var unused))
            {
                if(!RebirthNpcTradeOwnerExchange.TryReleaseLocalUnused(source,unused))return false;
                localAttempts.Remove(original.Operation);return true;
            }
            if(prior==null&&localAttempts.Count>=64||!RebirthNpcTradeOwnerExchange.TryHoldLoaded(source,original,out int phase)||
                !HostPrepared.TryReattachLocal(source,out var admission,out var prepared)||
                !OwnerAdmission.LocalPermission.TryCaptureRetained(admission,prepared,source,out var permission))return false;
            if(phase==3&&(prepared.Terminal==null||!RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(player,original,out var terminal)||terminal!=prepared.Terminal.Digest))return false;
            var attempt=new LocalAttempt{Player=player,Original=permission.Original,Admission=admission,Prepared=prepared,Permission=permission,
                Stage=phase==3?4:phase==2?2:1};
            if(!source.Transfer(permission))return false;
            localAttempts[original.Operation]=attempt;PumpLocalOriginal(attempt);return true;
        }
        catch{return false;}
    }
    private static bool PumpLocalOriginal(LocalAttempt attempt)
    {
        attempt.Next=DateTime.UtcNow.AddSeconds(1).Ticks;
        try
        {
            if(!TradeScope()||GameManager.Instance?.World?.worldState?.Guid!=attempt.Original.World)return false;
            if(attempt.Stage!=6&&(!ReferenceEquals(attempt.Player,GameManager.Instance?.World?.GetPrimaryPlayer())||
                !RebirthNpcTradeOwnerOriginalStore.IsRetained(attempt.Player,attempt.Original)))return false;
            if(attempt.Stage==0)
            {
                if(attempt.UnusedBefore==null&&!RebirthNpcTradeOwnerExchange.TryHoldBefore(attempt.Player,attempt.Original))return false;
                if(attempt.UnusedBefore==null&&attempt.Prepared==null&&attempt.Admission!=null)
                    HostPrepared.TryRecoverLocalPublication(attempt.Admission,out attempt.Prepared);
                if(attempt.UnusedBefore!=null||attempt.Prepared==null&&attempt.Original.CopyQuote().ExpiresClockTicks<=DateTime.UtcNow.Ticks)
                {
                    if(attempt.UnusedBefore==null)
                    {
                        if(!RebirthNpcTradeOwnerExchange.LocalUnusedBefore.TryCapture(attempt.Player,attempt.Original,out var unentered)||
                            !HostUnused.TryRetainLocalWarm(unentered,out var unused))return false;
                        attempt.UnusedBefore=unentered;attempt.UnusedReceipt=unused;
                    }
                    if(!attempt.UnusedBefore.TryRelease(attempt.UnusedReceipt))return false;
                    attempt.UnusedCompleted=true;attempt.Stage=6;localAttempts.Remove(attempt.Original.Operation);return true;
                }
                if(attempt.Admission==null&&!OwnerAdmission.TryCaptureLocal(attempt.Context,attempt.Original,out attempt.Admission))return false;
                if(attempt.Prepared==null&&!HostPrepared.TryRecoverLocalPublication(attempt.Admission,out attempt.Prepared)&&
                    !HostPrepared.TryRetain(attempt.Admission,out attempt.Prepared))return false;
                if(!OwnerAdmission.LocalPermission.TryCapture(attempt.Admission,attempt.Prepared,out attempt.Permission))return false;
                attempt.Stage=1;
            }
            if(attempt.Stage==1)
            {
                if(!RebirthNpcTradeOwnerExchange.TryApplyLocal(attempt.Player,attempt.Permission))return false;
                attempt.Stage=2;
            }
            if(attempt.Stage==2)
            {
                if(!OwnerAdmission.SavedAfter.TryCaptureLocal(attempt.Permission,out var after)||!HostTerminal.TrySettle(after,out var terminal))return false;
                attempt.Prepared.Terminal=terminal;attempt.Stage=3;
            }
            if(attempt.Stage==3)
            {
                if(!RebirthNpcTradeOwnerExchange.TryAcknowledgeLocal(attempt.Player,attempt.Permission,attempt.Prepared.Terminal))return false;
                attempt.Stage=4;
            }
            if(attempt.Stage==4)
            {
                if(attempt.Prepared.ClosedRecord==null&&!HostClosed.TryCloseLocal(attempt.Permission,out _))return false;
                if(!attempt.Permission.ClosedCurrent(attempt.Prepared.ClosedRecord))return false;attempt.Stage=5;
            }
            if(attempt.Stage==5)
            {
                if(!attempt.Prepared.ClosedRecord.TrySaveSocialCompletion())return false;
                if(!RebirthNpcTradeOwnerExchange.TryReleaseLocal(attempt.Player,attempt.Permission,attempt.Prepared.ClosedRecord))return false;
                attempt.Stage=6;
            }
            if(attempt.Stage==6)
            {
                if(attempt.Prepared?.ClosedRecord?.CanRetireFromQuote!=true)return false;
                localAttempts.Remove(attempt.Original.Operation);Interlocked.Increment(ref committed);return true;
            }
            return false;
        }
        catch{return false;}
    }
}
