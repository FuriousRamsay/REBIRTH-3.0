using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Strict shared codec for two named original producers. This is an effect
// witness in the existing NPC inventory file, not mutation authority or intent.
internal static class RebirthLogisticsNpcEffectReceipt
{
    internal const int MaximumReceipts=128,MaximumText=8192;
    internal sealed class Effect
    {
        internal string Domain,World,Owner,Creation,Npc,Binding,Before,After,NativeBefore,NativeAfter;
        internal Guid Operation,Transaction;internal int Deaths,Phase;internal uint Revision;
    }
    private static bool Hex(string value,int length)=>value?.Length==length&&value.All(Uri.IsHexDigit)&&value==value.ToLowerInvariant();
    private static string Hash(string text)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text??string.Empty))).Replace("-","").ToLowerInvariant();}
    private static string Quantities(IEnumerable<KeyValuePair<string,int>> values)
    {
        var map=new SortedDictionary<string,int>(StringComparer.Ordinal);
        foreach(var value in values)
        {
            if(string.IsNullOrWhiteSpace(value.Key)||value.Key.Length>256||value.Key.Trim()!=value.Key||value.Value<0||map.Count>=4096)throw new InvalidDataException();
            string key=value.Key.ToLowerInvariant();if(map.ContainsKey(key))throw new InvalidDataException();if(value.Value>0)map.Add(key,value.Value);
        }
        return Hash(new XElement("quantities",map.Select(value=>new XElement("item",new XAttribute("key",value.Key),new XAttribute("count",value.Value)))).ToString(SaveOptions.DisableFormatting));
    }
    private static string Native(RebirthNpcNativeStackSet value)=>Hash(value?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty);
    private static XElement Write(Effect value)
    {
        if(value==null||(value.Domain!="logistics"&&value.Domain!="remoteResource")||value.Operation==Guid.Empty||value.Transaction==Guid.Empty||
            !Hex(value.Owner,64)||!Hex(value.Npc,32)||value.Npc.All(item=>item=='0')||value.Deaths<0||value.Phase<1||value.Phase>2||value.Revision==0||
            !RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||
            !Hex(value.Binding,64)||!Hex(value.Before,64)||!Hex(value.After,64)||!Hex(value.NativeBefore,64)||!Hex(value.NativeAfter,64)||
            (value.Domain=="logistics"?(!Guid.TryParseExact(value.World,"N",out var world)||world==Guid.Empty||world.ToString("N")!=value.World):!Hex(value.World,64)))throw new InvalidDataException("Invalid original NPC effect receipt.");
        return new XElement("npcOriginalEffect",new XAttribute("version",1),new XAttribute("domain",value.Domain),new XAttribute("operation",value.Operation.ToString("N")),
            new XAttribute("transaction",value.Transaction.ToString("N")),new XAttribute("world",value.World),new XAttribute("owner",value.Owner),new XAttribute("creation",value.Creation),
            new XAttribute("deaths",value.Deaths),new XAttribute("npc",value.Npc),new XAttribute("phase",value.Phase),new XAttribute("revision",value.Revision),new XAttribute("binding",value.Binding),
            new XAttribute("before",value.Before),new XAttribute("after",value.After),new XAttribute("nativeBefore",value.NativeBefore),new XAttribute("nativeAfter",value.NativeAfter));
    }
    internal static bool TryRead(string text,out Effect effect)
    {
        effect=null;
        try
        {
            if(string.IsNullOrEmpty(text)||text.Length>MaximumText)return false;XElement node;
            using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumText}))node=XElement.Load(reader,LoadOptions.None);
            if(node.Name!="npcOriginalEffect"||node.Attributes().Count()!=16||node.Nodes().Any()||(string)node.Attribute("version")!="1")return false;
            var value=new Effect{Domain=(string)node.Attribute("domain"),Operation=Guid.ParseExact((string)node.Attribute("operation"),"N"),Transaction=Guid.ParseExact((string)node.Attribute("transaction"),"N"),
                World=(string)node.Attribute("world"),Owner=(string)node.Attribute("owner"),Creation=(string)node.Attribute("creation"),Deaths=(int)node.Attribute("deaths"),Npc=(string)node.Attribute("npc"),
                Phase=(int)node.Attribute("phase"),Revision=(uint)node.Attribute("revision"),Binding=(string)node.Attribute("binding"),Before=(string)node.Attribute("before"),After=(string)node.Attribute("after"),
                NativeBefore=(string)node.Attribute("nativeBefore"),NativeAfter=(string)node.Attribute("nativeAfter")};
            if(Write(value).ToString(SaveOptions.DisableFormatting)!=text)return false;effect=value;return true;
        }
        catch{return false;}
    }
    internal static bool Validate(RebirthNpcStableId id,uint revision,IDictionary<Guid,string> receipts)
    {
        if(receipts==null)return true; // Legacy format1 records have no effects.
        if(receipts.Count>MaximumReceipts)return false;var originals=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in receipts)
            if(!TryRead(item.Value,out var effect)||effect.Transaction!=item.Key||effect.Npc!=id.ToString()||effect.Revision>revision||
                !originals.Add(effect.Domain+":"+effect.Operation.ToString("N")+":"+effect.Phase))return false;
        return true;
    }
    private static bool NativeStages(RebirthLogisticsTransferJournal.NpcLeg leg,bool credit,out RebirthNpcNativeStackSet before,out RebirthNpcNativeStackSet after)
    {
        before=null;after=null;
        if(!RebirthNpcStableId.TryParse(leg.StableId,out var id)||leg.Revision>uint.MaxValue-2)return false;
        if(!string.IsNullOrEmpty(leg.NativeBefore)&&!RebirthNpcNativeStackSet.TryRead(XElement.Parse(leg.NativeBefore),out before))return false;
        if(!credit){after=before?.WithRevision(leg.Revision+1);return true;}
        before=before?.WithRevision(leg.Revision+1);var records=new List<RebirthNpcNativeStackRecord>();
        if(before!=null)foreach(var node in before.Write().Elements()){if(!RebirthNpcNativeStackRecord.TryRead(node,out var record))return false;records.Add(record);}
        foreach(var item in leg.NativeCredits){if(!RebirthNpcNativeStackRecord.TryCapture(item.StackId,id,item.Stack,out var record))return false;records.Add(record);}
        return before==null&&records.Count==0||RebirthNpcNativeStackSet.TryCreate(id,leg.Revision+2,records,out after);
    }
    internal static string ExpectedLogistics(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsTransferJournal.NpcLeg leg,bool credit,uint? appliedRevision=null)
    {
        if(!NativeStages(leg,credit,out var nativeBefore,out var nativeAfter))throw new InvalidDataException();
        uint revision=appliedRevision??leg.Revision+(credit?2U:1U);
        if(credit?revision<leg.Revision+2:revision!=leg.Revision+1)throw new InvalidDataException();
        var before=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var after=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in leg.Entries)
        {before.Add(entry.Key,credit?Math.Min(entry.Before,entry.After-entry.PendingNative):entry.Before);after.Add(entry.Key,credit?entry.After:Math.Min(entry.Before,entry.After-entry.PendingNative));}
        return Write(new Effect{Domain="logistics",Operation=original.Operation,Transaction=credit?leg.CreditId:leg.DebitId,World=original.World,Owner=original.Owner,Creation=original.Creation,Deaths=original.Deaths,
            Npc=leg.StableId,Phase=credit?2:1,Revision=revision,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Before=Quantities(before),After=Quantities(after),
            NativeBefore=Native(nativeBefore),NativeAfter=Native(nativeAfter)}).ToString(SaveOptions.DisableFormatting);
    }
    internal static bool MatchesLogistics(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsTransferJournal.NpcLeg leg,bool credit,string receipt)
    {
        try{return TryRead(receipt,out var effect)&&effect.Revision>=leg.Revision+(credit?2U:1U)&&ExpectedLogistics(original,leg,credit,effect.Revision)==receipt;}
        catch{return false;}
    }
    internal static string ExpectedRemoteResource(RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(plan==null||plan.DogStorage||plan.Revision==uint.MaxValue)throw new InvalidDataException();
        RebirthNpcNativeStackSet before=null,after=null;
        if(!string.IsNullOrEmpty(plan.BeforeNative)&&!RebirthNpcNativeStackSet.TryRead(XElement.Parse(plan.BeforeNative),out before)||
            !string.IsNullOrEmpty(plan.AfterNative)&&!RebirthNpcNativeStackSet.TryRead(XElement.Parse(plan.AfterNative),out after))throw new InvalidDataException();
        return Write(new Effect{Domain="remoteResource",Operation=plan.Operation,Transaction=plan.MutationId,World=plan.World,Owner=plan.Owner,Creation=plan.Creation,Deaths=plan.Deaths,Npc=plan.NpcId.ToString(),
            Phase=1,Revision=plan.Revision+1,Binding=plan.Binding,Before=Quantities(plan.BeforeQuantities),After=Quantities(plan.AfterQuantities),NativeBefore=Native(before),NativeAfter=Native(after)}).ToString(SaveOptions.DisableFormatting);
    }
    internal static bool IsSavedTerminalLogistics(string receipt)
    {
        try
        {
            if(!TryRead(receipt,out var effect)||effect.Domain!="logistics"||!RebirthLogisticsTransferJournal.TryRead(effect.Owner,effect.Operation,out var original)||original.Phase!=4)return false;
            var leg=original.Npcs.SingleOrDefault(value=>value.StableId==effect.Npc);
            return leg!=null&&MatchesLogistics(original,leg,effect.Phase==2,receipt);
        }
        catch{return false;}
    }
}
