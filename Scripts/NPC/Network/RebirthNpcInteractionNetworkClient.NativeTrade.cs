using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthNpcInteractionNetworkClient
{
    private sealed class NativeTradeReplyScope
    {
        internal PendingRequest Pending;
        internal RebirthNpcInteractionCommandResponse Response;
    }
    [ThreadStatic]private static NativeTradeReplyScope nativeTradeReply;
    internal static void RequestNativeTrade(RebirthNpcTradeOriginal original,string session,string operation,
        Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> callback,string terminal=null)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(original==null||player?.world?.IsRemote()!=true||!RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original)||
            operation!="prepareNative"&&operation!="settleNative"&&operation!="ackNative"||string.IsNullOrWhiteSpace(session)||
            operation=="ackNative"&&(terminal?.Length!=64||terminal.Any(c=>c<'0'||c>'9'&&c<'a'||c>'f'))||
            operation=="prepareNative"&&!RebirthNpcTradeOwnerExchange.TryHoldBefore(player,original)&&
                (!RebirthNpcTradeOwnerExchange.TryResumeHeld(player,original,out int heldPhase)||heldPhase!=1))
        {callback?.Invoke(null,null,"Native trade original is unavailable.");return;}
        var command=new RebirthNpcInteractionCommand{RequestId=session+":nativeTrade:"+original.Operation.ToString("N")+":"+operation,
            SessionKey=session,Kind=RebirthNpcInteractionCommandKind.Trade,
            Payload="operation="+operation+";quote="+original.Operation.ToString("N")+";original="+original.Digest+(operation=="ackNative"?";terminal="+terminal:"")};
        Send(RebirthNpcInteractionNetworkOperation.Command,-1,session,command,callback,original,operation,terminal);
    }
    private static void InvokeNativeTradeCallback(PendingRequest pending,RebirthNpcInteractionProjection projection,
        RebirthNpcInteractionCommandResponse response,string error)
    {
        if(nativeTradeReply!=null)throw new InvalidOperationException("Nested native trade response");
        bool eligible=pending.NativeTradeOriginal!=null&&string.IsNullOrEmpty(error)&&response?.Status==RebirthNpcInteractionCommandStatus.Accepted&&
            ReferenceEquals(pending.World,GameManager.Instance?.World)&&pending.World?.IsRemote()==true;
        if(eligible)nativeTradeReply=new NativeTradeReplyScope{Pending=pending,Response=response};
        try{pending.Callback?.Invoke(projection,response,error);}
        finally{nativeTradeReply=null;}
    }
    // Capturable only during the actual matching, consumed network reply.
    // UI dictionaries, retained grant files and equal frames cannot mint it.
    internal sealed class NativeTradeGrant
    {
        internal readonly RebirthNpcTradeOriginal Original;
        internal readonly string PreparationDigest,GrantDigest;
        private readonly World world;private readonly EntityPlayerLocal player;
        private readonly uint connection;private readonly ulong generation;
        private NativeTradeGrant(RebirthNpcTradeOriginal original,string preparation,string grant,PendingRequest pending,EntityPlayerLocal player)
        {Original=original;PreparationDigest=preparation;GrantDigest=grant;world=pending.World;connection=pending.ConnectionEpoch;generation=pending.Generation;this.player=player;}
        internal static bool TryCapture(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out NativeTradeGrant grant)
        {
            grant=null;
            try
            {
                var scope=nativeTradeReply;var pending=scope?.Pending;
                if(!ThreadManager.IsMainThread()||pending?.NativeTradeOperation!="prepareNative"||original==null||
                    !ReferenceEquals(pending.NativeTradeOriginal,original)||!RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original))return false;
                string payload=scope.Response.ResponsePayload;
                if(payload==null||payload.Length>1024)return false;
                var fields=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var part in payload.Split(';')){int at=part.IndexOf('=');if(at<1)return false;string key=part.Substring(0,at);if(fields.ContainsKey(key))return false;fields.Add(key,part.Substring(at+1));}
                if(fields.Count!=5||!fields.TryGetValue("operation",out var operation)||operation!="ownerGrant"||
                    !fields.TryGetValue("quote",out var quote)||quote!=original.Operation.ToString("N")||
                    !fields.TryGetValue("original",out var digest)||digest!=original.Digest||
                    !fields.TryGetValue("preparation",out var preparation)||!Hash(preparation)||
                    !fields.TryGetValue("grant",out var supplied)||!Hash(supplied))return false;
                string body=new XElement("npcTradeOwnerGrant",new XAttribute("version",1),new XAttribute("operation",quote),
                    new XAttribute("originalDigest",digest),new XAttribute("preparationDigest",preparation)).ToString(SaveOptions.DisableFormatting);
                if(RebirthRemoteResourceDurableJournal.Digest(body)!=supplied)return false;
                var next=new NativeTradeGrant(original,preparation,supplied,pending,player);
                if(!next.Current())return false;grant=next;return true;
            }
            catch{return false;}
        }
        private static bool Hash(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world?.IsRemote()==true&&
            ReferenceEquals(player,world.GetPrimaryPlayer())&&connection!=0&&connection==RebirthNpcNetworkEpoch.ClientConnectionEpoch&&
            generation==RebirthNpcRecoveryProjectionScope.Current&&generation==Original.CopyQuote().RecoveryGeneration&&
            RebirthNpcTradeOwnerOriginalStore.IsRetained(player,Original);
    }
}
