using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static partial class RemoteResourceTransactions
{
    // Discovery only. The existing immutable original, native source/target
    // loads and causal history still qualify every continuation. Completed
    // forensic directories never consume the bounded pending lookup.
    private static string ActionPendingTargetPath(string save,Guid operation)
        =>Path.Combine(save,"RebirthData","ActionPaymentPending",operation.ToString("N")+".original");
    private static void RememberActionPendingTarget(ActionPayment payment)
    {
        string path=ActionPendingTargetPath(payment.Save,payment.Operation);ActionPath(path);
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(payment.Original);
        if(bytes.Length<1||bytes.Length>8*1024*1024)throw new InvalidDataException("Pending electrical original bound exceeded");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if(!File.Exists(path))using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
        if(!ActionEnvelopeFileEquals(path,bytes))throw new IOException("Pending electrical original conflicts");
    }
    private static string[] PendingActionTargetDirectories(string save)
    {
        var operations=new HashSet<Guid>();string root=Path.Combine(save,"RebirthData","ActionPaymentPending");ActionPath(root);
        if(Directory.Exists(root))
        {
            int count=0;
            foreach(string file in Directory.EnumerateFiles(root,"*.original",SearchOption.TopDirectoryOnly))
            {
                if(++count>32)throw new InvalidDataException("Pending electrical discovery bound exceeded");
                ActionPath(file);if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file),"N",out var operation)||operation==Guid.Empty)throw new InvalidDataException();
                long length=new FileInfo(file).Length;if(length<1||length>8*1024*1024)throw new InvalidDataException();
                // A pending description is not a native receipt or a grant.
                operations.Add(operation);
            }
        }
        // Existing originals before this discovery sidecar are selected by
        // actual native loaded owner marker keys, never an historical scan.
        foreach(var owner in SnapshotActionHostOwnerLoads().Where(value=>value.Loaded&&value.Spawn?.Save==save))
            if(RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(owner.Buffs,out var keys))
                foreach(var operation in keys){if(operations.Count>=32&&!operations.Contains(operation))throw new InvalidDataException();operations.Add(operation);}
        return operations.OrderBy(value=>value).Select(value=>Path.Combine(save,"RebirthData","ActionPayments",value.ToString("N"))).ToArray();
    }
    private static bool HasPendingActionOwner(ActionHostSpawn spawn)
        =>PendingActionOwnerOperations(spawn).Length!=0;
    private static Guid[] PendingActionOwnerOperations(ActionHostSpawn spawn)
    {
        var result=new List<Guid>();
        try
        {
            foreach(string directory in PendingActionTargetDirectories(spawn.Save))
            {
                string path=ActionPendingTargetPath(spawn.Save,Guid.ParseExact(Path.GetFileName(directory),"N"));if(!File.Exists(path))continue;
                long length=new FileInfo(path).Length;if(length<1||length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*length+65536),out var memory))return Array.Empty<Guid>();
                using(memory)
                {
                    var original=ReadActionBoundedXml(ActionBytes(path,(int)length),8*1024*1024);
                    if(original.Name=="resourceActionOriginal"&&(string)original.Attribute("account")==spawn.Account&&(string)original.Attribute("world")==spawn.World.worldState.Guid&&
                        (string)original.Attribute("save")==spawn.Save)result.Add(Guid.ParseExact(Path.GetFileName(directory),"N"));
                }
            }
        }
        catch{}
        return result.ToArray();
    }
    private static void RetireActionPendingTarget(string save,Guid operation,string original)
    {
        string path=ActionPendingTargetPath(save,operation);ActionPath(path);
        if(File.Exists(path))
        {
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(original);
            if(!ActionEnvelopeFileEquals(path,bytes))throw new IOException("Pending electrical retirement differs from original");
            File.Delete(path);
        }
        lock(actionTargetLoadGate)
            foreach(var source in actionTargetNativeOriginals.Where(value=>value.Payment==null&&value.Operation==operation&&value.Save==save&&value.Original==original).ToArray())
            {source.Active=false;actionTargetNativeOriginals.Remove(source);ReleaseActionTargetNativeGraph(source);}
    }
}
