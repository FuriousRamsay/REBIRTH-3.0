using System;
using System.Linq;
using Platform;

public static partial class RebirthNpcTradeService
{
    // Called only at the authenticated native NPC packet boundary. Generic
    // router context is insufficient to prepare or settle an owner exchange.
    internal static bool TryRemoteOwnerCommand(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,
        string session,RebirthNpcInteractionCommand command,out RebirthNpcInteractionCommandResponse response)
    {
        response=null;if(command?.Kind!=RebirthNpcInteractionCommandKind.Trade)return false;
        var values=Parse(command.Payload);
        if(!values.TryGetValue("operation",out var operation)||operation!="prepareNative"&&operation!="settleNative"&&operation!="ackNative")return false;
        try
        {
            response=Reject("Native trade original is unavailable.");
            bool acknowledging=operation=="ackNative";string terminalDigest=null;
            if(acknowledging&&(!values.TryGetValue("terminal",out terminalDigest)||terminalDigest.Length!=64||terminalDigest.Any(value=>value<'0'||value>'9'&&value<'a'||value>'f')))return true;
            if(values.Count!=(acknowledging?4:3)||!values.TryGetValue("quote",out var text)||!Guid.TryParseExact(text,"N",out var id)||id==Guid.Empty||
                !values.TryGetValue("original",out var digest)||digest.Length!=64||digest.Any(value=>value<'0'||value>'9'&&value<'a'||value>'f')||
                command.Payload!="operation="+operation+";quote="+id.ToString("N")+";original="+digest+(acknowledging?";terminal="+terminalDigest:"")||packet==null||user==null||
                !packet.ValidEntityIdForSender(player)||!packet.ValidUserIdForSender(user)||!TradeScope()||
                !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection))return true;
            if(acknowledging&&RebirthNpcTradeClosedHistory.TryRead(packet,user,player,connection,id,digest,terminalDigest,out var history))
            {
                if(!history.TrySaveSocialCompletion())return true;
                response=Accept("Native trade original closed.","operation=ownerClosed;quote="+history.Operation.ToString("N")+
                    ";original="+history.OriginalDigest+";terminal="+history.TerminalDigest);
                return true;
            }
            if(operation=="prepareNative"&&RebirthNpcTradeUnusedHistory.TryRead(packet,user,player,connection,id,digest,out var unusedHistory))
            {
                response=Accept("Trade quotation was not used.","operation=ownerUnused;quote="+id.ToString("N")+";original="+digest+";unused="+unusedHistory.UnusedDigest);
                return true;
            }
            HostPrepared prepared;
            HostPrepared.TryReattach(packet,user,player,connection,id,digest);
            if(operation=="prepareNative")HostQuote.TryRestoreExpiredUnused(packet,user,player,connection,id);
            lock(Sync)
            {
                if(!Quotes.TryGetValue(id,out var quote)||quote.Quote.ActorId!=user.ToString()||quote.Resolution!=0&&(!acknowledging||quote.OwnerPreparation?.ClosedRecord==null))return true;
                prepared=quote.OwnerPreparation;
            }
            if(acknowledging)
            {
                if(prepared==null||prepared.Original.Digest!=digest)return true;
                var closed=prepared.ClosedRecord;
                if(closed==null&&!HostClosed.TryClose(prepared,terminalDigest,packet,user,out closed))return true;
                if(closed.TerminalDigest!=terminalDigest||!closed.CanReply(packet,user)||!closed.TrySaveSocialCompletion())return true;
                response=Accept("Native trade original closed.","operation=ownerClosed;quote="+id.ToString("N")+";original="+digest+";terminal="+terminalDigest);
                return true;
            }
            if(operation=="prepareNative")
            {
                if(prepared==null&&HostUnused.TryRetain(packet,user,connection,player,id,digest,out var unused))
                {
                    response=Accept("Trade quotation was not used.","operation=ownerUnused;quote="+id.ToString("N")+";original="+digest+";unused="+unused.Digest);
                    return true;
                }
                if(prepared==null)
                {
                    if(!OwnerAdmission.TryCaptureFromSaved(packet,user,session,connection,RebirthNpcNetworkEpoch.GetServerEpoch(),
                        player,id,digest,out var admission)||!HostPrepared.TryRetain(admission,out prepared))return true;
                }
                if(prepared.Original.Digest!=digest||!prepared.TryIssueGrant(out var grant))return true;
                response=Accept("Native trade original prepared.","operation=ownerGrant;quote="+id.ToString("N")+";original="+digest+
                    ";preparation="+RebirthRemoteResourceDurableJournal.Digest(prepared.Image)+";grant="+RebirthRemoteResourceDurableJournal.Digest(grant.Image));
                return true;
            }
            if(prepared==null||prepared.Original.Digest!=digest)return true;
            var retainedGrant=HostPrepared.HostGrant.Create(prepared);
            if(!prepared.TryObserveAfter(retainedGrant,packet,user,out var after)||!HostTerminal.TrySettle(after,out var terminal))return true;
            prepared.Terminal=terminal;
            response=Accept("Native trade original saved.","operation=ownerTerminal;quote="+id.ToString("N")+";original="+digest+";terminal="+terminal.Digest);
            return true;
        }
        catch{return true;}
    }
}
