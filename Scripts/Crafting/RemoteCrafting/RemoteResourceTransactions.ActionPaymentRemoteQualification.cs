using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionRemoteDebitPlan
    {
        internal ActionRemoteClick Click;internal string Original;
        internal byte[] HostNativeBefore;
        internal bool NoOwnerContribution;
        internal ItemStack[] BagAfter,BeltAfter;
        internal RebirthElectricalServiceRecord ServiceSnapshot;
        internal readonly List<(bool Belt,int Slot)> Takes=new List<(bool,int)>();
    }
    // Descriptive client projection from this already-qualified immutable
    // service original. This grants no native target or inventory authority.
    private static RebirthElectricalServiceRecord ReadActionServiceSnapshot(XElement target)
    {
        if(target==null)return null;
        var record=new RebirthElectricalServiceRecord{ClrIdx=(int)target.Attribute("clr"),
            Position=new Vector3i((int)target.Attribute("x"),(int)target.Attribute("y"),(int)target.Attribute("z")),
            Kind=(RebirthElectricalServiceKind)checked((byte)(int)target.Attribute("kind")),Condition=(float)target.Attribute("condition"),Workmanship=(float)target.Attribute("workmanship"),
            LastServiceWorldTime=(ulong)target.Attribute("time"),ServiceCount=(int)target.Attribute("count"),SourceBlock=checked((ushort)(int)target.Attribute("block")),SourceIncarnation=(string)target.Attribute("incarnation"),
            ConfiguredByStableId=(string)target.Attribute("author"),ConfiguredByBackgroundId=(string)target.Attribute("background"),ConfiguredByBonusId=(string)target.Attribute("bonus"),ConfiguredSkillValue=(float)target.Attribute("skill")};
        if(!Enum.IsDefined(typeof(RebirthElectricalServiceKind),record.Kind)||float.IsNaN(record.Condition)||float.IsInfinity(record.Condition)||record.Condition<0f||record.Condition>100f||
            float.IsNaN(record.Workmanship)||float.IsInfinity(record.Workmanship)||record.Workmanship<0f||record.Workmanship>1f||float.IsNaN(record.ConfiguredSkillValue)||float.IsInfinity(record.ConfiguredSkillValue)||
            record.SourceIncarnation==null||record.SourceIncarnation.Length>128||record.ConfiguredByStableId==null||record.ConfiguredByStableId.Length>512||record.ConfiguredByBackgroundId==null||record.ConfiguredByBackgroundId.Length>128||record.ConfiguredByBonusId==null||record.ConfiguredByBonusId.Length>128)
            throw new InvalidDataException("Electrical client description is invalid");
        return record;
    }
    private static XElement ReadRemoteActionOriginal(string text)
    {
        if(text==null||text.Length<1||text.Length>8*1024*1024)throw new InvalidDataException("Electrical original exceeds bound");
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024};
        // Bound the graph before XElement allocates it; a character bound alone
        // permits millions of tiny nodes outside the retained original budget.
        using(var reader=XmlReader.Create(new StringReader(text),settings))
        {int nodes=0;while(reader.Read())if(++nodes>32768||reader.Depth>24||reader.AttributeCount>32)throw new InvalidDataException("Electrical original graph exceeds bound");}
        using(var reader=XmlReader.Create(new StringReader(text),settings))return XElement.Load(reader);
    }
    private static ItemStack[] RemoteActionFrame(XElement element)
        =>RebirthCamperInventorySnapshot.Restore(RemoteActionFrameBytes(element));
    private static byte[] RemoteActionFrameBytes(XElement element)
    {
        string encoded=element?.Value;
        if(encoded==null||encoded.Length<1||encoded.Length>((2*1024*1024+2)/3)*4||element.HasElements)throw new InvalidDataException("Electrical frame exceeds bound");
        byte[] bytes=Convert.FromBase64String(encoded);if(bytes.Length>2*1024*1024||Convert.ToBase64String(bytes)!=encoded)throw new InvalidDataException("Noncanonical electrical frame");
        return bytes;
    }
    private static bool RemoteActionNativeBeforeMatches(ActionRemoteClick click,byte[] native,bool phaseReserved=false)
    {
        if(click==null||native==null||native.Length<6||native.Length>16*1024*1024)return false;
        IDisposable scratch=null;
        if(!phaseReserved&&!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out scratch))return false;
        using(scratch)
        {
            var data=new PlayerDataFile();
            using(var capture=new RebirthCamperEnergySavedOwner.Capture())using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0'||reader.ReadByte()!=PlayerDataFile.cFileVersion)return false;
                data.Read(reader,PlayerDataFile.cFileVersion,global::StreamModeRead.Persistency);
                return stream.Position==stream.Length&&!capture.Failed&&data.dragAndDropItem!=null&&capture.Items.TryGetValue(data.dragAndDropItem,out var cursor)&&cursor.IsEmpty()&&
                    data.id==click.OriginalNativeEntityId&&data.deaths==click.Deaths&&!data.bDead&&
                    RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(click.Operation),out float phase,out bool present)&&present&&phase==1f&&
                    SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,true),click.BagBefore)&&SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,false),click.BeltBefore);
            }
        }
    }
    // This typed result is private and is not a setter permit. The actual
    // authenticated grant dispatcher must additionally retain the exact native
    // connection and durably entered one-use invocation before applying it.
    private static ActionRemoteDebitPlan QualifyRemoteActionDebit(ActionRemoteClick click,string original,byte[] hostNativeBefore,out IDisposable memory,bool terminalWithoutOwnerInput=false,ActionClientNativeRead savedOwner=null)
    {
        memory=null;
        if(savedOwner!=null&&(!ReferenceEquals(click?.RestartOwnerSource,savedOwner)||!ActionClientLoadedCurrent(savedOwner)||!savedOwner.Operations.Contains(click.Operation)))return null;
        if(click==null||!(terminalWithoutOwnerInput||savedOwner!=null?click.OwnerMetadataCurrent():click.Current())||!click.BeforeObserved||hostNativeBefore==null||hostNativeBefore.Length<6||hostNativeBefore.Length>16*1024*1024||
            savedOwner==null&&!terminalWithoutOwnerInput&&(!SameActionItems(click.Bag.items,click.BagBefore)||!SameActionItems(click.Belt.items,click.BeltBefore)))return null;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out var scratch))return null;
        try
        {
            if(!RemoteActionNativeBeforeMatches(click,hostNativeBefore,true))return null;
            var image=ReadRemoteActionOriginal(original);var target=image.Element("electrical");
            if(image.Name!="resourceActionOriginal"||(int?)image.Attribute("version")!=1||(string)image.Attribute("operation")!=click.Operation.ToString("N")||
                (string)image.Attribute("action")!="electrical-service"||(string)image.Attribute("world")!=click.NativeWorld||(string)image.Attribute("account")!=click.Account||(string)image.Attribute("creation")!=click.Creation||
                (int?)image.Attribute("deaths")!=click.Deaths||(int?)image.Attribute("nativeId")!=click.OriginalNativeEntityId||target==null||(int?)target.Attribute("x")!=click.Position.x||(int?)target.Attribute("y")!=click.Position.y||
                (int?)target.Attribute("z")!=click.Position.z||(int?)target.Attribute("block")!=click.Block.type)return null;
            var owner=image.Element("ownerOriginal");
            var sources=image.Element("sources")?.Elements("source").ToArray();if(sources==null||sources.Length<1||sources.Length>32)return null;
            bool noOwner=owner==null&&sources.All(source=>(int?)source.Attribute("kind")!=0);
            if(terminalWithoutOwnerInput?!noOwner:owner==null)return null;
            if(owner!=null&&(!SameActionItems(RemoteActionFrame(owner.Element("bag")),click.BagBefore)||!SameActionItems(RemoteActionFrame(owner.Element("belt")),click.BeltBefore)))return null;
            var paidByType=new Dictionary<int,long>();var required=click.Price.ToDictionary(stack=>stack.itemValue.type,stack=>(long)stack.count);
            var plan=new ActionRemoteDebitPlan{Click=click,Original=original,ServiceSnapshot=ReadActionServiceSnapshot(target),NoOwnerContribution=noOwner,HostNativeBefore=hostNativeBefore,BagAfter=ItemStack.Clone(click.BagBefore),BeltAfter=ItemStack.Clone(click.BeltBefore)};
            var seen=new HashSet<bool>();
            foreach(var source in sources)
            {
                var paid=RemoteActionFrame(source.Element("paid"));
                foreach(var stack in paid)
                {
                    if(stack.IsEmpty()||stack.count<1||!required.ContainsKey(stack.itemValue.type)||!MatchesConsumable(stack,click.Price.First(value=>value.itemValue.type==stack.itemValue.type).itemValue))return null;
                    paidByType.TryGetValue(stack.itemValue.type,out long count);paidByType[stack.itemValue.type]=checked(count+stack.count);
                }
                if((int?)source.Attribute("kind")!=0)continue;
                bool belt=(bool)source.Attribute("belt");if(!seen.Add(belt)||(string)source.Attribute("id")!=(belt?"owner-belt":"owner-bag"))return null;
                var before=belt?click.BeltBefore:click.BagBefore;
                if(!SameActionItems(RemoteActionFrame(source.Element("before")),before))return null;
                var expected=ItemStack.Clone(before);var takes=source.Element("takes")?.Elements("take").ToArray();
                if(takes==null||takes.Length<1||takes.Length!=paid.Length)return null;
                var slots=new HashSet<int>();
                for(int index=0;index<takes.Length;index++)
                {
                    int slot=(int)takes[index].Attribute("slot"),count=(int)takes[index].Attribute("count");
                    if(slot<0||slot>=before.Length||!slots.Add(slot)||count<1||count>before[slot].count||paid[index].count!=count||
                        !RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(before[slot].itemValue.Clone(),count),paid[index]))return null;
                    expected[slot].count-=count;if(expected[slot].count==0)expected[slot]=ItemStack.Empty.Clone();plan.Takes.Add((belt,slot));
                }
                if(!SameActionItems(RemoteActionFrame(source.Element("after")),expected))return null;
                if(belt)plan.BeltAfter=expected;else plan.BagAfter=expected;
            }
            if(!noOwner&&plan.Takes.Count==0||required.Any(pair=>!paidByType.TryGetValue(pair.Key,out long count)||count!=pair.Value)||paidByType.Count!=required.Count)return null;
            if(noOwner){if(!terminalWithoutOwnerInput||!click.OwnerMetadataCurrent())return null;}
            else if(savedOwner==null)
            {
                for(int index=0;index<click.BagReferences.Length;index++)if(!ReferenceEquals(click.Bag.items[index],click.BagReferences[index]))return null;
                for(int index=0;index<click.BeltReferences.Length;index++)if(!ReferenceEquals(click.Belt.items[index],click.BeltReferences[index]))return null;
                if(!click.Current())return null;
            }
            else if(!click.OwnerMetadataCurrent())return null;
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,MeasureActionRemotePlanMemory(plan)))return null;
            memory=scratch;scratch=null;return plan;
        }
        finally{scratch?.Dispose();}
    }
}
