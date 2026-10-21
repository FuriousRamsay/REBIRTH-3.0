using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private const string ActionDogReceiptName="rebirthActionPaymentInventoryReceipt";
    internal static bool OwnsActionLeg(ActionPayment payment,ActionLeg leg)
    {lock(actionOwnerGate)return payment!=null&&leg!=null&&actionPayments.Contains(payment)&&payment.Legs.Contains(leg)&&ActionLegCurrent(payment,leg);}
    internal static bool ActionSameNpc(RebirthNpcInventorySnapshot left,RebirthNpcInventorySnapshot right)
    {
        return left!=null&&right!=null&&left.NpcId==right.NpcId&&left.Revision==right.Revision&&left.Quantities.Count==right.Quantities.Count&&left.Reservations.Count==right.Reservations.Count&&
            left.Quantities.All(pair=>right.Quantities.TryGetValue(pair.Key,out int count)&&count==pair.Value)&&left.Reservations.All(pair=>right.Reservations.TryGetValue(pair.Key,out int count)&&count==pair.Value)&&
            XNode.DeepEquals(left.NativeStacks?.Write(),right.NativeStacks?.Write())&&left.LogisticsEffects.Count==right.LogisticsEffects.Count&&left.LogisticsEffects.All(pair=>right.LogisticsEffects.TryGetValue(pair.Key,out var value)&&value==pair.Value);
    }
    internal static RebirthNpcInventorySnapshot ActionExpectedNpc(ActionLeg leg,bool after,bool returned)
    {
        var before=leg.NpcBefore;if(before==null)return null;uint revision=checked(before.Revision+(returned?2u:after?1u:0u));
        var quantities=new Dictionary<string,int>(before.Quantities,StringComparer.OrdinalIgnoreCase);
        if(after&&!returned)foreach(var stack in leg.Paid){string key=stack.itemValue.ItemClass.GetItemName();quantities.TryGetValue(key,out int count);int remaining=checked(count-stack.count);if(remaining<0)return null;if(remaining==0)quantities.Remove(key);else quantities[key]=remaining;}
        return new RebirthNpcInventorySnapshot{NpcId=before.NpcId,Revision=revision,Quantities=quantities,Reservations=new Dictionary<string,int>(before.Reservations,StringComparer.OrdinalIgnoreCase),LogisticsEffects=new Dictionary<Guid,string>(before.LogisticsEffects),NativeStacks=before.NativeStacks?.WithRevision(revision)};
    }
    private static XElement ActionNpcSnapshot(RebirthNpcInventorySnapshot snapshot)=>snapshot==null?null:new XElement("logicalInventory",new XAttribute("stable",snapshot.NpcId),new XAttribute("revision",snapshot.Revision),
        new XElement("quantities",snapshot.Quantities.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("item",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value)))),
        new XElement("reserved",snapshot.Reservations.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("item",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value)))),snapshot.NativeStacks?.Write(),new XElement("originalEffects",snapshot.LogisticsEffects.OrderBy(pair=>pair.Key).Select(pair=>new XElement("effect",new XAttribute("operation",pair.Key),pair.Value))));
    private static string ActionDogReceipt(ActionPayment payment,ActionLeg leg,bool returned)
    {
        var image=new XElement(ActionDogReceiptName,new XAttribute("version",1),new XAttribute("operation",payment.Operation.ToString("N")),new XAttribute("mutation",(returned?leg.ReturnMutation:leg.DebitMutation).ToString("N")),new XAttribute("phase",returned?2:1),new XAttribute("source",leg.Npc.NativeIdentity),new XAttribute("revision",checked((uint)leg.Revision+(returned?2u:1u))),new XAttribute("account",payment.Account),new XAttribute("creation",payment.Creation),new XAttribute("deaths",payment.Deaths),new XAttribute("save",payment.Save),new XAttribute("recovery",payment.Recovery),new XElement("before",ActionFrame(leg.Before)),new XElement("after",ActionFrame(leg.After)));
        using(var count=new ActionCountStream(6L*1024*1024))using(var writer=XmlWriter.Create(count,new XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false,true)})){image.WriteTo(writer);writer.Flush();}
        return image.ToString(SaveOptions.DisableFormatting);
    }
    private static bool ReadActionDogReceipt(RebirthNpcPersistentRecord row,out string receipt,out List<string> unrelated)
    {
        receipt=null;unrelated=new List<string>();
        foreach(string value in row.UnknownOptionalElements??Array.Empty<string>())
        {
            if(string.IsNullOrWhiteSpace(value))throw new InvalidDataException("Incomplete dog optional record");
            using var reader=XmlReader.Create(new StringReader(value),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});reader.MoveToContent();
            if(reader.LocalName!=ActionDogReceiptName){unrelated.Add(value);continue;}
            if(receipt!=null)return false;var image=XElement.Load(reader);if(image.Name!=ActionDogReceiptName||(int?)image.Attribute("version")!=1)return false;receipt=image.ToString(SaveOptions.DisableFormatting);
        }
        return true;
    }
    internal static bool ActionDogReceiptMatches(ActionPayment payment,ActionLeg leg,RebirthNpcPersistentRecord row,bool returned)
    {try{return ReadActionDogReceipt(row,out var receipt,out _)&&receipt==ActionDogReceipt(payment,leg,returned);}catch{return false;}}
    internal static bool StampActionDogCandidate(RebirthNpcPersistentRecord row)
    {
        var payment=actionNpcPayment;var leg=actionNpcLeg;
        if(payment==null||leg?.Npc?.IsDogStorage!=true)return true; // unrelated native caller
        if(!OwnsActionLeg(payment,leg)||row?.Identity?.StableNpcId!=leg.Npc.NativeIdentity||!actionNpcPermitUsed)return false;
        bool returned=leg.State==ActionLegState.ReturnAttempted;if(!returned&&leg.State!=ActionLegState.DebitAttempted)return false;
        int index=payment.Legs.IndexOf(leg);if(!payment.Events.ContainsKey("source-"+index+(returned?"-return-intent":"-debit-intent")))return false;
        if(!ReadActionDogReceipt(row,out var previous,out var unrelated))return false;
        if(returned&&previous!=ActionDogReceipt(payment,leg,false)||!returned&&previous!=null&&!CompletedActionDogReceipt(payment,leg,previous))return false;
        unrelated.Add(ActionDogReceipt(payment,leg,returned));row.UnknownOptionalElements=unrelated.ToArray();return true;
    }
    // An old positively terminal receipt may be retired as metadata when a new
    // independent full original is admitted. Pending/ambiguous receipts never
    // become a new debit permit, even if today's dog slots equal an old image.
    private static bool CompletedActionDogReceipt(ActionPayment payment,ActionLeg leg,string prior)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return false;
        using(scratch)try
        {
            var receipt=XElement.Parse(prior);
            if(!Guid.TryParseExact((string)receipt.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||operation==payment.Operation||
                (string)receipt.Attribute("source")!=leg.Npc.NativeIdentity.ToString()||(string)receipt.Attribute("save")!=payment.Save)return false;
            int phase=(int?)receipt.Attribute("phase")??0;if(phase!=1&&phase!=2)return false;
            string directory=Path.Combine(payment.Save,"RebirthData","ActionPayments",operation.ToString("N"));ActionPath(directory);
            if(!Directory.Exists(directory))return false;
            string terminal=null;int count=0;
            foreach(string path in Directory.EnumerateFiles(directory))
            {if(++count>256+3*32+3+2+1)return false;if(Path.GetFileName(path).EndsWith(phase==1?"-used.xml":"-returned.xml",StringComparison.Ordinal)){if(terminal!=null)return false;terminal=path;}}
            if(terminal==null)return false;
            var info=new FileInfo(terminal);if(info.Length<1||info.Length>24L*1024*1024)return false;
            XElement end;
            using(var input=new MemoryStream(ActionBytes(terminal,checked((int)info.Length)),false))
            using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=24L*1024*1024}))end=XElement.Load(reader);
            if(end.Name!="actionPaymentEvent"||(int?)end.Attribute("version")!=1||(string)end.Attribute("stage")!=(phase==1?"used":"returned"))return false;
            var original=XElement.Parse(end.Element("original")?.Value??"");
            if(original.Name!="resourceActionOriginal"||(int?)original.Attribute("version")!=1||(string)original.Attribute("action")!="electrical-service"||
                (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("save")!=payment.Save)return false;
            foreach(string name in new[]{"account","creation","deaths","recovery"})if((string)original.Attribute(name)!=(string)receipt.Attribute(name))return false;
            var sources=original.Element("sources")?.Elements("source").ToArray();if(sources==null||sources.Length>32)return false;
            int index=Array.FindIndex(sources,value=>(string)value.Attribute("id")==leg.Id);if(index<0||sources.Count(value=>(string)value.Attribute("id")==leg.Id)!=1)return false;
            var source=sources[index];uint revision=unchecked((uint)(int)source.Attribute("revision"));
            if((uint?)receipt.Attribute("revision")!=checked(revision+(phase==1?1u:2u))||
                (string)receipt.Attribute("mutation")!=(string)source.Attribute(phase==1?"debit":"return")||
                receipt.Element("before")?.Value!=source.Element("before")?.Value||receipt.Element("after")?.Value!=source.Element("after")?.Value)return false;
            string saved=Path.Combine(directory,"source-"+index+(phase==1?"-after-saved.native.xml":"-return-saved.native.xml"));
            var savedInfo=new FileInfo(saved);if(!savedInfo.Exists||savedInfo.Length<1||savedInfo.Length>8L*1024*1024)return false;
            using(var input=new MemoryStream(ActionBytes(saved,checked((int)savedInfo.Length)),false))
            using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8L*1024*1024}))
            {var savedBody=XElement.Load(reader);return savedBody.Descendants(ActionDogReceiptName).Any(value=>value.ToString(SaveOptions.DisableFormatting)==prior);}
        }
        catch{return false;}
    }
    private static bool SaveActionNpc(ActionPayment payment,ActionLeg leg,string phase)
    {
        if(leg.SavedMaterial!=null)return FinishActionLegMaterial(payment,leg,phase);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var scratch))return false;
        using(scratch)return SaveActionNpcReserved(payment,leg,phase);
    }
    private static bool SaveActionNpcReserved(ActionPayment payment,ActionLeg leg,string phase)
    {
        if(!OwnsActionLeg(payment,leg)||!RebirthNpcPersistenceCoordinator.SaveAll(out _))return false;
        bool after=phase!="before",returned=phase=="return";
        string full=leg.Npc.IsDogStorage?RebirthNpcAggregatePersistenceStore.CaptureActionPaymentDogSaved(payment,leg,after,returned):RebirthNpcInventoryPersistenceStore.CaptureActionPaymentSaved(payment,leg,after,returned);
        if(full==null)return false;int index=payment.Legs.IndexOf(leg);string stage="source-"+index+"-"+phase+"-saved";
        byte[] bytes=new System.Text.UTF8Encoding(false,true).GetBytes(full);if(bytes.Length>8*1024*1024)throw new InvalidDataException("Saved NPC action image exceeds bound");string path=Path.Combine(payment.Directory,stage+".native.xml");
        if(File.Exists(path)){if(!ActionBytes(path,bytes.Length).SequenceEqual(bytes))return false;}
        else{using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(!ActionBytes(path,bytes.Length).SequenceEqual(bytes))return false;}
        var material=RememberActionMaterial(phase,path,8*1024*1024);if(!material.Bytes.SequenceEqual(bytes)){material.Dispose();throw new IOException("Saved native NPC material changed during retention");}leg.SavedMaterial=material;return FinishActionLegMaterial(payment,leg,phase);
    }
}
