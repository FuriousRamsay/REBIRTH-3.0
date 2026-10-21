using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    // Reads only the operation named by the actual native owner load. A control
    // row authenticates its full stage body; it never substitutes for a saved
    // native source prefix or for the canonical repository's own saved image.
    private static Dictionary<string,XElement> ReadInventoryHistory(string directory,Guid operation,out string original)
    {
        original=null;
        for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)
            if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        var heads=System.IO.Directory.EnumerateFiles(directory,"head-*.xml").Take(1025).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
        if(heads.Length==0||heads.Length>1024)throw new InvalidDataException();
        var stages=new Dictionary<string,XElement>(StringComparer.Ordinal);
        for(int index=0;index<heads.Length;index++)
        {
            string Read(string path,int maximum)
            {
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>maximum||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
                return File.ReadAllText(path,new System.Text.UTF8Encoding(false,true));
            }
            var head=RebirthCookingRemoteAdmission.ReadProtocolXml(Read(heads[index],4096),4096);
            string stage=(string)head.Attribute("stage");
            if(head.Name!="playerConsumptionHead"||head.Nodes().Any()||head.Attributes().Count()!=6||(int?)head.Attribute("version")!=1||
                (int?)head.Attribute("sequence")!=index||Path.GetFileName(heads[index])!="head-"+index.ToString("D5")+".xml"||
                (string)head.Attribute("operation")!=operation.ToString("N")||string.IsNullOrEmpty(stage)||stage.Length>64||
                stage.Any(value=>!char.IsLetterOrDigit(value)&&value!='-')||stages.ContainsKey(stage))throw new InvalidDataException();
            string stagePath=Path.Combine(directory,stage+".xml");
            var file=new FileInfo(stagePath);if(!file.Exists||file.Length<1||file.Length>64L*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
            string hash;XElement body;
            using(var stream=new FileStream(stagePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=file.Length)throw new InvalidDataException();
                using(var digest=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(digest.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                stream.Position=0;
                using(var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64L*1024*1024}))body=XElement.Load(reader);
            }
            if(hash!=(string)head.Attribute("body"))throw new InvalidDataException();
            string retained=(string)body.Element("original");
            if(body.Name!="playerConsumptionStage"||body.Attributes().Count()!=2||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("stage")!=stage||body.Elements("original").Count()!=1||retained==null||
                RebirthRemoteResourceDurableJournal.Digest(retained)!=(string)head.Attribute("original")||original!=null&&original!=retained)throw new InvalidDataException();
            original=original??retained;
            // Every stage repeats the same immutable original. Retain one
            // string, and only lazy native descriptors, rather than holding
            // every complete PDS/base64 graph at once. The streamed body hash
            // above still authenticates every byte of every original stage.
            body.Element("original").Value=original;
            foreach(var native in body.Descendants("native").ToArray())
            {
                if(native.HasAttributes||native.HasElements||native.Value.Length<8||native.Value.Length>22369624)throw new InvalidDataException();
                int length=native.Value.Length;native.Value="";native.SetAttributeValue("encodedLength",length);
            }
            body.SetAttributeValue("verifiedBody",hash);body.SetAttributeValue("verifiedPath",stagePath);stages.Add(stage,body);
        }
        if(!stages.ContainsKey("prepared"))throw new InvalidDataException();
        return stages;
    }
    private static byte[] ReadInventoryNativeStage(Dictionary<string,XElement> history,string phase)
    {
        if(!history.TryGetValue("owner-"+phase+"-saved",out var row)||row.Elements().Count()!=2||row.Element("native") is not XElement native)throw new InvalidDataException();
        return ReadBoundInventoryNative(row,native);
    }
    private static byte[] ReadBoundInventoryNative(XElement row,XElement descriptor)
    {
        string path=(string)row.Attribute("verifiedPath"),expected=(string)row.Attribute("verifiedBody");
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>64L*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0||descriptor.HasElements||descriptor.Attributes().Count()!=1)throw new InvalidDataException();
        for(var parent=file.Directory;parent!=null;parent=parent.Parent)if((parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        XElement full;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=expected)throw new InvalidDataException();
            stream.Position=0;
            using(var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64L*1024*1024}))full=XElement.Load(reader);
        }
        var native=full.Descendants("native").SingleOrDefault();
        if(full.Name!="playerConsumptionStage"||(string)full.Attribute("stage")!=(string)row.Attribute("stage")||(string)full.Element("original")!=(string)row.Element("original")||
            native==null||native.HasAttributes||native.HasElements||native.Value.Length!=(int?)descriptor.Attribute("encodedLength"))throw new InvalidDataException();
        byte[] bytes=Convert.FromBase64String(native.Value);
        if(bytes.Length<6||bytes.Length>16*1024*1024||bytes[0]!='t'||bytes[1]!='t'||bytes[2]!='p'||bytes[3]!=0||bytes[4]!=PlayerDataFile.cFileVersion)throw new InvalidDataException();
        return bytes;
    }
    private static bool HasQualifiedInventoryNative(Dictionary<string,XElement> history,string phase)
    {
        if(history.ContainsKey("owner-"+phase+"-saved")){ReadInventoryNativeStage(history,phase);return true;}
        foreach(var row in history.Where(pair=>pair.Key.StartsWith("cold-native-",StringComparison.Ordinal)).Select(pair=>pair.Value))
        {
            var qualified=row.Element("qualifiedNative");var proof=qualified?.Element("qualification");var native=qualified?.Element("native");
            if(row.Elements().Count()!=2||qualified==null||qualified.HasAttributes||qualified.Elements().Count()!=2||proof==null||proof.HasElements||proof.Attributes().Count()!=4||native==null)continue;
            float source=(float)proof.Attribute("source"),secondary=(float)proof.Attribute("secondary"),used=(float)proof.Attribute("use");
            if(source!=1f||phase=="secondary"&&secondary!=1f||phase=="after"&&(secondary!=1f||used!=1f))continue;
            byte[] bytes=ReadBoundInventoryNative(row,native);
            if(bytes.Length>=6&&bytes.Length<=16*1024*1024&&bytes[0]=='t'&&bytes[1]=='t'&&bytes[2]=='p'&&bytes[3]==0&&bytes[4]==PlayerDataFile.cFileVersion)return true;
        }
        return false;
    }
    private static void PumpLoadedInventoryOriginals()
    {
        if(!InventoryProtocolActive)return;
        foreach(var load in inventoryNativeLoads.ToArray())
        {
            if(!load.Current())continue;
            long now=System.Diagnostics.Stopwatch.GetTimestamp();if(now<load.NextResume)continue;
            load.NextResume=now+System.Diagnostics.Stopwatch.Frequency;
            var operations=load.Operations;if(operations.Length==0)continue;
            int count=Math.Min(4,operations.Length);
            for(int offset=0;offset<count;offset++)
            {
                var operation=operations[(load.ResumeCursor+offset)%operations.Length];
                if(inventoryOriginals.ContainsKey(operation))continue;
                try{TryResumeInventoryOriginal(load,operation);}catch(Exception error)
                {if(load.Reported.Add(operation))Log.Warning("[REBIRTH Consumption] native original retained: "+error.GetType().Name);}
            }
            load.ResumeCursor=(load.ResumeCursor+count)%operations.Length;
        }
    }
    private static void TryResumeInventoryOriginal(InventoryNativeLoad load,Guid operation)
    {
        IDisposable memory=null;
        try
        {
            var actor=load.Actor;
            if(!load.Current()||load.Dead||actor.IsDead()||inventoryOriginals.Count>=16||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.CanonicalId!=load.Account||
                !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(load.Path,load.Native)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out memory))return;
            string directory=Path.Combine(load.Save,"RebirthData","PlayerConsumption",identity.StorageKey,operation.ToString("N"));
            var history=ReadInventoryHistory(directory,operation,out string text);
            var image=RebirthCookingRemoteAdmission.ReadProtocolXml(text,16*1024*1024);
            string[] names={"beforeBag","beforeBelt","afterBag","afterBelt","bagLocks","beltLocks","source","replacement","siblings","serving","plan","metabolismBefore","metabolismAfter"};
            if(image.Name!="playerSourceConsumption"||image.Attributes().Count()!=9||(int?)image.Attribute("version")!=1||
                (string)image.Attribute("operation")!=operation.ToString("N")||(string)image.Attribute("owner")!=identity.CanonicalId||
                (string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                ((int?)image.Attribute("deaths")!=actor.Died&&(!history.ContainsKey("committed")||(int?)image.Attribute("deaths") is not int historicalDeaths||historicalDeaths<0||historicalDeaths>actor.Died))||
                (string)image.Attribute("world")!=actor.world.worldState.Guid||image.Elements().Count()!=names.Length||names.Any(name=>image.Elements(name).Count()!=1))return;
            var bag=PurposeInventory(image.Element("beforeBag"));var belt=PurposeInventory(image.Element("beforeBelt"));
            bool[] Locks(string name,int count)
            {var row=image.Element(name);if(row.HasAttributes||row.HasElements||row.Value.Length!=count||row.Value.Any(value=>value!='0'&&value!='1'))throw new InvalidDataException();return row.Value.Select(value=>value=='1').ToArray();}
            var source=PurposeStack(image.Element("source"));var replacement=PurposeStack(image.Element("replacement"));var siblings=PurposeStack(image.Element("siblings"));
            if(!RebirthConsumptionInventoryPlan.TryCreate((RebirthMetabolismSourceKind)checked((byte)(int)image.Attribute("sourceKind")),(int)image.Attribute("sourceSlot"),(int)image.Attribute("beltCount"),
                bag,belt,Locks("bagLocks",bag.Length),Locks("beltLocks",belt.Length),source,replacement,siblings,out var inventory)||
                !inventory.MatchesAfter(PurposeInventory(image.Element("afterBag")),PurposeInventory(image.Element("afterBelt"))))return;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(load.Data,InventoryBeforeReceipt(operation),out float before,out _)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(load.Data,InventorySourceReceipt(operation),out float paid,out _)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(load.Data,InventorySecondaryReceipt(operation),out float secondary,out _)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(load.Data,InventoryUseReceipt(operation),out float used,out _))return;
            bool retired=paid==3f;
            if((int?)image.Attribute("deaths")!=actor.Died&&!retired)return;
            if(retired?(before!=0f||secondary!=0f||used!=0f||!history.ContainsKey("committed")):before!=1f)return;
            var commands=inventory.Commands();int prefix=paid==1f||retired?commands.Length:paid>=101f&&(float)(int)paid==paid?(int)paid-100:paid==0f?0:-1;
            if(prefix<0||prefix>commands.Length||!retired&&!inventory.MatchesPrefix(RebirthGearPlayerDataInventory.ReadSlots(load.Data,true),RebirthGearPlayerDataInventory.ReadSlots(load.Data,false),prefix)||
                secondary!=0f&&secondary!=1f||used!=0f&&used!=1f||used==1f&&secondary!=1f||paid!=1f&&(secondary!=0f||used!=0f))return;
            if(!history.ContainsKey("owner-before-intent")||prefix>0&&!history.ContainsKey("source-intent"))return;
            for(int ordinal=0;ordinal<prefix;ordinal++)if(!history.ContainsKey("source-command-"+ordinal))return;
            byte[] nativeBefore=ReadInventoryNativeStage(history,"before");
            bool applied=history.TryGetValue("ingestion-saved",out var ingestion);
            // An entered deferred callback without its positive return is
            // genuinely ambiguous. A source checkpoint never authorizes replay.
            if(history.ContainsKey("secondary-intent")&&!history.ContainsKey("secondary-returned")||
                secondary==1f&&!history.ContainsKey("owner-secondary-intent")||used==1f&&!history.ContainsKey("owner-use-intent"))return;
            bool committed=history.TryGetValue("committed",out var completion);
            if(committed&&(!applied||!history.ContainsKey("secondary-returned")||!history.ContainsKey("owner-secondary-intent")||!history.ContainsKey("owner-use-intent")))return;
            if(committed&&(!HasQualifiedInventoryNative(history,"source")||!HasQualifiedInventoryNative(history,"secondary")||!HasQualifiedInventoryNative(history,"after")))return;
            // A complete saved terminal is metadata-only; the actor's canonical
            // state may legitimately have progressed since that serving.
            bool deferredStateSaved=history.TryGetValue("secondary-state-saved",out var secondaryState);
            if((secondary==1f||used==1f||committed)&&!deferredStateSaved)return;
            var metabolism=committed?RebirthMetabolismStateRepository.CaptureNpcServingState(actor,out _):applied?
                RebirthMetabolismStateRepository.QualifyColdAppliedNpcServing(actor,(string)(deferredStateSaved?secondaryState:ingestion).Element("saved"),(string)image.Element("metabolismBefore")):
                RebirthMetabolismStateRepository.QualifyColdNpcServing(actor,(string)image.Element("metabolismBefore"),out _);
            if(metabolism==null)return;
            var state=committed?RebirthMetabolismStateRepository.ReadHistoricalNpcServing(metabolism.Id,(string)completion.Element("saved")):
                applied?metabolism.After:RebirthMetabolismStateRepository.HydrateColdNpcAfter(metabolism,(string)image.Element("metabolismAfter"));
            if(state==null||image.Element("serving").HasAttributes||image.Element("serving").HasElements||!RebirthNativeItemCodec.TryDecode(image.Element("serving").Value,out var serving))return;
            var plan=RebirthMetabolismService.HydrateNpcPlan(actor,state,source,new ItemStack(serving,1),replacement,siblings,image.Element("plan"));if(plan==null)return;
            var original=new InventoryOriginal(operation,actor,identity,inventory,metabolism,plan,memory,text){BeforeIssued=true,SourceIssued=history.ContainsKey("source-intent"),QualifiedPrefix=prefix,
                SourceSaved=paid==1f||retired,CanonicalEntered=applied,CanonicalSaved=applied,DeferredEntered=history.ContainsKey("secondary-intent"),DeferredReturned=history.ContainsKey("secondary-returned"),
                DeferredStateSaved=deferredStateSaved,
                SecondaryEntered=history.ContainsKey("owner-secondary-intent"),SecondarySaved=secondary==1f||committed,NativeEntered=history.ContainsKey("owner-use-intent"),NativeSaved=used==1f||committed,Committed=committed};
            if(!original.Current()||!load.Current())return;
            // Retain the complete actual loaded file under its immutable body
            // identity. Later saved secondary/use files must not overwrite the
            // historical earlier source-stage image.
            string nativeDigest;using(var digest=System.Security.Cryptography.SHA256.Create())nativeDigest=BitConverter.ToString(digest.ComputeHash(load.Native)).Replace("-","").ToLowerInvariant();
            original.Record("cold-native-"+nativeDigest.Substring(0,32),new XElement("qualifiedNative",
                new XElement("native",Convert.ToBase64String(load.Native)),new XElement("qualification",new XAttribute("source",paid),new XAttribute("secondary",secondary),new XAttribute("use",used),new XAttribute("prefix",prefix))));
            var phases=new Dictionary<string,InventoryOwnerFile>();
            phases.Add("before",new InventoryOwnerFile{Original=original,Native=nativeBefore,Path=load.Path,Phase="before",Accepted=true,Saved=true,Archived=true});
            void Add(string phase)=>phases.Add(phase,new InventoryOwnerFile{Original=original,Native=null,Path=load.Path,Phase=phase,Accepted=true,Saved=true,Archived=true});
            if(paid==1f||retired)Add("source");if(secondary==1f)Add("secondary");if(used==1f)Add("after");if(retired)Add("retired");
            lock(inventoryOwnerGate){inventoryOwnerFiles.Add(original,phases);inventoryOriginals.Add(operation,original);}
            ObserveInventoryAdmission(operation,actor,original.Purpose);
            memory=null;
            if(actor is EntityPlayerLocal local)RestoreInventoryClientNative(local,load.Data,null,new[]{operation},true);
            PumpInventoryOriginal(original);
        }
        finally{memory?.Dispose();}
    }
}
