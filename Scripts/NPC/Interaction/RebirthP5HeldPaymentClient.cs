using System;
using System.Collections.Generic;

// Client call sequence for the actor-scoped held-payment route (list / resolve / refunded). It is
// dormant until RebirthNpcInteractionCommandRouter.Dispatch routes SessionKey "held-payment" to
// RebirthSurvivorHirePayment.HandleActorScoped before NPC-session authorization; until then the
// server rejects these requests and every method reports that error to its callback.
internal static class RebirthP5HeldPaymentClient
{
    internal sealed class Row { public string Operation, Npc, Currency; public int Cost; }

    private static void Send(string payload,Action<RebirthNpcInteractionCommandResponse,string> done)
    {
        var command=new RebirthNpcInteractionCommand{RequestId="held:"+Guid.NewGuid().ToString("N"),SessionKey=RebirthSurvivorHirePayment.HeldSessionKey,
            Kind=RebirthNpcInteractionCommandKind.Hire,Payload=payload};
        RebirthNpcInteractionNetworkClient.RequestCommand(command,(projection,response,error)=>done(response,error));
    }
    internal static void List(Action<List<Row>,string> done)
    {
        Send("operation=list",(response,error)=>
        {
            var rows=new List<Row>();
            if(response?.Status!=RebirthNpcInteractionCommandStatus.Accepted){done(rows,!string.IsNullOrEmpty(error)?error:response?.Detail);return;}
            if(RebirthSurvivorHirePayment.Parse(response.ResponsePayload).TryGetValue("held",out var text)&&!string.IsNullOrEmpty(text))
                foreach(string part in text.Split(','))
                {
                    var f=part.Split('|');
                    if(f.Length==4&&int.TryParse(f[2],out int cost))rows.Add(new Row{Operation=f[0],Npc=f[1],Cost=cost,Currency=f[3]});
                }
            done(rows,null);
        });
    }
    // resolve -> (saved hire: settled) | (completable: OpenToComplete text, the payer must open the survivor) |
    // refund payload -> local restore -> refunded acknowledgement. Each step is idempotent, so a repeat or
    // a reload re-enters safely (marker/cell flags on the client, Refund flag and saved marker 4 on the server).
    internal static void Resolve(string operation,Action<string> message)
    {
        Send("operation=resolve;id="+operation,(response,error)=>
        {
            if(response==null){message(error);return;}
            if(response.Status!=RebirthNpcInteractionCommandStatus.Accepted){message(response.Detail);return;}
            var values=RebirthSurvivorHirePayment.Parse(response.ResponsePayload);
            if(!values.ContainsKey("refund")){message(response.Detail);return;}
            if(GameManager.Instance?.World?.GetPrimaryPlayer() is not EntityPlayerLocal player){message(Localization.Get("xuiRbNpcHireUnavailable"));return;}
            if(!RebirthSurvivorHirePayment.TryRefundLocal(player,null,response.ResponsePayload,out var op,out var refundError)){message(refundError);return;}
            Send("operation=refunded;id="+op,(ack,ackError)=>message(ack?.Status==RebirthNpcInteractionCommandStatus.Accepted?ack.Detail:(ackError??ack?.Detail)));
        });
    }
}
