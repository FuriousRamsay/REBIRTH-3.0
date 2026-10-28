using System;
internal enum RebirthPoiClearanceState {Discovered=0,Cleared=1,ResetPending=2}
internal static class Program
{
    static int passed;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);passed++;}
    static void Main()
    {
        foreach(string mode in new[]{"peer host/client","dedicated server/client"})
        {
            var scope=new object();var request=Guid.NewGuid();var world=Guid.NewGuid();var session=Guid.NewGuid();
            var id=new RebirthPoiIdentity("reported_poi",305,40,1641,0,20,20,20,"wasteland");
            var receiver=new RebirthPoiMapReceiver(scope,request);
            RebirthPoiMapFrame Frame(long sequence,long previous,long epoch,RebirthPoiClearanceState state,int areas,int enemies,string finder,string killers)
                =>new RebirthPoiMapFrame(request,world,session,Guid.NewGuid(),sequence,previous,sequence==1,0,1,1,
                    new[]{new RebirthPoiMapRecord(id,epoch,state,3,finder,killers,100,state==RebirthPoiClearanceState.Cleared?200UL:0UL,areas,enemies,true)});
            RebirthPoiMapFrame Wire(RebirthPoiMapFrame frame)
            {Check(RebirthPoiMapFrame.TryDecode(RebirthPoiMapFrame.Encode(frame),out var decoded),mode+" production codec");return decoded;}
            var discovered=Wire(Frame(1,0,1,RebirthPoiClearanceState.Discovered,16,17,"FuriousRamsay",""));
            Check(receiver.Accept(scope,discovered)==RebirthPoiMapReceive.Published,mode+" initial map publication");
            var shown=receiver.Published[id.Key];
            Check(shown.AreasLeft==16&&shown.EstimatedEntities==17&&shown.Approximate,mode+" 16 areas and17+ entities preserved");
            Check(shown.Discoverer=="FuriousRamsay"&&shown.DiscoveredAt==100,mode+" discovery attribution preserved");
            var purged=Wire(Frame(2,1,1,RebirthPoiClearanceState.Cleared,0,0,"FuriousRamsay","FuriousRamsay\nSecondPlayer"));
            Check(receiver.Accept(scope,purged)==RebirthPoiMapReceive.Published,mode+" purged delta");
            shown=receiver.Published[id.Key];
            Check(shown.Discoverer=="FuriousRamsay"&&shown.Contributors=="FuriousRamsay\nSecondPlayer"&&shown.PurgedAt==200,mode+" both discovery and ordered contributor history retained");
            Check(receiver.Accept(scope,discovered)==RebirthPoiMapReceive.Duplicate&&receiver.Published[id.Key].State==RebirthPoiClearanceState.Cleared,mode+" delayed discovery cannot roll back purge");
            var reset=Wire(Frame(3,2,2,RebirthPoiClearanceState.Discovered,-1,-1,"",""));
            Check(receiver.Accept(scope,reset)==RebirthPoiMapReceive.Published,mode+" new epoch publication");
            Check(receiver.Published[id.Key].AreasLeft==-1&&receiver.Published[id.Key].Contributors=="",mode+" new epoch carries no old counters/contributors");
            var stale=Wire(Frame(4,3,1,RebirthPoiClearanceState.Discovered,16,17,"OldFinder","OldKiller"));
            Check(receiver.Accept(scope,stale)==RebirthPoiMapReceive.Refused&&receiver.Published[id.Key].Epoch==2,mode+" old epoch rejected even at newer sequence");
        }
        Console.WriteLine($"PASS {passed} production codec/receiver assertions; simulated roles, no native transport or game execution.");
    }
}
