using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthNpcInteractionNetworkClient
{
    internal sealed class NativeTradeResult
    {
        internal readonly RebirthNpcTradeOriginal Original;
        internal readonly string TerminalDigest;
        internal readonly bool Closed;
        private readonly World world;private readonly EntityPlayerLocal player;private readonly uint connection;private readonly ulong generation;
        private NativeTradeResult(RebirthNpcTradeOriginal original,string terminal,bool closed,PendingRequest pending,EntityPlayerLocal player)
        {Original=original;TerminalDigest=terminal;Closed=closed;world=pending.World;connection=pending.ConnectionEpoch;generation=pending.Generation;this.player=player;}
        internal static bool TryCapture(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out NativeTradeResult result)
        {
            result=null;
            try
            {
                var scope=nativeTradeReply;var pending=scope?.Pending;
                if(!ThreadManager.IsMainThread()||pending==null||!ReferenceEquals(pending.NativeTradeOriginal,original)||
                    pending.NativeTradeOperation!="settleNative"&&pending.NativeTradeOperation!="ackNative")return false;
                bool closed=pending.NativeTradeOperation=="ackNative";
                string payload=scope.Response.ResponsePayload;if(payload==null||payload.Length>1024)return false;
                var fields=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var part in payload.Split(';')){int at=part.IndexOf('=');if(at<1)return false;string key=part.Substring(0,at);if(fields.ContainsKey(key))return false;fields.Add(key,part.Substring(at+1));}
                string operation=closed?"ownerClosed":"ownerTerminal";
                if(fields.Count!=4||!fields.TryGetValue("terminal",out var terminal)||terminal.Length!=64||
                    terminal.Any(c=>c<'0'||c>'9'&&c<'a'||c>'f')||closed&&terminal!=pending.NativeTradeTerminal||
                    payload!="operation="+operation+";quote="+original.Operation.ToString("N")+";original="+original.Digest+";terminal="+terminal)return false;
                var next=new NativeTradeResult(original,terminal,closed,pending,player);
                if(!next.Current())return false;result=next;return true;
            }
            catch{return false;}
        }
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world?.IsRemote()==true&&
            ReferenceEquals(player,world.GetPrimaryPlayer())&&connection!=0&&connection==RebirthNpcNetworkEpoch.ClientConnectionEpoch&&
            generation==RebirthNpcRecoveryProjectionScope.Current&&generation==Original.CopyQuote().RecoveryGeneration&&
            RebirthNpcTradeOwnerOriginalStore.IsRetained(player,Original);
    }
}
