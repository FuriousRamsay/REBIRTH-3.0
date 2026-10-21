using System;
using System.IO;

internal static partial class RebirthRepairOriginalCorrection
{
    private static bool CanRetireHistoricalTerminal(Client client)
    {
        return !Current(client)&&SamePhysicalActor(client)&&
            (client.Receipt.Published||client.Receipt.CancelConfirmed||client.Receipt.BaseOnlyConfirmed);
    }
    private static bool CanPumpOriginal(Client client)
    {
        return Current(client)||CanRetireHistoricalTerminal(client)||SamePhysicalActor(client)&&!client.Receipt.Published&&
            (client.Receipt.Cancelled&&client.Receipt.CancelReturned||client.Receipt.Admitted&&!client.Receipt.Cancelled);
    }
    private static void ConfirmHistoricalCancellation(EntityPlayer owner,Receipt request)
    {
        // This is withdrawal of bonus permission and metadata retirement only.
        // The native cancellation returned in its own saved owner episode; no
        // old tool/kit refund, output, correction or training is invoked here.
        if(!request.Cancelled||!request.CancelReturned||owner.Died<=request.Deaths)return;
        Receipt original;string expected=null;
        if(Read(request,true,out var saved))
        {
            original=saved;expected=Encode(saved);
            if(original.Published||original.After.Length!=0||original.BaseOnly||original.NativeDelivered||original.OutputInvoking)return;
        }
        else
        {
            string path=PathFor(request,true);
            if(File.Exists(path)||File.Exists(path+".bak")||request.Payment.Length!=0||request.Admitted||request.PaymentProof.Length!=0||
                request.Before.Length!=0||request.After.Length!=0||request.NativeDelivered||request.OutputInvoking||request.Published||request.BaseOnly)return;
            // A never-admitted, local-kit cancellation can only close its own
            // permission; it cannot mint a paid source or any native reward.
            original=Decode(Encode(request));
        }
        var terminal=Decode(Encode(original));terminal.Cancelled=true;terminal.CancelReturned=true;terminal.CancelConfirmed=true;
        if(!QueueAbsentProof(owner,terminal,true)||!Save(terminal,true,expected))return;
        Reply(owner,terminal);
    }
}
