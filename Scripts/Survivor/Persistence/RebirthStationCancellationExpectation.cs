using System.Xml.Linq;

// Owned cancellation expectation for demand-scoped native span observation; never refund authority.
internal sealed class RebirthStationCancellationExpectation
{
    private readonly RebirthStationNativeInputSnapshot input;
    private readonly RebirthStationNativeQueueSnapshot queue;
    private readonly RebirthStationTerminalSnapshot terminal;
    private RebirthStationCancellationExpectation(RebirthStationNativeInputSnapshot i,RebirthStationNativeQueueSnapshot q,RebirthStationTerminalSnapshot t)
    {input=i;queue=q;terminal=t;}
    internal static bool TryCreate(TileEntityWorkstation station,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        out RebirthStationCancellationExpectation expectation)
    {
        expectation=null;
        try
        {
            if(station==null||attempt==null||
                !RebirthStationCancellationAttempt.TryRead(attempt.Write(),admission,intent,refund,out _)||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,attempt.JobId)||
                !RebirthStationNativeInputSnapshot.TryCapture(station.Input,out var inputs)||
                !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queued)||
                !RebirthStationTerminalSnapshot.TryCapture(station,out var contents))return false;
            var image=attempt.Write();
            if(queued.Digest()!=(string)image.Attribute("after")||contents.Digest()!=(string)image.Attribute("terminal"))return false;
            expectation=new RebirthStationCancellationExpectation(inputs,queued,contents);return true;
        }
        catch{return false;}
    }
    internal bool MatchesLive(TileEntityWorkstation station)
    {return station!=null&&input.Matches(station.Input)&&queue.Matches(station.Queue)&&terminal.Matches(station);}
    internal bool MatchesStation(byte[] inputs,byte[] queued)
    {return input.MatchesSerializedInput(inputs)&&queue.MatchesSerializedQueue(queued);}
    internal bool MatchesTerminal(byte[] output,byte[] completion)
    {return terminal.MatchesSerializedTerminal(output,completion);}
}