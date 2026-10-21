using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private static XElement ParseOriginal(string text)
    {using var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});return XElement.Load(reader);}
    private static ItemStack ReadOriginalStack(XElement row)
    {
        if(row==null||row.Attributes().Count()!=2||row.HasElements)throw new InvalidDataException("Missing full original serving");
        int count=(int)row.Attribute("count");string encoded=(string)row.Attribute("item");if(count==0&&encoded==string.Empty)return ItemStack.Empty.Clone();
        if(count<1||!RebirthNativeItemCodec.TryDecode(encoded,out var item)||item?.ItemClass==null)throw new InvalidDataException("Invalid original native item");return new ItemStack(item,count);
    }
    private static RebirthNpcInventorySnapshot ReadOriginalSource(XElement image,RebirthNpcStableId id,uint revision)
    {
        if(image?.Name!="inventory"||image.HasAttributes)throw new InvalidDataException("Missing full original source");
        var result=new RebirthNpcInventorySnapshot{NpcId=id,Revision=revision,Quantities=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase),Reservations=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)};
        foreach(var row in image.Elements())
        {
            if(row.Name=="nativeStacks"){if(result.NativeStacks!=null||!RebirthNpcNativeStackSet.TryRead(row,out var native)||native.Owner!=id.ToString()||native.Revision!=revision)throw new InvalidDataException("Original native source lineage changed");result.NativeStacks=native;continue;}
            if(row.Name!="quantity"&&row.Name!="reserved"||row.HasElements||row.Attributes().Count()!=2)throw new InvalidDataException("Unknown source original member");
            string key=(string)row.Attribute("key");int count=(int)row.Attribute("count");if(string.IsNullOrWhiteSpace(key)||count<1)throw new InvalidDataException("Invalid original source quantity");
            (row.Name=="quantity"?result.Quantities:result.Reservations).Add(key,count);
        }
        if(result.NativeStacks!=null&&!result.NativeStacks.FitsQuantities(result.Quantities))throw new InvalidDataException("Native source metadata exceeds original quantities");return result;
    }
    private static bool SourceHeadUnissued(string directory,string text)
    {
        var head=ReadRecord(Path.Combine(directory,"control.xml"));
        return head.Name=="npcConsumptionHead"&&head.Attributes().Count()==2&&(int?)head.Attribute("version")==1&&(string)head.Attribute("stage")=="source-saved"&&
            head.Elements().Count()==1&&(string)head.Element("original")==text&&Stage(directory,"source-saved",text);
    }
    private static string ColdPositiveHead(string directory,string text)
    {
        var head=ReadRecord(Path.Combine(directory,"control.xml"));string stage=(string)head.Attribute("stage");
        if(head.Name!="npcConsumptionHead"||head.Attributes().Count()!=2||(int?)head.Attribute("version")!=1||head.Elements().Count()!=1||(string)head.Element("original")!=text||
            stage!="source-saved"&&stage!="ingestion-saved"&&stage!="owner-secondary-saved"&&stage!="owner-after-saved"||!Stage(directory,stage,text))return null;
        return stage;
    }
    private static bool RecordFirstIngestion(Original original)
    {
        ObserveScenario(original,"before-ingestion-permit");
        // This exact positive predecessor head is published before any first
        // ingestion permit. A crash while writing the intent file but before
        // its head cannot have invoked ingestion; converge that head first.
        if(!SourceHeadUnissued(original.Directory,original.Text))return false;
        Record(original,"ingestion-intent");AdvanceHead(original,"ingestion-intent");
        var head=ReadRecord(Path.Combine(original.Directory,"control.xml"));
        return (string)head.Attribute("stage")=="ingestion-intent"&&(string)head.Element("original")==original.Text;
    }
    private static void PumpColdConsumptionOriginals()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var loaded in nativeOwners.Values.Where(value=>value.Hydrated).Take(4).ToArray())
        {
            foreach(var completed in loaded.Originals.ToArray())
            {try{TryRetireAlreadyCommitted(loaded,completed.Key,completed.Value);}catch{}if(loaded.Actor==null)break;}
            if(loaded.Actor==null)continue;
            if(loaded.Originals.Count!=1)continue; // ambiguous coupled owner originals retain custody.
            var saved=loaded.Originals.Single();
            try{if(!TryRetireAlreadyCommitted(loaded,saved.Key,saved.Value))TryResumeColdSavedSource(loaded,saved.Key,saved.Value);}catch(Exception error){Log.Warning("[REBIRTH NPC] cold consumption retained: "+error.Message);}
        }
    }
    private static void TryResumeColdSavedSource(NpcOwnerLoad loaded,string directory,string text)
    {
        var actor=loaded.Actor;var image=ParseOriginal(text);string head=ColdPositiveHead(directory,text);bool applied=head!="source-saved",secondary=head=="owner-secondary-saved"||head=="owner-after-saved",terminal=head=="owner-after-saved";
        if((int?)image.Attribute("version")!=2||actor==null||!loaded.Loaded||!loaded.ReadReturned||!loaded.Hydrated||loaded.Dead||actor.IsDead()||actor.Died!=loaded.Deaths||
            !ReferenceEquals(loaded.World,GameManager.Instance?.World)||!ReferenceEquals(actor.world,loaded.World)||!ReferenceEquals(loaded.World.GetEntity(actor.entityId),actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var account)||account.CanonicalId!=loaded.Account||(string)image.Attribute("owner")!=loaded.Account||
            (string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||(int?)image.Attribute("deaths")!=actor.Died||(ulong?)image.Attribute("recovery")!=RebirthNpcRecoveryGeneration.RequireCurrent()||
            !RebirthNpcStableId.TryParse((string)image.Attribute("npc"),out var id)||pending.ContainsKey(id)||pending.Count>=4||!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||
            head==null||!HistoricalOwner(directory,"before",text))return;
        if(actor is not EntityPlayerLocal&&!loaded.Ready.Contains(operation))return;
        if(applied&&!Stage(directory,"ingestion-saved",text)||secondary&&(!Stage(directory,"secondary-returned",text)||!Stage(directory,"secondary-state-saved",text)||!Stage(directory,"owner-secondary-returned",text)||!HistoricalOwner(directory,"secondary",text))||
            terminal&&(!Stage(directory,"owner-effect-returned",text)||!HistoricalOwner(directory,"after",text)))return;
        var body=nativeBodies.Values.SingleOrDefault(value=>value.Returned&&value.Stable==id&&ReferenceEquals(value.World,loaded.World)&&value.Save==loaded.Save);
        if(body?.Body==null||body.Body.IsDead()||!ReferenceEquals(body.Body.world,loaded.World)||!ReferenceEquals(loaded.World.GetEntity(body.Body.entityId),body.Body)||body.Body.RebirthRuntimeState?.StableId!=id)return;
        var bodyOriginal=image.Element("nativeBody");if(bodyOriginal==null||(string)bodyOriginal.Attribute("stable")!=id.ToString()||(int?)bodyOriginal.Attribute("class")!=body.Body.entityClass)return;
        ReadOriginalBody(bodyOriginal,id);
        // Complete positive saved source and baseline owner images accompany the
        // original operation. Current equality alone cannot enter this method.
        string archivedSource=(string)ReadRecord(Path.Combine(directory,"source-saved.xml")).Element("saved");
        if(!RebirthNpcInventoryPersistenceStore.QualifyColdConsumptionSource(id,archivedSource))return;
        var before=ReadOriginalSource(image.Element("sourceBefore")?.Element("inventory"),id,(uint)image.Attribute("revision"));if(before.Revision==uint.MaxValue)return;
        var after=ReadOriginalSource(image.Element("sourceAfter")?.Element("inventory"),id,before.Revision+1);
        var actual=RebirthNpcInventoryTransactionService.GetSnapshot(id);
        if(actual.Revision!=after.Revision||!XNode.DeepEquals(Snapshot(actual.Quantities,actual.Reservations,actual.NativeStacks),Snapshot(after.Quantities,after.Reservations,after.NativeStacks)))return;
        var ownerNative=ReadHistoricalOwnerBytes(directory,"before",text);
        byte[] secondaryNative=secondary?ReadHistoricalOwnerBytes(directory,"secondary",text):null;
        if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(loaded.Path,loaded.Native))return;
        if(!terminal&&!(secondary?secondaryNative:ownerNative).SequenceEqual(loaded.Native))return;
        if(terminal&&(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Data,OwnerReceipt(operation),out float receipt,out bool present)||!present||receipt!=1f))return;
        string canonicalSaved=applied?(string)ReadRecord(Path.Combine(directory,secondary?"secondary-state-saved.xml":"ingestion-saved.xml")).Element("saved"):null;
        RebirthMetabolismStateRepository.NpcServingState metabolism=null;RebirthMetabolismState state;
        if(terminal)
        {
            string historicalId=(string)ParseOriginal((string)image.Element("metabolismAfter")).Attribute("id");
            state=RebirthMetabolismStateRepository.ReadHistoricalNpcServing(historicalId,canonicalSaved);
        }
        else
        {
            metabolism=applied?RebirthMetabolismStateRepository.QualifyColdAppliedNpcServing(actor,canonicalSaved,(string)image.Element("metabolismBefore")):
                RebirthMetabolismStateRepository.QualifyColdNpcServing(actor,(string)image.Element("metabolismBefore"),out _);
            if(metabolism==null)return;state=applied?metabolism.After:RebirthMetabolismStateRepository.HydrateColdNpcAfter(metabolism,(string)image.Element("metabolismAfter"));
        }
        if(state==null)return;
        var source=ReadOriginalStack(image.Element("source"));var plan=RebirthMetabolismService.HydrateNpcPlan(actor,state,source,ReadOriginalStack(image.Element("serving")),ReadOriginalStack(image.Element("replacement")),ReadOriginalStack(image.Element("siblings")),image.Element("plan"));if(plan==null)return;
        var original=new Original{Operation=operation,World=loaded.World,Actor=actor,Npc=body.Body,Id=id,Account=loaded.Account,Creation=(string)image.Attribute("creation"),Deaths=actor.Died,Save=loaded.Save,Directory=directory,Text=text,Key=(string)image.Attribute("key"),Recovery=(ulong)image.Attribute("recovery"),
            Before=before,AfterNative=after.NativeStacks,AfterQuantities=after.Quantities,Source=source,SourceRecord=Guid.ParseExact((string)image.Attribute("record"),"N"),Metabolism=metabolism,Plan=plan,SourceEntered=true,SourceReturned=true,SourceSaved=true,
            CanonicalEntered=applied,CanonicalSaved=applied,DeferredEntered=secondary,DeferredReturned=secondary,OwnerSecondaryReturned=secondary,SecondaryPdsRequested=secondary,OwnerEntered=terminal,OwnerReturned=terminal,OwnerSaved=terminal,MetadataOnly=terminal};
        if(!Current(original)||ColdPositiveHead(directory,text)!=head||!applied&&!RebirthMetabolismStateRepository.NpcServingBeforeCurrent(metabolism))return;
        lock(ownerGate)
        {
            if(ownerFiles.ContainsKey(original))return;
            ownerFiles.Add(original,new Dictionary<string,OwnerFile>{{"before",new OwnerFile{Original=original,Native=ownerNative,Path=loaded.Path,Phase="before",Accepted=true,Saved=true,Archived=true}}});
            if(secondary)ownerFiles[original].Add("secondary",new OwnerFile{Original=original,Native=secondaryNative,Path=loaded.Path,Phase="secondary",Accepted=true,Saved=true,Archived=true});
            try{pending.Add(id,original);}catch{ownerFiles.Remove(original);throw;}
            original.Memory=loaded.Memory;loaded.Memory=null;
        }
        ObserveColdSourceAdoption(original,loaded,body);
        ReleaseNativeOwner(loaded);nativeBodies.Remove(body.Body);body.Memory?.Dispose();body.Memory=null;body.Payload=null;body.Body=null;body.World=null;
        Advance(original); // source debit is skipped; only positively never-issued effects receive a first intent.
    }
    private static void CompleteColdTerminalMetadata(Original original)
    {
        if(!Current(original)||!original.MetadataOnly||!original.OwnerReturned||!original.OwnerSaved||!HistoricalOwner(original.Directory,"before",original.Text)||!HistoricalOwner(original.Directory,"secondary",original.Text)||!HistoricalOwner(original.Directory,"after",original.Text)||
            !Stage(original.Directory,"source-saved",original.Text)||!Stage(original.Directory,"ingestion-saved",original.Text)||!Stage(original.Directory,"secondary-state-saved",original.Text)||!Stage(original.Directory,"owner-effect-returned",original.Text))return;
        Record(original,"committed");ArchiveCompletedOriginal(original);
        if(original.Actor is EntityPlayerLocal local){local.Buffs.RemoveCustomVar(OwnerReceipt(original.Operation));local.Buffs.RemoveCustomVar(SecondaryReceipt(original.Operation));local.Buffs.RemoveCustomVar(BeforeReceipt(original.Operation));GameManager.Instance.SaveLocalPlayerData();}
        else if(!SendOwnerPurpose(original,2))return;
        original.Terminal=true;pending.Remove(original.Id);ReleaseOwner(original);
    }
    private static void ObserveColdSourceAdoption(Original original,NpcOwnerLoad owner,NpcBodyLoad body)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&(ColdSourceAudit.Count<64||ColdSourceAudit.ContainsKey(original.Operation)))
            ColdSourceAudit[original.Operation]=new ColdSourceReceipt{Process=System.Diagnostics.Process.GetCurrentProcess().Id,NativeOwnerBytes=owner.Native.Length,NativeBodyBytes=body.Payload.Length,Revision=original.Before.Revision+1};
#endif
    }
#if REBIRTH_DEBUG
    private sealed class ColdSourceReceipt{internal int Process,NativeOwnerBytes,NativeBodyBytes,HistoricalBodyBytes,SourceInvocations,OwnerInvocations;internal uint Revision;internal string Stage;internal bool MetadataOnly;}
    private static readonly Dictionary<Guid,int[]> OwnerNativeCallAudit=new Dictionary<Guid,int[]>();
    private static readonly Dictionary<Guid,ColdSourceReceipt> ColdSourceAudit=new Dictionary<Guid,ColdSourceReceipt>();
#endif
    internal static void ObserveSourceInvoked(Guid operation)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ColdSourceAudit.TryGetValue(operation,out var receipt))receipt.SourceInvocations++;
#endif
    }
    private static void ObserveOwnerInvoked(Guid operation)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ColdSourceAudit.TryGetValue(operation,out var receipt))receipt.OwnerInvocations++;
        ObserveOwnerNativeCall(operation,1);
#endif
    }
    private static void ObserveOwnerNativeCall(Guid operation,int index)
    {
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")return;
        if(!OwnerNativeCallAudit.TryGetValue(operation,out var counts)){if(OwnerNativeCallAudit.Count>=64)return;OwnerNativeCallAudit.Add(operation,counts=new int[2]);}counts[index]++;
#endif
    }
}
