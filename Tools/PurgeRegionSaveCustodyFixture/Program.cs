using System;
class Program
{
    static int n;static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);n++;Console.WriteLine("PASS "+label);}
    static void Main()
    {
        object owner=new object(),utility=new object(),snapshot=new object();bool live=true;
        var custody=new RebirthPoiRegionSaveSnapshotCustody<object>(owner,utility,()=>live);
        Check(custody.CaptureKey(owner,42),"exact original queued key captured before reset admission");
        Check(!custody.MustRetain(utility,snapshot),"ordinary native snapshot free unchanged");custody.Defer();
        Check(custody.MustRetain(utility,snapshot),"deferred prequeued native snapshot retained before write");
        Check(custody.Capture(utility,snapshot,42),"original snapshot identity bound");
        Check(!custody.Capture(utility,new object(),42),"another snapshot cannot replace original custody");
        Check(!custody.CaptureKey(owner,43),"another chunk cannot borrow original custody");
        Check(!custody.MustRetain(new object(),snapshot),"another utility cannot retain original snapshot");
        Check(!custody.MustRetain(utility,new object()),"foreign snapshot is not retained");
        Check(!custody.Written(utility,snapshot,false)&&custody.MustRetain(utility,snapshot),"suppressed write cannot consume retry custody");
        Check(custody.Written(utility,snapshot,true)&&!custody.MustRetain(utility,snapshot),"positive original write permits exactly native free without replay");
        var lost=new RebirthPoiRegionSaveSnapshotCustody<object>(owner,utility,()=>live);lost.Capture(utility,snapshot,42);lost.Defer();live=false;
        Check(!lost.MustRetain(utility,snapshot),"replaced native owner cannot inject old snapshot into new save");
        Check(!lost.Written(utility,snapshot,true),"late write receipt cannot consume replaced custody");
        Console.WriteLine("RESULT "+n+" PASS; production exact-snapshot custody state; no native queue or gameplay claim.");
    }
}