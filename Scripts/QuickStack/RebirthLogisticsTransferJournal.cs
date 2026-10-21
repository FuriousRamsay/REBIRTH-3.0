using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;


// Original logistics intent/evidence only. These images never replace native
// container storage, and this journal does not execute any inventory mutation.
internal static class RebirthLogisticsTransferJournal
{
    internal const int MaximumBytes=4194304,MaximumLegs=128;
    internal sealed class Leg
    {
        internal string Id,NativeIdentity;internal RemoteResourceSourceKind Kind;
        internal ItemStack[] Before,After;internal bool[] Locks;internal int Published,Attempted;internal bool Restored;
    }
    internal sealed class NpcEntry{internal string Key;internal int Before,Reserved,Custodied,PendingNative,After;internal bool Locked;}
    internal sealed class NpcCredit{internal Guid StackId;internal string Key,Source;internal int Slot;internal ItemStack Stack;}
    internal sealed class NpcLeg
    {
        internal string StableId,NativeBefore;internal Guid DebitId,CreditId;internal uint Revision;internal int Published,Attempted;
        internal readonly List<NpcEntry> Entries=new List<NpcEntry>();
        internal readonly List<NpcCredit> NativeCredits=new List<NpcCredit>();
    }
    internal sealed class Record
    {
        internal Guid Operation;internal string World,Owner,Creation,Target,Selection,OwnerDebitProof,OwnerNoEffectProof;
        internal ulong Epoch;internal int Request,Deaths,Phase,Outcome;internal bool Rejecting,OwnerEscrowRecovery;internal QuickStackRadialAction Action;
        internal ItemStack[] BeforeOwner,DebitedOwner,AfterOwner,OriginalOwnerBag,OriginalOwnerBelt;
        internal readonly List<Leg> Legs=new List<Leg>();
        internal readonly List<NpcLeg> Npcs=new List<NpcLeg>();
    }
    private static bool writing;
    private static object transitionMutation=new object();
    internal static object TransitionMutation=>transitionMutation;
    internal static bool TransitionQuiescent=>!writing;
    internal static string CurrentTransitionRoot(World world)
    {if(writing||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))throw new InvalidOperationException();return Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthLogisticsTransfers");}
    internal static string[] CurrentTransitionHeads(World world,string owner,Guid operation)
    {
        CurrentTransitionRoot(world);if(!TryRead(owner,operation,out var original))throw new InvalidDataException();
        string path=PathFor(GameIO.GetSaveGameDir(),owner,operation),archive=ArchiveFor(path);
        foreach(string primary in new[]{path,archive})foreach(string suffix in new[]{".bak",".tmp"})
        {
            string candidate=primary+suffix;if(!File.Exists(candidate))continue;
            var side=Read(ReadFile(candidate));if(!(suffix==".bak"?CanFollow(side,original):CanFollow(original,side)))throw new InvalidDataException();
        }
        return new[]{path,path+".bak",path+".tmp",archive,archive+".bak",archive+".tmp"};
    }
    internal static string WorldKey(World world)
    {
        string text=world?.IsRemote()==true?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world?.Guid;
        return Guid.TryParse(text,out var id)&&id!=Guid.Empty?id.ToString("N"):null;
    }
    private static bool Hex(string text,int length)=>text?.Length==length&&text.All(Uri.IsHexDigit);
    private static bool DogIdentity(Leg leg)
    {
        if(leg.Kind!=RemoteResourceSourceKind.NpcStorage)return true;
        if(leg.Id?.StartsWith("N:",StringComparison.Ordinal)!=true)return false;
        string id=leg.Id.Substring(2),prefix="dog:"+id+":";
        return Hex(id,32)&&id==id.ToLowerInvariant()&&!id.All(value=>value=='0')&&leg.NativeIdentity?.StartsWith(prefix,StringComparison.Ordinal)==true&&
            uint.TryParse(leg.NativeIdentity.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out uint revision)&&revision<=uint.MaxValue-2&&
            leg.NativeIdentity==prefix+revision.ToString(CultureInfo.InvariantCulture);
    }
    private static bool NativeImage(string text,out RebirthNpcNativeStackSet native)
    {
        native=null;if(string.IsNullOrEmpty(text)||text.Length>2097152)return false;
        using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2097152}))
            return RebirthNpcNativeStackSet.TryRead(XElement.Load(reader,LoadOptions.None),out native);
    }
    private static string Image(ItemStack[] items)
    {
        if(items==null||items.Length<1||items.Length>256)throw new InvalidDataException("Invalid logistics native slot dimension.");
        foreach(var item in items)if(item==null||item.count<0||item.count>ushort.MaxValue||item.count>0&&(item.itemValue==null||item.itemValue.IsEmpty()||ItemClass.GetForId(item.itemValue.type)==null))throw new InvalidDataException("Invalid original native logistics stack.");
        return Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    }
    private static void Conserved(Record record)
    {
        var totals=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        void Add(string key,long quantity)
        {if(string.IsNullOrWhiteSpace(key))throw new InvalidDataException();totals.TryGetValue(key,out long value);totals[key]=checked(value+quantity);}
        void Native(ItemStack[] items,int sign)
        {foreach(var item in items)if(item!=null&&!item.IsEmpty())Add(ItemClass.GetForId(item.itemValue.type)?.Name,(long)sign*item.count);}
        Native(record.BeforeOwner,-1);Native(record.AfterOwner,1);
        foreach(var leg in record.Legs){Native(leg.Before,-1);Native(leg.After,1);}
        foreach(var npc in record.Npcs)foreach(var entry in npc.Entries)Add(entry.Key,(long)entry.After-entry.Before);
        if(totals.Values.Any(value=>value!=0))throw new InvalidDataException("Original logistics plan does not conserve native/semantic item quantities.");
    }
    private static ItemStack[] Slots(string image)
    {
        if(string.IsNullOrEmpty(image)||image.Length>MaximumBytes)throw new InvalidDataException();
        var slots=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image));
        if(Image(slots)!=image)throw new InvalidDataException("Noncanonical logistics native image.");return slots;
    }
    private static XElement Write(Record record)
    {
        if(record==null||record.Operation==Guid.Empty||!Guid.TryParseExact(record.World,"N",out var world)||world==Guid.Empty||
            !Hex(record.Owner,64)||!RebirthOriginalActionScope.Validate(record.Owner,record.Creation)||
            record.Epoch==0||record.Request<=0||record.Deaths<0||record.Phase<0||record.Phase>4||record.Outcome<0||record.Outcome>3||
            record.OwnerEscrowRecovery&&(record.Outcome!=3||record.Phase<2)||record.Phase<2&&record.Outcome!=0||record.Phase>=2&&record.Outcome==0||record.Rejecting&&(record.Phase<1||record.Outcome==1||record.Legs.Any(leg=>leg.Attempted>1||leg.Kind==RemoteResourceSourceKind.NpcStorage&&leg.Attempted!=0)||record.Npcs.Any(leg=>leg.Attempted!=0))||
            !string.IsNullOrEmpty(record.OwnerDebitProof)&&(!Hex(record.OwnerDebitProof,64)||record.Phase<1)||record.Phase>=2&&record.Outcome!=3&&string.IsNullOrEmpty(record.OwnerDebitProof)||
            !string.IsNullOrEmpty(record.OwnerNoEffectProof)&&(!Hex(record.OwnerNoEffectProof,64)||record.Outcome!=3)||
            record.Outcome==3&&(!record.Rejecting||record.Phase<2||string.IsNullOrEmpty(record.OwnerNoEffectProof)||!string.IsNullOrEmpty(record.OwnerDebitProof)||record.Legs.Any(leg=>leg.Attempted!=0||leg.Published!=0||leg.Restored)||record.Npcs.Any(leg=>leg.Attempted!=0||leg.Published!=0))||
            (byte)record.Action>(byte)QuickStackRadialAction.PullDroneCredits||record.Legs.Count+record.Npcs.Count>MaximumLegs||
            record.BeforeOwner?.Length!=record.AfterOwner?.Length||record.BeforeOwner?.Length!=record.DebitedOwner?.Length||record.BeforeOwner?.Length!=record.OriginalOwnerBag?.Length||record.Target?.Length>512||record.Selection?.Length>8192)throw new InvalidDataException("Invalid original logistics intent.");
        var root=new XElement("logisticsTransfer",new XAttribute("version",2),new XAttribute("operation",record.Operation.ToString("N")),
            new XAttribute("world",record.World),new XAttribute("owner",record.Owner),new XAttribute("creation",record.Creation),
            new XAttribute("epoch",record.Epoch),new XAttribute("request",record.Request),new XAttribute("deaths",record.Deaths),
            new XAttribute("action",(byte)record.Action),new XAttribute("phase",record.Phase),new XAttribute("outcome",record.Outcome),new XAttribute("rejecting",record.Rejecting?1:0),new XAttribute("ownerDebitProof",record.OwnerDebitProof??string.Empty),new XAttribute("ownerNoEffectProof",record.OwnerNoEffectProof??string.Empty),
            new XElement("target",Convert.ToBase64String(Encoding.UTF8.GetBytes(record.Target??string.Empty))),new XElement("selection",Convert.ToBase64String(Encoding.UTF8.GetBytes(record.Selection??string.Empty))),
            new XElement("ownerBefore",Image(record.BeforeOwner)),new XElement("ownerDebited",Image(record.DebitedOwner)),new XElement("ownerAfter",Image(record.AfterOwner)),
            new XElement("originalOwnerBag",Image(record.OriginalOwnerBag)),new XElement("originalOwnerBelt",Image(record.OriginalOwnerBelt)));
        if(record.OwnerEscrowRecovery)root.Add(new XAttribute("ownerEscrowRecovery",1));
        for(int i=0;i<record.BeforeOwner.Length;i++)
        {
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(Debited(record.BeforeOwner[i],record.AfterOwner[i]),record.DebitedOwner[i]))throw new InvalidDataException("Original owner debit boundary differs.");
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(record.BeforeOwner[i],record.AfterOwner[i])&&!RebirthStationGridIngredients.IsSameStackSnapshot(record.OriginalOwnerBag[i],record.BeforeOwner[i]))throw new InvalidDataException("Original full native source differs from sparse intent.");
        }
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var leg in record.Legs)
        {
            if(leg==null||string.IsNullOrEmpty(leg.Id)||leg.Id.Length>512||!identities.Add(leg.Id)||
                string.IsNullOrEmpty(leg.NativeIdentity)||leg.NativeIdentity.Length>1024||!Enum.IsDefined(typeof(RemoteResourceSourceKind),leg.Kind)||
                leg.Published<0||leg.Published>2||leg.Attempted<leg.Published||leg.Attempted>2||leg.Attempted==2&&leg.Published<1||leg.Before?.Length!=leg.After?.Length||leg.Locks?.Length!=leg.Before?.Length||!DogIdentity(leg)||leg.Attempted>0&&string.IsNullOrEmpty(record.OwnerDebitProof)||
                record.Outcome==1&&(leg.Published!=2||leg.Restored)||record.Outcome==2&&(leg.Attempted>0&&!leg.Restored||leg.Published>1)||
                leg.Restored&&(record.Legs.Any(value=>value.Attempted>1||value.Kind==RemoteResourceSourceKind.NpcStorage&&value.Attempted>0)||string.IsNullOrEmpty(record.OwnerDebitProof)||record.Npcs.Any(npc=>npc.Attempted!=0)))throw new InvalidDataException("Invalid original logistics endpoint.");
            for(int slot=0;slot<leg.Locks.Length;slot++)if(leg.Locks[slot]&&
                (!RebirthStationGridIngredients.IsSameStackSnapshot(Debited(leg.Before[slot],leg.After[slot]),leg.Before[slot])||leg.Before[slot].IsEmpty()&&!leg.After[slot].IsEmpty()))throw new InvalidDataException("Original protected endpoint slot differs.");
            root.Add(new XElement("leg",new XAttribute("id",leg.Id),new XAttribute("identity",leg.NativeIdentity),new XAttribute("kind",(byte)leg.Kind),
                new XAttribute("published",leg.Published),new XAttribute("attempted",leg.Attempted),new XAttribute("restored",leg.Restored?1:0),new XElement("before",Image(leg.Before)),new XElement("after",Image(leg.After)),new XElement("locks",Convert.ToBase64String(leg.Locks.Select(value=>(byte)(value?1:0)).ToArray()))));
        }
        var nativeCreditIds=new HashSet<Guid>();var nativeCreditSources=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var npc in record.Npcs)
        {
            if(npc==null||npc.Revision>uint.MaxValue-2||npc.DebitId==Guid.Empty||npc.CreditId==Guid.Empty||npc.DebitId==npc.CreditId||!Hex(npc.StableId,32)||npc.StableId!=npc.StableId.ToLowerInvariant()||npc.StableId.All(c=>c=='0')||!identities.Add("N:"+npc.StableId)||
                npc.Published<0||npc.Published>2||npc.Attempted<npc.Published||npc.Attempted>2||npc.Attempted==2&&npc.Published<1||npc.Entries.Count>4096||npc.NativeCredits.Count>4096||npc.Attempted>0&&string.IsNullOrEmpty(record.OwnerDebitProof)||record.Outcome==1&&npc.Published!=2||record.Outcome==2&&npc.Attempted!=0)throw new InvalidDataException("Invalid original logical companion.");
            RebirthNpcNativeStackSet native=null;
            if(!string.IsNullOrEmpty(npc.NativeBefore))
            {
                if(!NativeImage(npc.NativeBefore,out native)||native.Owner!=npc.StableId||native.Revision!=npc.Revision||
                    native.Write().ToString(SaveOptions.DisableFormatting)!=npc.NativeBefore)throw new InvalidDataException("Original retained native companion custody differs.");
            }
            var node=new XElement("npc",new XAttribute("id",npc.StableId),new XAttribute("revision",npc.Revision),new XAttribute("published",npc.Published),new XAttribute("attempted",npc.Attempted),new XAttribute("debit",npc.DebitId.ToString("N")),new XAttribute("credit",npc.CreditId.ToString("N")),new XElement("nativeBefore",npc.NativeBefore??string.Empty));
            var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string previous=null;
            foreach(var entry in npc.Entries)
            {
                if(entry==null||string.IsNullOrWhiteSpace(entry.Key)||entry.Key.Length>128||entry.Key.Trim()!=entry.Key||!keys.Add(entry.Key)||
                    previous!=null&&string.Compare(previous,entry.Key,StringComparison.OrdinalIgnoreCase)>=0||entry.Before<0||entry.Reserved<0||entry.Custodied<0||entry.PendingNative<0||(long)entry.Reserved+entry.Custodied>entry.Before||entry.After<(long)entry.Reserved+entry.Custodied+entry.PendingNative||entry.Locked&&entry.After<entry.Before)
                    throw new InvalidDataException("Invalid original companion quantities/reservations.");
                if((native?.GetQuantity(entry.Key)??0)!=entry.Custodied)throw new InvalidDataException("Original retained native companion quantity differs.");
                previous=entry.Key;node.Add(new XElement("item",new XAttribute("key",entry.Key),new XAttribute("before",entry.Before),new XAttribute("reserved",entry.Reserved),new XAttribute("custodied",entry.Custodied),new XAttribute("pendingNative",entry.PendingNative),new XAttribute("after",entry.After),new XAttribute("locked",entry.Locked)));
            }
            if(native!=null&&!native.FitsQuantities(npc.Entries.ToDictionary(entry=>entry.Key,entry=>entry.Before,StringComparer.OrdinalIgnoreCase)))throw new InvalidDataException("Original native companion custody lacks semantic quantities.");
            var creditCounts=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
            foreach(var credit in npc.NativeCredits)
            {
                if(credit==null||credit.StackId==Guid.Empty||!nativeCreditIds.Add(credit.StackId)||!keys.Contains(credit.Key)||string.IsNullOrEmpty(credit.Source)||credit.Source.Length>512||credit.Source.IndexOf('\0')>=0||credit.Slot<0||credit.Slot>255||credit.Stack==null||credit.Stack.IsEmpty())throw new InvalidDataException("Invalid retained native companion credit.");
                if(!string.Equals(ItemClass.GetForId(credit.Stack.itemValue.type)?.Name,credit.Key,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Retained native companion credit item key differs.");
                var originalSource=credit.Source=="$owner"?record.OriginalOwnerBag:record.Legs.FirstOrDefault(leg=>leg.Id==credit.Source)?.Before;
                if(originalSource==null||credit.Slot>=originalSource.Length||originalSource[credit.Slot]==null||originalSource[credit.Slot].count<credit.Stack.count)throw new InvalidDataException("Native companion credit source differs.");
                var before=originalSource[credit.Slot].Clone();var after=credit.Stack.Clone();before.count=1;after.count=1;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(before,after))throw new InvalidDataException("Native companion credit metadata differs.");
                string sourceKey=credit.Source+"\0"+credit.Slot.ToString(CultureInfo.InvariantCulture);nativeCreditSources.TryGetValue(sourceKey,out long sourceCount);sourceCount=checked(sourceCount+credit.Stack.count);
                if(sourceCount>originalSource[credit.Slot].count)throw new InvalidDataException("Native companion credit exceeds original source budget.");nativeCreditSources[sourceKey]=sourceCount;
                creditCounts.TryGetValue(credit.Key,out long count);creditCounts[credit.Key]=checked(count+credit.Stack.count);
                node.Add(new XElement("nativeCredit",new XAttribute("stack",credit.StackId.ToString("N")),new XAttribute("key",credit.Key),new XAttribute("source",credit.Source),new XAttribute("slot",credit.Slot),Image(new[]{credit.Stack})));
            }
            foreach(var entry in npc.Entries){creditCounts.TryGetValue(entry.Key,out long count);if(count!=entry.PendingNative)throw new InvalidDataException("Retained native companion credit quantity differs.");}
            root.Add(node);
        }
        Conserved(record);
        if(Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting))>MaximumBytes)throw new InvalidDataException("Original logistics intent exceeds budget.");return root;
    }
    private static Record Read(XElement root)
    {
        if(root?.Name!="logisticsTransfer"||root.Attributes().Count()!=(root.Attribute("ownerEscrowRecovery")==null?14:15)||((string)root.Attribute("version")!="1"&&(string)root.Attribute("version")!="2")||
            root.Elements().Count()!=7+root.Elements("leg").Count()+root.Elements("npc").Count()||root.Elements("target").Count()!=1||root.Elements("selection").Count()!=1||
            root.Elements("ownerBefore").Count()!=1||root.Elements("ownerDebited").Count()!=1||root.Elements("ownerAfter").Count()!=1||root.Elements("originalOwnerBag").Count()!=1||root.Elements("originalOwnerBelt").Count()!=1)throw new InvalidDataException();
        var record=new Record{Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),World=(string)root.Attribute("world"),Owner=(string)root.Attribute("owner"),
            Creation=(string)root.Attribute("creation"),Epoch=ulong.Parse((string)root.Attribute("epoch"),CultureInfo.InvariantCulture),Request=(int)root.Attribute("request"),
            OwnerEscrowRecovery=root.Attribute("ownerEscrowRecovery")!=null&&(int)root.Attribute("ownerEscrowRecovery")==1,Deaths=(int)root.Attribute("deaths"),Action=(QuickStackRadialAction)checked((byte)(int)root.Attribute("action")),Phase=(int)root.Attribute("phase"),Outcome=(int)root.Attribute("outcome"),Rejecting=(int)root.Attribute("rejecting")==1,OwnerDebitProof=(string)root.Attribute("ownerDebitProof"),OwnerNoEffectProof=(string)root.Attribute("ownerNoEffectProof"),
            Target=(string)root.Attribute("version")=="1"?(string)root.Element("target"):new UTF8Encoding(false,true).GetString(Convert.FromBase64String((string)root.Element("target"))),Selection=(string)root.Attribute("version")=="1"?(string)root.Element("selection"):new UTF8Encoding(false,true).GetString(Convert.FromBase64String((string)root.Element("selection"))),BeforeOwner=Slots((string)root.Element("ownerBefore")),DebitedOwner=Slots((string)root.Element("ownerDebited")),AfterOwner=Slots((string)root.Element("ownerAfter")),
            OriginalOwnerBag=Slots((string)root.Element("originalOwnerBag")),OriginalOwnerBelt=Slots((string)root.Element("originalOwnerBelt"))};
        foreach(var node in root.Elements("leg"))
        {
            if(node.Attributes().Count()!=6||node.Elements().Count()!=3||node.Elements("before").Count()!=1||node.Elements("after").Count()!=1||node.Elements("locks").Count()!=1)throw new InvalidDataException();
            int restored=(int)node.Attribute("restored");if(restored<0||restored>1)throw new InvalidDataException();
            var locks=Convert.FromBase64String((string)node.Element("locks"));if(locks.Length<1||locks.Length>256||locks.Any(value=>value>1))throw new InvalidDataException();
            record.Legs.Add(new Leg{Id=(string)node.Attribute("id"),NativeIdentity=(string)node.Attribute("identity"),Kind=(RemoteResourceSourceKind)checked((byte)(int)node.Attribute("kind")),
                Published=(int)node.Attribute("published"),Attempted=(int)node.Attribute("attempted"),Restored=restored==1,Before=Slots((string)node.Element("before")),After=Slots((string)node.Element("after")),Locks=locks.Select(value=>value==1).ToArray()});
        }
        foreach(var node in root.Elements("npc"))
        {
            if(node.Attributes().Count()!=6||node.Elements("nativeBefore").Count()!=1||node.Elements().Count()!=1+node.Elements("item").Count()+node.Elements("nativeCredit").Count())throw new InvalidDataException();
            var npc=new NpcLeg{StableId=(string)node.Attribute("id"),Revision=(uint)node.Attribute("revision"),Published=(int)node.Attribute("published"),Attempted=(int)node.Attribute("attempted"),DebitId=Guid.ParseExact((string)node.Attribute("debit"),"N"),CreditId=Guid.ParseExact((string)node.Attribute("credit"),"N"),NativeBefore=(string)node.Element("nativeBefore")};
            foreach(var entry in node.Elements("item"))
            {
                if(entry.Attributes().Count()!=7||entry.HasElements||!string.IsNullOrEmpty(entry.Value))throw new InvalidDataException();
                npc.Entries.Add(new NpcEntry{Key=(string)entry.Attribute("key"),Before=(int)entry.Attribute("before"),Reserved=(int)entry.Attribute("reserved"),Custodied=(int)entry.Attribute("custodied"),PendingNative=(int)entry.Attribute("pendingNative"),After=(int)entry.Attribute("after"),Locked=(bool)entry.Attribute("locked")});
            }
            foreach(var credit in node.Elements("nativeCredit"))
            {
                if(credit.Attributes().Count()!=4||credit.HasElements)throw new InvalidDataException();var image=Slots(credit.Value);if(image.Length!=1)throw new InvalidDataException();
                npc.NativeCredits.Add(new NpcCredit{StackId=Guid.ParseExact((string)credit.Attribute("stack"),"N"),Key=(string)credit.Attribute("key"),Source=(string)credit.Attribute("source"),Slot=(int)credit.Attribute("slot"),Stack=image[0]});
            }
            record.Npcs.Add(npc);
        }
        if((string)root.Attribute("version")=="1")
        {
            root=new XElement(root);root.SetAttributeValue("version",2);
            root.Element("target").Value=Convert.ToBase64String(Encoding.UTF8.GetBytes(record.Target??string.Empty));
            root.Element("selection").Value=Convert.ToBase64String(Encoding.UTF8.GetBytes(record.Selection??string.Empty));
        }
        if(!XNode.DeepEquals(root,Write(record)))throw new InvalidDataException("Original logistics schema/image differs.");return record;
    }
    private static string PathFor(string directory,string owner,Guid operation)
    {
        if(string.IsNullOrEmpty(directory)||!Hex(owner,64)||operation==Guid.Empty)throw new InvalidDataException();
        return Path.Combine(directory,"RebirthLogisticsTransfers",owner.ToLowerInvariant(),operation.ToString("N")+".xml");
    }
    internal static string Encode(Record record)=>Write(record).ToString(SaveOptions.DisableFormatting);
    internal static ItemStack Debited(ItemStack before,ItemStack after)
    {
        if(before==null||before.IsEmpty())return ItemStack.Empty.Clone();
        if(after==null||after.IsEmpty())return ItemStack.Empty.Clone();
        var a=before.Clone();var b=after.Clone();a.count=1;b.count=1;
        if(!RebirthStationGridIngredients.IsSameStackSnapshot(a,b))return ItemStack.Empty.Clone();
        var result=before.Clone();result.count=Math.Min(before.count,after.count);return result;
    }
    internal static bool TryDecode(string text,out Record record)
    {
        record=null;
        try
        {
            if(string.IsNullOrEmpty(text)||Encoding.UTF8.GetByteCount(text)>MaximumBytes)return false;
            using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
                record=Read(XElement.Load(reader,LoadOptions.None));
            return true;
        }
        catch{return false;}
    }
    private static XElement ReadFile(string path)
    {
        var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>MaximumBytes)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
            return XElement.Load(reader,LoadOptions.None);
    }
    internal static bool TryRead(string owner,Guid operation,out Record record)
    {
        record=null;if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
        try
        {
            string path=PathFor(GameIO.GetSaveGameDir(),owner,operation),archive=ArchiveFor(path);Record value=null;
            if(File.Exists(path))value=Read(ReadFile(path));
            if(File.Exists(archive))
            {
                var completed=Read(ReadFile(archive));if(completed.Phase!=4||value!=null&&Encode(value)!=Encode(completed))return false;value=completed;
            }
            if(value==null||value.Owner!=owner||value.Operation!=operation||value.World!=WorldKey(GameManager.Instance.World))return false;record=value;return true;
        }
        catch{return false;}
    }
    private static string ArchiveFor(string path)=>Path.Combine(Path.GetDirectoryName(path),"completed",Path.GetFileName(path));
    private static bool CanFollow(Record before,Record after)
    {
        if(!XNode.DeepEquals(Plan(Write(before)),Plan(Write(after)))||after.Phase<before.Phase||after.Phase>before.Phase+1||
            !string.IsNullOrEmpty(before.OwnerDebitProof)&&after.OwnerDebitProof!=before.OwnerDebitProof||
            !string.IsNullOrEmpty(before.OwnerNoEffectProof)&&after.OwnerNoEffectProof!=before.OwnerNoEffectProof||before.Rejecting&&!after.Rejecting||before.Outcome!=0&&after.Outcome!=before.Outcome)return false;
        if(before.OwnerEscrowRecovery&&!after.OwnerEscrowRecovery||!before.OwnerEscrowRecovery&&after.OwnerEscrowRecovery&&
            (before.Phase!=1||after.Phase!=2||before.Outcome!=0||after.Outcome!=3))return false;
        for(int i=0;i<after.Legs.Count;i++)if(after.Legs[i].Published<before.Legs[i].Published||after.Legs[i].Published>before.Legs[i].Published+1||
            after.Legs[i].Attempted<before.Legs[i].Attempted||after.Legs[i].Attempted>before.Legs[i].Attempted+1||
            before.Legs[i].Restored&&(!after.Legs[i].Restored||after.Legs[i].Published!=before.Legs[i].Published||after.Legs[i].Attempted!=before.Legs[i].Attempted))return false;
        for(int i=0;i<after.Npcs.Count;i++)if(after.Npcs[i].Published<before.Npcs[i].Published||after.Npcs[i].Published>before.Npcs[i].Published+1||after.Npcs[i].Attempted<before.Npcs[i].Attempted||after.Npcs[i].Attempted>before.Npcs[i].Attempted+1)return false;
        return true;
    }
    private static void ArchiveTerminal(string path,XElement original)
    {
        // The final record is already committed and read back. A failed move
        // leaves the root original resolvable; never delete either receipt.
        if((int)original.Attribute("phase")!=4)return;
        try
        {
            string archive=ArchiveFor(path);Directory.CreateDirectory(Path.GetDirectoryName(archive));
            if(File.Exists(archive)){if(!XNode.DeepEquals(ReadFile(archive),original))throw new InvalidDataException("Conflicting completed original.");return;}
            if(!XNode.DeepEquals(ReadFile(path),original))throw new InvalidDataException("Terminal original changed before archive.");
            File.Move(path,archive);
            foreach(string suffix in new[]{".bak",".tmp"})
            {
                string sidecar=path+suffix,target=archive+suffix;if(!File.Exists(sidecar))continue;
                var side=Read(ReadFile(sidecar));var completed=Read(original);
                if(!(suffix==".bak"?CanFollow(side,completed):CanFollow(completed,side)))throw new InvalidDataException("Unknown terminal sidecar retained.");
                if(File.Exists(target)){if(Encode(Read(ReadFile(target)))!=Encode(side))throw new InvalidDataException("Conflicting terminal sidecar retained.");}
                else File.Move(sidecar,target);
            }
        }
        catch(Exception error){Log.Warning("[QuickStack] Saved original terminal retained at root; archive pending: "+error.Message);}
    }
    internal static bool TryReadPendingNativeOriginals(World world,out Record[] records)
        =>TryReadPendingCore(world,false,out records,out _,out _);
    // Named CURRENT-world transition observation, not an effect capability.
    // Parse/validate one original at a time; retain no native item graphs.
    internal static bool TryReadTransitionPending(World world,out RebirthNpcOriginalEffectAdmission.Pending[] pending,out string[] heads)
        =>TryReadPendingCore(world,true,out _,out pending,out heads);
    private static string TransitionHead(Record record)
    {
        using(var hash=System.Security.Cryptography.SHA256.Create())
        using(var stream=new System.Security.Cryptography.CryptoStream(Stream.Null,hash,System.Security.Cryptography.CryptoStreamMode.Write))
        using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{OmitXmlDeclaration=true,Indent=false,NewLineHandling=NewLineHandling.None,ConformanceLevel=ConformanceLevel.Fragment,Encoding=new UTF8Encoding(false),CloseOutput=false}))
        {
            Write(record).WriteTo(writer);writer.Flush();stream.FlushFinalBlock();
            return record.Owner+":"+record.Operation.ToString("N")+":"+BitConverter.ToString(hash.Hash).Replace("-",string.Empty).ToLowerInvariant();
        }
    }
    private static bool TryReadPendingCore(World world,bool transition,out Record[] records,out RebirthNpcOriginalEffectAdmission.Pending[] pending,out string[] heads)
    {
        records=null;pending=null;heads=null;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return false;
        try
        {
            string directory=GameIO.GetSaveGameDir(),root=Path.Combine(directory,"RebirthLogisticsTransfers");var result=transition?null:new List<Record>();var claims=transition?new List<RebirthNpcOriginalEffectAdmission.Pending>():null;var hashes=transition?new List<string>():null;long bytes=0;int candidates=0;
            if(!Directory.Exists(root)){records=new Record[0];pending=new RebirthNpcOriginalEffectAdmission.Pending[0];heads=new string[0];return ReferenceEquals(GameManager.Instance?.World,world)&&GameIO.GetSaveGameDir()==directory;}
            if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return false;
            foreach(string ownerDirectory in Directory.EnumerateDirectories(root))
            {
                string owner=Path.GetFileName(ownerDirectory);
                if(!Hex(owner,64)||owner!=owner.ToLowerInvariant()||(File.GetAttributes(ownerDirectory)&FileAttributes.ReparsePoint)!=0)return false;
                foreach(string path in Directory.EnumerateFiles(ownerDirectory))
                {
                    if(++candidates>4096||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
                    var info=new FileInfo(path);bytes=checked(bytes+info.Length);if(bytes>67108864)return false;
                    string originalPath=path;bool backup=path.EndsWith(".xml.bak",StringComparison.Ordinal),temporary=path.EndsWith(".xml.tmp",StringComparison.Ordinal);
                    if(backup||temporary)originalPath=path.Substring(0,path.Length-4);
                    if(Path.GetExtension(originalPath)!=".xml"||!Guid.TryParseExact(Path.GetFileNameWithoutExtension(originalPath),"N",out var operation)||operation==Guid.Empty)return false;
                    if(!TryRead(owner,operation,out var value)||value.World!=WorldKey(world))return false;
                    if(backup||temporary)
                    {
                        // Recognize only a fully validated predecessor/next
                        // original. A sidecar never grants effect/publication.
                        var side=Read(ReadFile(path));if(!(backup?CanFollow(side,value):CanFollow(value,side)))return false;continue;
                    }
                    if(value.Phase<4)
                    {
                        if(transition){claims.Add(RebirthNpcOriginalEffectAdmission.Logistics(value));hashes.Add(TransitionHead(value));}
                        else result.Add(value);
                    }
                }
                foreach(string child in Directory.EnumerateDirectories(ownerDirectory))
                    if(Path.GetFileName(child)!="completed"||(File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)return false;
            }
            if(!ReferenceEquals(GameManager.Instance?.World,world)||GameIO.GetSaveGameDir()!=directory)return false;
            if(transition){pending=claims.OrderBy(value=>value.Owner,StringComparer.Ordinal).ThenBy(value=>value.Operation).ToArray();heads=hashes.OrderBy(value=>value,StringComparer.Ordinal).ToArray();}
            else records=result.OrderBy(value=>value.Owner,StringComparer.Ordinal).ThenBy(value=>value.Operation).ToArray();return true;
        }
        catch{return false;}
    }
    private static XElement Plan(XElement value)
    {var copy=new XElement(value);copy.Attribute("ownerEscrowRecovery")?.Remove();copy.Attribute("phase").Remove();copy.Attribute("outcome").Remove();copy.Attribute("rejecting").Remove();copy.Attribute("ownerDebitProof").Remove();copy.Attribute("ownerNoEffectProof").Remove();foreach(var leg in copy.Elements().Where(node=>node.Name=="leg"||node.Name=="npc")){leg.Attribute("published").Remove();leg.Attribute("attempted").Remove();leg.Attribute("restored")?.Remove();}return copy;}
    internal static string ImmutablePlanDigest(Record record)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Plan(Write(record)).ToString(SaveOptions.DisableFormatting)))).Replace("-","").ToLowerInvariant();}
    private static bool CanAdmitPending(World world,XElement document)
    {
        // Reserve source/backup/temporary capacity before any owner payment,
        // not only when a later phase happens to enumerate cold originals.
        if(!TryReadPendingNativeOriginals(world,out var pending)||pending.Length>=1365)return false;
        long bytes=checked(3L*(Encoding.UTF8.GetByteCount(document.ToString(SaveOptions.DisableFormatting))+256));int candidates=3*(pending.Length+1);
        foreach(var original in pending)bytes=checked(bytes+3L*(Encoding.UTF8.GetByteCount(Encode(original))+256));
        var known=new HashSet<string>(pending.Select(value=>value.Owner+"/"+value.Operation.ToString("N")),StringComparer.Ordinal);
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsTransfers");
        if(Directory.Exists(root))foreach(string owner in Directory.EnumerateDirectories(root))foreach(string file in Directory.EnumerateFiles(owner))
        {
            string name=Path.GetFileName(file);if(name.EndsWith(".bak",StringComparison.Ordinal)||name.EndsWith(".tmp",StringComparison.Ordinal))name=name.Substring(0,name.Length-4);
            if(known.Contains(Path.GetFileName(owner)+"/"+Path.GetFileNameWithoutExtension(name)))continue;
            bytes=checked(bytes+new FileInfo(file).Length);candidates++;
        }
        return candidates<=4096&&bytes<=67108864&&Encoding.UTF8.GetByteCount(document.ToString(SaveOptions.DisableFormatting))+256<=MaximumBytes;
    }
    internal static bool TryPublish(Record record,Func<bool> current)
    {
        if(writing||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||current==null)return false;
        transitionMutation=new object();writing=true;
        try
        {
            string directory=GameIO.GetSaveGameDir();var world=GameManager.Instance.World;var document=Write(record);
            string path=PathFor(directory,record.Owner,record.Operation);
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&GameIO.GetSaveGameDir()==directory&&WorldKey(world)==record.World&&current();
            if(!Current())return false;
            string archive=ArchiveFor(path);
            if(File.Exists(archive))
            {
                if(!TryRead(record.Owner,record.Operation,out var completed)||completed.Phase!=4||Encode(completed)!=document.ToString(SaveOptions.DisableFormatting))return false;
                return Current();
            }
            if(File.Exists(path))
            {
                var previous=Read(ReadFile(path));if(!CanFollow(previous,record))return false;
            }
            else if(record.Phase!=0||record.Legs.Any(leg=>leg.Published!=0||leg.Attempted!=0||leg.Restored)||record.Npcs.Any(leg=>leg.Published!=0||leg.Attempted!=0)||File.Exists(path+".tmp")||File.Exists(path+".bak")||!CanAdmitPending(world,document))return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {var bytes=Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(!Current()||!RebirthDurableFileCommit.TryPublish(temp,path,out _))return false;
            if(!Current()||!XNode.DeepEquals(document,Write(Read(ReadFile(path)))))return false;
            ArchiveTerminal(path,document);return Current();
        }
        catch(Exception error){Log.Warning("[QuickStack] Original logistics journal retained/refused: "+error.Message);return false;}
        finally{writing=false;}
    }
}


