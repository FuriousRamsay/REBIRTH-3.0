using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

static class Program
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static void Main()
    {
        var id=Guid.NewGuid();string owner="Steam_76561198000000001",key=RebirthStablePlayerIdentity.ComputeStorageKey(owner);
        string creation=Guid.NewGuid().ToString("N"),world=Guid.NewGuid().ToString("N"),save=RebirthStablePlayerIdentity.ComputeStorageKey("fixture-save");
        byte[] wire={13,0,1,2,3,4};
        Check(RebirthConsumptionDurableReceipt.TryCreate(id,owner,key,creation,world,save,7,wire,false,out var pending),"valid scoped pending receipt");
        wire[2]=99;Check(pending.OriginalWire[2]==1,"retained original bytes detached");var copy=pending.OriginalWire;copy[2]=88;
        Check(pending.OriginalWire[2]==1,"returned payload cannot mutate original");
        string xml=pending.Write().ToString();Check(RebirthConsumptionDurableReceipt.TryRead(xml,out var parsed)&&pending.Exact(parsed),"actual receipt codec roundtrip");
        var ack=pending.WithAcknowledgement();Check(ack.Acknowledged&&ack.SameOriginal(pending)&&!ack.Exact(pending),"ACK transition retains exact original");
        Check(!RebirthConsumptionDurableReceipt.TryRead(xml.Replace("version=\"1\"","version=\"2\""),out _),"unknown version refused");
        Check(!RebirthConsumptionDurableReceipt.TryRead(xml.Replace("<wire ","<wire foreign=\"x\" "),out _),"unknown payload attribute refused");
        Check(!RebirthConsumptionDurableReceipt.TryRead(xml.Replace(key,new string('a',64)),out _),"wrong stable storage key refused");
        Check(!RebirthConsumptionDurableReceipt.TryRead(xml.Replace("state=\"pending\"","state=\"refunded\""),out _),"unknown terminal state refused");
        Check(!RebirthConsumptionDurableReceipt.TryRead(xml.Replace("DQABAgME","DQAJAgME"),out _),"tampered wire hash refused");
        Check(!RebirthConsumptionDurableReceipt.TryRead("<!DOCTYPE x [<!ENTITY p 'x'>]>"+xml,out _),"DTD refused");
        Check(!RebirthConsumptionDurableReceipt.TryRead(new string('x',RebirthConsumptionDurableReceipt.MaximumXmlCharacters+1),out _),"oversized XML refused");
        Check(!RebirthConsumptionDurableReceipt.TryCreate(id,owner,key,creation,world,save,7,new byte[RebirthConsumptionDurableReceipt.MaximumWireBytes+1],false,out _),"oversized packet refused");
        string directory=Path.Combine(Path.GetTempPath(),"RebirthConsumptionReceiptFixture_"+Guid.NewGuid().ToString("N"));
        string path=Path.Combine(directory,id.ToString("N")+".xml");
        // Explicit caller packet validator DOUBLE: accepts only fixture original bytes.
        Func<RebirthConsumptionDurableReceipt,bool> validate=r=>r.OriginalWire.SequenceEqual(new byte[]{13,0,1,2,3,4});
        Check(!RebirthConsumptionDurableReceiptStore.TryPersist(directory,pending,()=>false,validate,out _)&&!File.Exists(path),"stale authority performs no publication");
        Check(!RebirthConsumptionDurableReceiptStore.TryPersist(directory,ack,()=>true,validate,out _)&&!File.Exists(path),"genesis terminal receipt refused");
        Check(RebirthConsumptionDurableReceiptStore.TryPersist(directory,pending,()=>true,validate,out var error),"new pending published and final verified "+error);
        Check(RebirthConsumptionDurableReceiptStore.TryReadFinal(path,out parsed)&&pending.Exact(parsed),"actual final readback");
        Check(RebirthConsumptionDurableReceiptStore.TryPersist(directory,pending,()=>true,validate,out _),"exact repeat idempotent");
        Check(!RebirthConsumptionDurableReceiptStore.TryPersist(directory,pending,()=>true,r=>false,out _),"invalid original packet witness refuses update");
        Check(RebirthConsumptionDurableReceiptStore.TryPersist(directory,ack,()=>true,validate,out error),"pending to ACK atomic replacement "+error);
        Check(!RebirthConsumptionDurableReceiptStore.TryPersist(directory,pending,()=>true,validate,out _),"ACK cannot regress to pending");
        Check(RebirthConsumptionDurableReceiptStore.TryReadFinal(path,out parsed)&&ack.Exact(parsed),"terminal retained after refused regression");
        Console.WriteLine("PASS "+checks+" source-only fixture prepared for explicit parent run. Identity normalization/packet validator doubles; actual codec/store/durable primitive. No game/native packet/auth/save qualification. Files retained for inspection: "+directory);
    }
}
static class RebirthStablePlayerIdentity
{public static string ComputeStorageKey(string owner){using var hash=SHA256.Create();return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(owner)).Select(b=>b.ToString("x2")));}}
static class RebirthSurvivorRequestScope
{public static bool TryNormalize(string value,out string normalized){normalized=null;if(!Guid.TryParse(value,out var id)||id==Guid.Empty)return false;normalized=id.ToString("N");return true;}}
