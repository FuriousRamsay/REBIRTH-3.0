using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// PREPARED, NOT RUN. Invoke around actual authenticated request/native fatal;
// this fixture never authors saved owner, lifecycle or effect evidence.
internal sealed class mpA_logistics_pre_admission_fixture
{
    private readonly string path;private readonly string encoded;
    internal mpA_logistics_pre_admission_fixture(string owner,Guid operation)
    {
        path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsPreAdmission",owner,operation.ToString("N")+".xml");
        var root=XElement.Load(path);if((string)root.Attribute("state")!="0")throw new Exception("Requires actual saved0 authenticated preparation before discovery.");
        encoded=root.Element("original").Value;
        if(!RebirthLogisticsTransferJournal.TryDecode(encoded,out var original)||original.Legs.Count!=0||original.Npcs.Count!=0||original.BeforeOwner.Any(x=>!x.IsEmpty()))throw new Exception("Preparation contains effects.");
    }
    internal void RequireAdmittingRefusesFatalFreeze(EntityPlayer actualFatalActor)
    {
        var before=XElement.Load(path);if((string)before.Attribute("state")!="1")throw new Exception("Requires actual admission transition first.");
        RebirthLogisticsPreAdmissionReceipt.OnNativeFatal(actualFatalActor);
        if(!XNode.DeepEquals(before,XElement.Load(path)))throw new Exception("Admitting lineage became a zero-effect fatal.");
    }
    internal void RequireActualFrozenOriginal()
    {
        var root=XElement.Load(path);if((string)root.Attribute("state")!="2"||root.Element("original").Value!=encoded||!RebirthLogisticsTransferJournal.TryDecode(encoded,out var expected)||
            !RebirthLogisticsNativeDeathReceipt.TryRead(expected,out _)||!RebirthLogisticsTransferJournal.TryRead(expected.Owner,expected.Operation,out var actual)||RebirthLogisticsTransferJournal.Encode(actual)!=encoded)throw new Exception("Missing positive original fatal/frozen zero-effect chain.");
    }
    internal void RecoverInterruptedFreeze(EntityPlayer actualCurrentOwner)
    {
        var root=XElement.Load(path);if((string)root.Attribute("state")=="1"||!RebirthLogisticsTransferJournal.TryDecode(encoded,out var original)||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out _))throw new Exception("Requires real saved fatal and never-admitted lineage.");
        RebirthLogisticsPreAdmissionReceipt.Recover(actualCurrentOwner,original.Operation);RequireActualFrozenOriginal();
    }
}
