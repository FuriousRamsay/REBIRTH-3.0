using System;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthNpcInteractionNetworkClient
{
    internal sealed class NativeTradeUnused
    {
        internal readonly RebirthNpcTradeOriginal Original;
        internal readonly string Digest,Receipt;
        private readonly World world;private readonly EntityPlayerLocal player;private readonly uint connection;private readonly ulong generation;
        private NativeTradeUnused(EntityPlayerLocal player,RebirthNpcTradeOriginal original,string digest,PendingRequest pending)
        {
            this.player=player;Original=original;Digest=digest;world=pending.World;connection=pending.ConnectionEpoch;generation=pending.Generation;
            Receipt=RebirthNpcTradeOwnerReceipt.WriteUnused(original.Operation,original.Digest,digest);
        }
        internal static bool TryCapture(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out NativeTradeUnused unused)
        {
            unused=null;
            try
            {
                var scope=nativeTradeReply;var pending=scope?.Pending;
                if(!ThreadManager.IsMainThread()||pending?.NativeTradeOperation!="prepareNative"||original==null||
                    !ReferenceEquals(pending.NativeTradeOriginal,original))return false;
                string prefix="operation=ownerUnused;quote="+original.Operation.ToString("N")+";original="+original.Digest+";unused=";
                string payload=scope.Response.ResponsePayload;
                if(payload==null||payload.Length!=prefix.Length+64||!payload.StartsWith(prefix,StringComparison.Ordinal))return false;
                string digest=payload.Substring(prefix.Length);if(!digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))return false;
                var next=new NativeTradeUnused(player,original,digest,pending);if(!next.Current())return false;unused=next;return true;
            }
            catch{return false;}
        }
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world?.IsRemote()==true&&
            ReferenceEquals(player,world.GetPrimaryPlayer())&&connection!=0&&connection==RebirthNpcNetworkEpoch.ClientConnectionEpoch&&
            generation==RebirthNpcRecoveryProjectionScope.Current&&generation==Original.CopyQuote().RecoveryGeneration&&
            RebirthNpcTradeOwnerOriginalStore.IsRetained(player,Original);
    }
}
