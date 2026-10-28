// Actual production pure final-native-CVar decoder. No native file, owner,
// original journal, authority, transport or effect proof is simulated here.
public static class MpALogisticsSavedOwnerMarkerFixture
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Main()
    {
        var bytes=new byte[16];bytes[0]=7;var expected=new Guid(bytes);
        var values=new Dictionary<string,float>{{"rbLogisticsOwner0",7},{"rbLogisticsOwnerHeld",1}};
        Check(RebirthLogisticsSavedOwnerMarker.TryRead(values,out var operation,out var phase,out var held)&&operation==expected&&phase==0&&held==1,"native omitted phase0 and seven zero GUID words");
        values.Remove("rbLogisticsOwnerHeld");
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out _,out _),"absent Held cannot prepare an original");
        values["rbLogisticsOwnerPhase"]=4;
        Check(RebirthLogisticsSavedOwnerMarker.TryRead(values,out operation,out phase,out held)&&operation==expected&&phase==4&&held==0,"native terminal omitted Held0 accepted as syntax only");
        values["rbLogisticsOwnerPhase"]=2;
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out _,out _),"missing held not payment");
        values["rbLogisticsOwnerHeld"]=1;values["rbLogisticsOwnerPhase"]=5;
        Check(RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out phase,out held)&&phase==5&&held==1,"saved no-effect phase5 still held");
        values["rbLogisticsOwner1"]=float.NaN;
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out _,out _),"nonfinite native word refused");
        values["rbLogisticsOwner1"]=65536;
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out _,out _),"word overflow refused");
        values["rbLogisticsOwner1"]=0.5f;
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out _,out _),"fractional word refused");
        values.Remove("rbLogisticsOwner1");values["rbLogisticsOwner0"]=0;
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out _,out _,out _),"empty original refused despite held");
        Check(!RebirthLogisticsSavedOwnerMarker.TryRead(new Dictionary<string,float>(),out _,out _,out _),"empty native CVars never operation");
        Console.WriteLine("PASS actual saved logistics marker parser; native file/authority NOT VALIDATED");
    }
}
