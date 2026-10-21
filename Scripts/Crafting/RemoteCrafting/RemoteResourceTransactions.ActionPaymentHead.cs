using System;
using System.IO;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    // Called within ActionRecord's reserved scratch. The full immutable event,
    // rather than a stage name/hash, is the positive latest issued permission.
    private static void PublishActionHead(ActionPayment payment,int sequence,byte[] image)
    {
        ActionPath(payment.Directory);
        string head=Path.Combine(payment.Directory,"current.xml");
        if(File.Exists(head))
        {
            byte[] current=ActionHeadBytes(head);
            // A publish/readback failure can leave this exact new head durable.
            if(current.SequenceEqual(image))return;
            if(sequence==0)throw new IOException("Action current head conflicts with initial original");
            string priorStage=payment.Events.Single(pair=>pair.Value==sequence-1).Key;
            string prior=Path.Combine(payment.Directory,(sequence-1).ToString("D6")+"-"+priorStage+".xml");
            if(!current.SequenceEqual(ActionHeadBytes(prior)))throw new IOException("Action current head differs from retained causal predecessor");
        }
        else if(sequence!=0)throw new IOException("Action current head was lost; no later permission may be issued");

        string temporary=Path.Combine(payment.Directory,"current-"+sequence.ToString("D6")+".pending");
        // An interrupted temporary is deliberately preserved. It is never a
        // current-head issuer and cold discovery must not promote it.
        if(!File.Exists(temporary))
        {using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {output.Write(image,0,image.Length);output.Flush(true);}}
        else if(!ActionBytes(temporary,image.Length).SequenceEqual(image))throw new IOException("Interrupted action current staging conflicts; original retained");
        ObserveActionHeadScenario(payment,"head-staged");
        if(!ActionBytes(temporary,image.Length).SequenceEqual(image))throw new IOException("Action current staging readback differs");
        if(!RebirthDurableFileCommit.TryPublish(temporary,head,out string error))throw new IOException("Action current publication failed: "+error);
        ObserveActionHeadScenario(payment,"head-published");
        if(!ActionBytes(head,image.Length).SequenceEqual(image))throw new IOException("Action current publication readback differs");
    }
    private static void ObserveActionHeadScenario(ActionPayment payment,string boundary)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")ActionPaymentScenarioHeadBoundary?.Invoke(payment.Operation,boundary);
#endif
    }
#if REBIRTH_DEBUG
    internal static Action<Guid,string> ActionPaymentScenarioHeadBoundary;
#endif
    private static byte[] ActionHeadBytes(string path)
    {
        ActionPath(path);
        long length=new FileInfo(path).Length;
        if(length<1||length>24L*1024*1024)throw new InvalidDataException("Action current head exceeds bound");
        return ActionBytes(path,checked((int)length));
    }
    private static void ValidateActionHead(ActionPayment payment)
    {
        if(payment.Sequence<1)throw new IOException("Action has no retained current event");
        string stage=payment.Events.Single(pair=>pair.Value==payment.Sequence-1).Key;
        string expected=Path.Combine(payment.Directory,(payment.Sequence-1).ToString("D6")+"-"+stage+".xml");
        if(!ActionHeadBytes(Path.Combine(payment.Directory,"current.xml")).SequenceEqual(ActionHeadBytes(expected)))
            throw new IOException("Action latest issued permission differs from its retained current event");
    }
}
