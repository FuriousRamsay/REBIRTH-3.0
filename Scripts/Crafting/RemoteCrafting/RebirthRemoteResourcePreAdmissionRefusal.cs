using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

// A purpose-specific admission tombstone, never a source outcome or an owner
// inventory receipt. Published Prepared already forbids admission; only the
// exact fully published body may be acknowledged to its authenticated owner.
internal static class RebirthRemoteResourcePreAdmissionRefusal
{
    internal static void Install(Harmony harmony)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRemoteResourcePersonalCloseRefusal));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRemoteResourceBagCloseRefusal));
    }
    internal static void Close(XUi ui)
    {RebirthRemoteResourceRepairProtocol.CancelUnadmittedForUi(ui);RebirthRemoteResourceCraftProtocol.CancelUnadmittedForUi(ui);}
    private static string Root=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourcePreAdmissionRefusals");
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Root);
    private static bool KnownAbsent(string directory,Guid operation)
    {
        if(!Directory.Exists(directory))return true;
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
        string prefix=operation.ToString("N");
        if(Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any())return false;
        foreach(string name in new[]{"completed","failedCandidates"})
        {
            string child=Path.Combine(directory,name);if(!Directory.Exists(child))continue;
            if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(child,prefix+"*").Any())return false;
        }
        return true;
    }
    private static string ClientDirectory(string name)=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);
    private static bool NoClientEffects(RebirthRemoteResourceClientOriginal.Intent original)=>
        KnownAbsent(ClientDirectory("RemoteResourceClientActions"),original.Operation)&&KnownAbsent(ClientDirectory("RemoteResourceClientSources"),original.Operation)&&
        KnownAbsent(ClientDirectory("RemoteCraftOwnerOriginals"),original.Operation)&&KnownAbsent(ClientDirectory("RemoteBlockOwnerOriginals"),original.Operation)&&
        KnownAbsent(ClientDirectory("RemoteBlockOwnerCommands"),original.Operation)&&
        (!RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)||RebirthRemoteResourceFetchedStationOriginal.HasNoFetchedClientEffects(original));
    private static bool IsBlock(RebirthRemoteResourceClientOriginal.Intent original)=>original!=null&&
        (original.Kind==RemoteResourceClientOperation.BlockRepair||original.Kind==RemoteResourceClientOperation.BlockUpgrade);
    private static bool BlockMetadataOnly(RebirthRemoteResourceClientOriginal.Intent original,Guid source)
    {
        if(!IsBlock(original))return false;
        if(!KnownAbsent(ClientDirectory("RemoteBlockAcceptedCommands"),original.Operation)||!KnownAbsent(ClientDirectory("RemoteBlockAcceptedPending"),original.Operation)||!KnownAbsent(ClientDirectory("RemoteBlockHostAttempts"),original.Operation)||
            !KnownAbsent(ClientDirectory("RemoteBlockHostDeclined"),original.Operation)||!KnownAbsent(ClientDirectory("RemoteBlockHostSaved"),original.Operation)||
            !KnownAbsent(ClientDirectory("RemoteBlockFootprintPlans"),original.Operation)||!KnownAbsent(ClientDirectory("RemoteBlockFootprintBirths"),original.Operation))return false;
        bool noOriginal=KnownAbsent(ClientDirectory("RemoteBlockPreSourceAdmission"),original.Operation),noMap=KnownAbsent(ClientDirectory("RemoteBlockSourceMap"),source);
        if(noOriginal&&noMap)return KnownAbsent(ClientDirectory("RemoteBlockNativeOwner"),original.Operation)&&KnownAbsent(ClientDirectory("RemoteBlockTargetAdmission"),original.Operation)&&
            KnownAbsent(ClientDirectory("RemoteBlockTargetSaved"),original.Operation)&&KnownAbsent(ClientDirectory("RemoteBlockTransitionContext"),original.Operation);
        // Exact canonical metadata may exist without any source root. Partial
        // or orphan metadata is not silently treated as no admission.
        return RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)&&retained.Digest==original.Digest&&
            RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(source,out var mapped)&&mapped.Digest==original.Digest&&RebirthRemoteResourceBlockAdmission.TryReadNativeUser(original,out _)&&
            (KnownAbsent(ClientDirectory("RemoteBlockTargetAdmission"),original.Operation)||RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out _,out _,out _))&&
            (KnownAbsent(ClientDirectory("RemoteBlockTransitionContext"),original.Operation)||RebirthRemoteResourceBlockAdmission.TryReadTransitionContext(original,out _))&&
            (KnownAbsent(ClientDirectory("RemoteBlockTargetSaved"),original.Operation)||RebirthRemoteResourceBlockAdmission.HasSavedTarget(original));
    }
    internal static bool ClientRequested(RebirthRemoteResourceClientOriginal.Intent original)
    {try{return original!=null&&!KnownAbsent(ClientDirectory("RemoteResourcePreAdmissionCancel"),original.Operation);}catch{return true;}}
    internal static bool RequestClient(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!NoClientEffects(original))return false;
            byte[] bytes=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));
            var record=new RebirthCamperTransferJournal(ClientDirectory("RemoteResourcePreAdmissionCancel")).Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            return record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
        }
        catch{return false;}
    }
    internal static void SendRequested(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!ClientRequested(original))return;
            var record=new RebirthCamperTransferJournal(ClientDirectory("RemoteResourcePreAdmissionCancel")).Read(original.Operation);
            byte[] exact=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));
            if(record==null||!record.Before.SequenceEqual(exact)||!record.After.SequenceEqual(exact))return;
            var body=new XElement("preAdmissionRefusalRequest",new XAttribute("version",1),original.Copy());
            if(player.world.IsRemote()==false&&original.Kind==RemoteResourceClientOperation.Craft)
            {Request(player,body.ToString(SaveOptions.DisableFormatting));return;}
            var user=GameManager.Instance.GetPersistentLocalPlayer()?.PrimaryId;if(user==null)return;
            NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,user,original,body.ToString(SaveOptions.DisableFormatting));
        }
        catch{}
    }
    internal static void Request(EntityPlayer actor,string text)
    {
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,17*1024*1024);
            if(body.Name!="preAdmissionRefusalRequest"||body.Attributes().Count()!=1||(int?)body.Attribute("version")!=1||body.Elements().Count()!=1||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out var original)||
                !TryRefuse(actor,original,out var proof)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out var world))return;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            var record=Journal().Read(source);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied)return;
            var result=new XElement("preAdmissionRefusalResult",new XAttribute("version",1),new XAttribute("proof",proof),
                RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),8192));
            if(actor is EntityPlayerLocal primary&&actor.world?.IsRemote()==false&&original.Kind==RemoteResourceClientOperation.Craft&&
                ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetPrimaryPlayer(),primary)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))
                Result(primary,original.Operation,result.ToString(SaveOptions.DisableFormatting));
            else NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,result.ToString(SaveOptions.DisableFormatting));
        }
        catch{}
    }
    internal static bool HasClientTerminal(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(original==null||!NoClientEffects(original))return false;
            var record=new RebirthRemoteResourceClientOriginalJournal(ClientDirectory("RemoteResourceNeverAdmittedTerminal")).Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||!record.Before.SequenceEqual(record.After)||record.Before.Length>8192)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),8192);
            return ValidateTerminal(original,body,out _);
        }
        catch{return false;}
    }
    private static bool ValidateTerminal(RebirthRemoteResourceClientOriginal.Intent original,XElement body,out string proof)
    {
        proof=null;
        if(body?.Name!="preAdmissionRefusalResult"||body.Attributes().Count()!=2||(int?)body.Attribute("version")!=1||body.Elements().Count()!=1||body.Nodes().Any(node=>node is not XElement))return false;
        var value=body.Element("fetchNeverAdmitted");
        if(value==null||value.Attributes().Count()!=9||value.Nodes().Any()||(int?)value.Attribute("version")!=1||
            (string)value.Attribute("original")!=original.Operation.ToString("N")||(string)value.Attribute("digest")!=original.Digest||
            checked((byte?)(int?)value.Attribute("kind"))!=(byte)original.Kind||(string)value.Attribute("creation")!=original.Creation||(int?)value.Attribute("deaths")!=original.Deaths||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(GameManager.Instance?.World?.GetPrimaryPlayer(),out var owner))return false;
        string world=(string)value.Attribute("world");if(world?.Length!=64||world.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||(string)value.Attribute("owner")!=owner.StorageKey||
            (string)value.Attribute("source")!=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind).ToString("N"))return false;
        proof=RebirthRemoteResourceDurableJournal.Digest("never-admitted|"+value.ToString(SaveOptions.DisableFormatting));return (string)body.Attribute("proof")==proof;
    }
    internal static void Result(EntityPlayerLocal player,Guid operation,string text)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.TryReadPending(out var originals))return;
            var original=originals.SingleOrDefault(value=>value.Operation==operation);
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!ClientRequested(original)||!NoClientEffects(original))return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,8192);if(!ValidateTerminal(original,body,out _))return;
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));
            var journal=new RebirthRemoteResourceClientOriginalJournal(ClientDirectory("RemoteResourceNeverAdmittedTerminal"));
            var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State==RebirthCamperTransferJournal.Phase.Applied)RebirthRemoteResourceClientOriginal.CompleteNeverAdmitted(player,original);
        }
        catch{}
    }
    internal static bool BlocksAdmission(Guid source)
    {
        try{return source==Guid.Empty||!KnownAbsent(Root,source);}
        catch{return true;}
    }
    internal static bool HasSavedServerTerminal(World world,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||original==null||world.worldState.Guid!=original.World||
                !RemoteResourceWorldIdentity.TryGet(world,out var worldId))return false;
            string owner=RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner);
            Guid source=RebirthRemoteResourceDurableJournal.Operation(worldId,owner,original.Creation,original.Epoch,original.Request,original.Kind);
            var expected=new XElement("fetchNeverAdmitted",new XAttribute("version",1),new XAttribute("world",worldId),new XAttribute("owner",owner),new XAttribute("source",source.ToString("N")),
                new XAttribute("original",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("kind",(byte)original.Kind),new XAttribute("creation",original.Creation),new XAttribute("deaths",original.Deaths));
            var record=Journal().Read(source);byte[] bytes=Encoding.UTF8.GetBytes(expected.ToString(SaveOptions.DisableFormatting));
            return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Operation==source&&record.Vehicle==original.Operation&&record.OwnerCreation==original.Creation&&record.Revision==0&&
                record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes)&&RebirthRemoteResourceDurableJournal.IsKnownAbsentIncludingCandidates(owner,source)&&
                (!IsBlock(original)||BlockMetadataOnly(original,source))&&
                (!RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)||RebirthRemoteResourceFetchedStationOriginal.IsFetchedMetadataOnly(original,source,worldId,owner));
        }
        catch{return false;}
    }
    internal static bool TryRefuse(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||original==null||
                (original.Kind!=RemoteResourceClientOperation.ItemRepair&&original.Kind!=RemoteResourceClientOperation.Craft&&!IsBlock(original))||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                actor.Died!=original.Deaths||actor.world.worldState.Guid!=original.World||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.CanonicalId!=original.Owner||
                !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            byte[] bytes=Encoding.UTF8.GetBytes(new XElement("fetchNeverAdmitted",new XAttribute("version",1),new XAttribute("world",world),
                new XAttribute("owner",owner.StorageKey),new XAttribute("source",source.ToString("N")),new XAttribute("original",original.Operation.ToString("N")),
                new XAttribute("digest",original.Digest),new XAttribute("kind",(byte)original.Kind),new XAttribute("creation",original.Creation),new XAttribute("deaths",original.Deaths)).ToString(SaveOptions.DisableFormatting));
            if(bytes.Length>8192)return false;
            if(Directory.Exists(Root)&&(File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0)return false;
            var journal=Journal();var record=journal.Read(source);
            if(record==null)
            {
                string admission=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",original.Kind==RemoteResourceClientOperation.Craft?"RemoteCraftSourceAdmission":"RemoteRepairSourceAdmission");
                if(!KnownAbsent(Root,source)||!(IsBlock(original)?BlockMetadataOnly(original,source):RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)?
                    RebirthRemoteResourceFetchedStationOriginal.IsFetchedMetadataOnly(original,source,world,owner.StorageKey):KnownAbsent(admission,source))||
                    !RebirthRemoteResourceDurableJournal.IsKnownAbsentIncludingCandidates(owner.StorageKey,source))return false;
                record=journal.Prepare(source,original.Operation,original.Creation,0,bytes,bytes);
            }
            if(record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(source,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(source,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            proof=RebirthRemoteResourceDurableJournal.Digest("never-admitted|"+Encoding.UTF8.GetString(bytes));return true;
        }
        catch{return false;}
    }
}

[HarmonyPatch(typeof(XUiC_RebirthPersonalCrafting),nameof(XUiC_RebirthPersonalCrafting.OnClose))]
internal static class RebirthRemoteResourcePersonalCloseRefusal
{private static void Prefix(XUiC_RebirthPersonalCrafting __instance)=>RebirthRemoteResourcePreAdmissionRefusal.Close(__instance?.xui);}
[HarmonyPatch(typeof(XUiC_BagStorageWindowGroup),nameof(XUiC_BagStorageWindowGroup.OnClose))]
internal static class RebirthRemoteResourceBagCloseRefusal
{private static void Prefix(XUiC_BagStorageWindowGroup __instance)=>RebirthRemoteResourcePreAdmissionRefusal.Close(__instance?.xui);}
