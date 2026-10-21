using System;
using System.IO;
class Program
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}catch(InvalidDataException){Check(true,name);return;}throw new Exception(name);}
    static void Main()
    {
        var id=Guid.NewGuid();var storage=new RebirthCamperStorageInstallations(id);var stations=new RebirthCamperStationInstallations(id);
        var stack=new ItemStack{Count=2};
        var old=RebirthCamperTransferSnapshot.Capture(storage,stations,new[]{new ItemStack()},stack);
        var cursor=RebirthCamperTransferSnapshot.Restore(id,old);
        Check(cursor.BackpackSlot==-1,"existing cursor receipts retain cursor ownership");
        Check(Convert.ToBase64String(cursor.Capture())==Convert.ToBase64String(old),"existing receipt round trip unchanged");
        var cell=RebirthCamperTransferSnapshot.Capture(storage,stations,new[]{new ItemStack()},stack,33);
        var restored=RebirthCamperTransferSnapshot.Restore(id,cell);
        Check(restored.BackpackSlot==33&&restored.Cursor.Count==2,"quick receipt restores exact owner cell and stack");
        Check(Convert.ToBase64String(restored.Capture())==Convert.ToBase64String(cell),"quick receipt round trip preserves owner");
        Reject(()=>RebirthCamperTransferSnapshot.Restore(Guid.NewGuid(),cell),"cross-vehicle receipt refused");
        var truncated=cell[..^1];Reject(()=>RebirthCamperTransferSnapshot.Restore(id,truncated),"truncated owner trailer refused");
        var extra=new byte[cell.Length+1];Array.Copy(cell,extra,cell.Length);Reject(()=>RebirthCamperTransferSnapshot.Restore(id,extra),"unexpected receipt trailing data refused");
        Reject(()=>RebirthCamperTransferSnapshot.Capture(storage,stations,new[]{new ItemStack()},stack,-2),"invalid owner kind refused");
        var offered=RebirthCamperQuickTransferFrame.Capture(16,stack);var native=RebirthCamperQuickTransferFrame.Read(offered,out int slot);
        Check(slot==16&&RebirthCamperInventorySnapshot.Restore(native)[0].Count==2,"remote offer retains physical backpack slot");
        Reject(()=>RebirthCamperQuickTransferFrame.Read(new byte[4],out _),"missing remote stack refused");
        Reject(()=>RebirthCamperQuickTransferFrame.Capture(-1,stack),"negative remote cell refused");
        var bad=(byte[])offered.Clone();Array.Copy(BitConverter.GetBytes(-1),bad,4);Reject(()=>RebirthCamperQuickTransferFrame.Read(bad,out _),"malformed remote cell refused");
        Check(RebirthCamperRemoteCursorPolicy.Decide(false,0,true,false)==RebirthCamperRemoteCursorPolicy.Action.PublishCursor,"unpublished exact source admitted");
        Check(RebirthCamperRemoteCursorPolicy.Decide(false,0,false,false)==RebirthCamperRemoteCursorPolicy.Action.Cancel,"changed source cancels before debit");
        Check(RebirthCamperRemoteCursorPolicy.Decide(false,2,false,true)==RebirthCamperRemoteCursorPolicy.Action.PublishReceipt,"interrupted exact result publishes receipt without another debit");
        Check(RebirthCamperRemoteCursorPolicy.Decide(false,2,false,false)==RebirthCamperRemoteCursorPolicy.Action.Retain,"unknown interrupted owner state retained");
        Check(RebirthCamperRemoteCursorPolicy.Decide(false,1,false,false)==RebirthCamperRemoteCursorPolicy.Action.Resume,"published owner resumes without replay");
        Check(RebirthCamperRemoteCursorPolicy.Decide(true,0,false,false)==RebirthCamperRemoteCursorPolicy.Action.Resume,"server acknowledgement resumes vehicle publication only");
        Console.WriteLine(checks+" checks passed. Production framing and receipt policy; native inventory/model serialization is doubled. No actual network session.");
    }
}
public class ItemStack{public int Count;}
public static class RebirthCamperInventorySnapshot
{
    public static byte[] Capture(ItemStack[] items){using var s=new MemoryStream();using var w=new BinaryWriter(s);w.Write(items.Length);foreach(var item in items)w.Write(item.Count);return s.ToArray();}
    public static ItemStack[] Restore(byte[] bytes){using var s=new MemoryStream(bytes);using var r=new BinaryReader(s);var a=new ItemStack[r.ReadInt32()];for(int i=0;i<a.Length;i++)a[i]=new ItemStack{Count=r.ReadInt32()};return a;}
}
public class RebirthCamperStorageInstallations
{
    public Guid VehicleIdentity;public int Capacity=1;public RebirthCamperStorageInstallations(Guid id){VehicleIdentity=id;}
    public byte[] Capture()=>VehicleIdentity.ToByteArray();public static RebirthCamperStorageInstallations Restore(Guid id,byte[] bytes)=>new RebirthCamperStorageInstallations(id);
}
public class RebirthCamperStationInstallations
{
    public Guid VehicleIdentity;public RebirthCamperStationInstallations(Guid id){VehicleIdentity=id;}
    public byte[] Capture()=>VehicleIdentity.ToByteArray();public static RebirthCamperStationInstallations Restore(Guid id,byte[] bytes)=>new RebirthCamperStationInstallations(id);
}
