using System;
using System.Collections.Generic;
using System.Linq;

internal static class RebirthNpcTradeClientFlow
{
    private sealed class Attempt
    {
        internal EntityPlayerLocal Player;internal RebirthNpcTradeOriginal Original;internal string Session;
        internal RebirthNpcInteractionNetworkClient.NativeTradeGrant Grant;
        internal RebirthNpcInteractionNetworkClient.NativeTradeResult Terminal;
        internal string TerminalDigest;
        internal Action<bool,string> Feedback;internal int Stage;internal bool InFlight;internal long Next;
    }
    private static Dictionary<Guid,Attempt> attempts=new Dictionary<Guid,Attempt>();
    internal static bool HasLifecycleCustody=>attempts.Count!=0;
    internal static RebirthNpcTradeOriginal[] CaptureNativeReentryMembership(World originalWorld)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=true||attempts.Count>64||
            attempts.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.Player?.world,originalWorld)))
            throw new InvalidOperationException("Exact original client trade membership required");
        return attempts.Values.Select(value=>value.Original).ToArray();
    }
    internal static Func<RebirthNpcTradeOwnerExchange.Release,bool>[] CaptureRetirement()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Trade original main thread required");
        return attempts.Values.Select(value=>{var original=value.Original;return new Func<RebirthNpcTradeOwnerExchange.Release,bool>(release=>release.Matches(original));}).ToArray();
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareQuarantine(World world)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||attempts.Count>64||
            attempts.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.Player?.world,world)))throw new InvalidOperationException("Trade flow original scope unavailable");
        var records=attempts;var empty=new Dictionary<Guid,Attempt>();var doc=LifecycleImage();string image=doc.OuterXml;
        var identity=records.Select(pair=>new{Key=pair.Key,Value=pair.Value,Player=pair.Value.Player,Original=pair.Value.Original,
            Grant=pair.Value.Grant,Terminal=pair.Value.Terminal,Feedback=pair.Value.Feedback}).ToArray();
        return new RebirthNpcSaveQuarantineDomain("trade-client-flow",doc,()=>ReferenceEquals(attempts,records)&&records.Count==identity.Length&&
            identity.All(row=>records.TryGetValue(row.Key,out var value)&&ReferenceEquals(value,row.Value)&&ReferenceEquals(value.Player,row.Player)&&
                ReferenceEquals(value.Original,row.Original)&&ReferenceEquals(value.Grant,row.Grant)&&ReferenceEquals(value.Terminal,row.Terminal)&&ReferenceEquals(value.Feedback,row.Feedback))&&LifecycleImage().OuterXml==image,
            ()=>attempts=empty,()=>attempts=records);
    }
    private static System.Xml.XmlDocument LifecycleImage()
    {
        var doc=new System.Xml.XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeClientFlow");doc.AppendChild(root);
        foreach(var pair in attempts.OrderBy(value=>value.Key))
        {
            var value=pair.Value;if(value?.Original==null||value.Original.Operation!=pair.Key||value.Stage<0||value.Stage>4)throw new InvalidOperationException("Trade flow original unavailable");
            var row=doc.CreateElement("attempt");root.AppendChild(row);row.SetAttribute("operation",pair.Key.ToString("N"));row.SetAttribute("digest",value.Original.Digest);
            row.SetAttribute("stage",value.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture));row.SetAttribute("inFlight",value.InFlight?"1":"0");
            row.SetAttribute("next",value.Next.ToString(System.Globalization.CultureInfo.InvariantCulture));row.SetAttribute("session",value.Session??string.Empty);
            row.SetAttribute("terminal",value.TerminalDigest??string.Empty);row.SetAttribute("feedback",value.Feedback!=null?"1":"0");
            row.SetAttribute("grant",value.Grant?.GrantDigest??string.Empty);row.SetAttribute("result",value.Terminal?.TerminalDigest??string.Empty);
            row.InnerText=value.Original.Image;
        }
        return doc;
    }
    internal static object CaptureAuditState(EntityPlayer actor)
    {
        if(!ThreadManager.IsMainThread()||actor is not EntityPlayerLocal player)return new{schema=1,available=false};
        if(player.world?.IsRemote()==false)return RebirthNpcTradeService.CaptureLocalOriginalAuditState(player);
        return new{schema=1,available=true,owner=RebirthNpcTradeOwnerExchange.CaptureAuditState(player),
            nativeJoin=RebirthNpcTradeOwnerLoad.CaptureAuditState(player),attempts=attempts.Values.Where(value=>ReferenceEquals(value.Player,player)).Select(value=>
                new{operation=value.Original.Operation.ToString("N"),original=value.Original.Digest,stage=value.Stage,
                    inFlight=value.InFlight,terminal=value.TerminalDigest??string.Empty}).ToArray()};
    }
    internal static bool ResumeLoaded(RebirthNpcTradeOwnerLoad.Source source)
    {
        try
        {
            var player=source?.Player;
            if(source==null||!source.Current(player)||
                !RebirthNpcTradeOwnerOriginalStore.TryReadRetained(player,source.Operation,out var original))return false;
            if(RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(player,original,out _))
            {
                if(!RebirthNpcTradeOwnerExchange.TryReleaseHistorical(source,original))return false;
                attempts.Remove(original.Operation);return true;
            }
            if(attempts.Values.Any(value=>value.Original.Operation!=original.Operation&&ReferenceEquals(value.Player,player)))return false;
            if(attempts.TryGetValue(original.Operation,out var prior)&&prior.Original.Digest!=original.Digest)return false;
            if(prior==null&&attempts.Count>=64||!RebirthNpcTradeOwnerExchange.TryHoldLoaded(source,original,out int phase))return false;
            string terminal=null;
            if(phase==3&&!RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(player,original,out terminal))return false;
            // Rejoin resumes the immutable operation; no expired UI projection
            // or parsed description supplies an item setter capability.
            var attempt=new Attempt{Player=player,Original=original,Session="native-trade:"+original.Operation.ToString("N"),
                Stage=phase==3?4:phase==2?2:0,TerminalDigest=terminal,Feedback=prior?.Feedback};
            attempts[original.Operation]=attempt;Pump(attempt);return true;
        }
        catch{return false;}
    }
    internal static bool Begin(RebirthNpcTradeQuote quote,RebirthNpcInteractionProjection projection,Action<bool,string> feedback)
    {
        try
        {
            var player=GameManager.Instance?.World?.GetPrimaryPlayer();
            if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||quote==null||projection==null||
                quote.ActorId!=projection.ActorId||quote.NpcId!=projection.NpcId||string.IsNullOrWhiteSpace(projection.SessionKey))return false;
            if(attempts.TryGetValue(quote.QuoteId,out var prior))
            {if(!ReferenceEquals(prior.Player,player))return false;prior.Feedback=feedback;return true;}
            if(attempts.Values.Any(value=>ReferenceEquals(value.Player,player)))return false;
            if(attempts.Count>=64||!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out var original))return false;
            var attempt=new Attempt{Player=player,Original=original,Session=projection.SessionKey,Feedback=feedback};
            attempts.Add(original.Operation,attempt);Pump(attempt);return true;
        }
        catch{return false;}
    }
    internal static void Update()
    {
        if(!ThreadManager.IsMainThread())return;
        RebirthNpcClientNativeReentry.Update();
        RebirthNpcTradeOwnerLoad.Update();
        if(attempts.Count==0)return;
        long now=DateTime.UtcNow.Ticks;
        // Event continuation, bounded and throttled. Never replan or start a
        // replacement operation because an original reply timed out.
        foreach(var attempt in attempts.Values.ToArray())
            if(!attempt.InFlight&&attempt.Next<=now)Pump(attempt);
    }
    private static void Pump(Attempt attempt)
    {
        attempt.Next=DateTime.UtcNow.AddSeconds(1).Ticks;
        if(attempt.InFlight||!ReferenceEquals(attempt.Player.world,GameManager.Instance?.World)||
            !RebirthNpcTradeOwnerOriginalStore.IsRetained(attempt.Player,attempt.Original))return;
        try
        {
            if(attempt.Stage==1&&(attempt.Grant==null||!attempt.Grant.Current())||
                attempt.Stage==3&&(attempt.Terminal==null||!attempt.Terminal.Current()))
            {
                if(!RebirthNpcTradeOwnerExchange.TryResumeHeld(attempt.Player,attempt.Original,out int heldPhase))return;
                if(heldPhase==3)
                {
                    if(!RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(attempt.Player,attempt.Original,out var terminal))return;
                    attempt.TerminalDigest=terminal;attempt.Stage=4;
                }
                else attempt.Stage=heldPhase==2?2:0;
            }
            if(attempt.Stage==0)
            {
                if(!RebirthNpcTradeOwnerExchange.TryHoldBefore(attempt.Player,attempt.Original)&&
                    (!RebirthNpcTradeOwnerExchange.TryResumeHeld(attempt.Player,attempt.Original,out int heldPhase)||heldPhase!=1))return;
                GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
                Request(attempt,"prepareNative");return;
            }
            if(attempt.Stage==1)
            {if(!RebirthNpcTradeOwnerExchange.TryApply(attempt.Player,attempt.Grant))return;attempt.Stage=2;}
            if(attempt.Stage==2)
            {GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);Request(attempt,"settleNative");return;}
            if(attempt.Stage==3)
            {if(!RebirthNpcTradeOwnerExchange.TryAcknowledge(attempt.Player,attempt.Terminal))return;attempt.Stage=4;}
            if(attempt.Stage==4)
            {GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);Request(attempt,"ackNative");}
        }
        catch{attempt.Feedback?.Invoke(false,"Trade continuation is retained.");}
    }
    private static void Request(Attempt attempt,string operation)
    {
        attempt.InFlight=true;
        RebirthNpcInteractionNetworkClient.RequestNativeTrade(attempt.Original,attempt.Session,operation,(projection,response,error)=>
        {
            attempt.InFlight=false;attempt.Next=DateTime.UtcNow.AddSeconds(1).Ticks;
            if(!attempts.TryGetValue(attempt.Original.Operation,out var actual)||!ReferenceEquals(actual,attempt))return;
            if(!string.IsNullOrEmpty(error)||response?.Status!=RebirthNpcInteractionCommandStatus.Accepted)
            {attempt.Feedback?.Invoke(false,!string.IsNullOrEmpty(error)?error:response?.Detail);return;}
            if(operation=="prepareNative")
            {
                if(RebirthNpcInteractionNetworkClient.NativeTradeUnused.TryCapture(attempt.Player,attempt.Original,out var unused))
                {
                    if(RebirthNpcTradeOwnerExchange.TryReleaseUnused(attempt.Player,unused))
                    {attempts.Remove(attempt.Original.Operation);attempt.Feedback?.Invoke(false,response.Detail);}
                    return;
                }
                if(!RebirthNpcInteractionNetworkClient.NativeTradeGrant.TryCapture(attempt.Player,attempt.Original,out var grant))return;
                attempt.Grant=grant;attempt.Stage=1;
            }
            else if(RebirthNpcInteractionNetworkClient.NativeTradeResult.TryCapture(attempt.Player,attempt.Original,out var result))
            {
                if(operation=="settleNative"&&!result.Closed){attempt.Terminal=result;attempt.TerminalDigest=result.TerminalDigest;attempt.Stage=3;}
                else if(operation=="ackNative"&&result.Closed&&RebirthNpcTradeOwnerExchange.TryRelease(attempt.Player,result))
                {attempts.Remove(attempt.Original.Operation);attempt.Feedback?.Invoke(true,response.Detail);}
            }
        },operation=="ackNative"?attempt.TerminalDigest:null);
    }
}
