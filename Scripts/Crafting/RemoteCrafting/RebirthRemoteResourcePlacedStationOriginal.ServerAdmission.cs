using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Immutable authenticated request metadata only. Applied here means the input
// record is published, not that any owner, source, or native station was paid.
internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private const string AdmissionDomain="NativePlacedSourceAdmissions";
    // Existing rcj framing plus two bounded original images; checked before
    // the shared journal may materialize phase/archive comparison buffers.
    private const long MaximumAdmissionPhaseBytes=4+32+5+4*512+8+1+8+2L*RebirthRemoteResourceClientOriginal.MaximumOriginalBytes+32;
    private static RebirthRemoteResourceClientOriginalJournal Admissions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",AdmissionDomain));
    private static bool ServerScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&
        actor?.world?.IsRemote()==false&&AllowsReviewedPlacedMode&&actor.IsSpawned()&&!actor.IsDead()&&
        ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&IsCandidate(original)&&
        actor.world.worldState.Guid==original.World&&actor.Died==original.Deaths&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&
        owner.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation);
    private static bool KnownAdmissionFiles(Guid operation)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",AdmissionDomain),prefix=operation.ToString("N");
        for(var ancestor=new DirectoryInfo(Path.GetFullPath(root));ancestor!=null;ancestor=ancestor.Parent)
            if(ancestor.Exists&&(ancestor.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        if(!Directory.Exists(root))return true;
        int entries=0;
        foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix),Path.Combine(root,"failedCandidates")})
        {
            if(!Directory.Exists(folder))continue;
            if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return false;
            foreach(string entry in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
            {
                if(++entries>32)return false;
                if(folder==Path.Combine(root,"completed")&&entry==Path.Combine(folder,prefix)&&Directory.Exists(entry))continue;
                if(folder==Path.Combine(root,"failedCandidates")||(File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0||Directory.Exists(entry)||
                    !new[]{prefix+".0.rcj",prefix+".1.rcj",prefix+".2.rcj"}.Contains(Path.GetFileName(entry))||new FileInfo(entry).Length>MaximumAdmissionPhaseBytes)return false;
            }
        }
        return true;
    }
    internal static bool IsMappedRoot(RebirthRemoteResourceDurableJournal.Record root)
    {
        if(root==null)return false;
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",AdmissionDomain),prefix=root.Operation.ToString("N");
            if(!Directory.Exists(directory))return false;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return true;
            return Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any()||Directory.Exists(Path.Combine(directory,"completed",prefix));
        }
        catch{return true;}
    }
    internal static bool TryReadAdmission(RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            if(root==null||!KnownAdmissionFiles(root.Operation))return false;
            var record=Admissions().Read(root.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.OwnerCreation!=root.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(new UTF8Encoding(false,true).GetString(record.Before),out var value)||
                !IsCandidate(value)||record.Vehicle!=value.Operation||value.Creation!=root.Creation||value.Deaths!=(int)root.Copy().Attribute("deaths")||
                value.Kind!=(RemoteResourceClientOperation)checked((byte)(int)root.Copy().Attribute("kind"))||
                RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,value.Creation,value.Epoch,value.Request,value.Kind)!=root.Operation||
                !ValidateInput(value,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _))return false;
            original=value;return true;
        }
        catch{return false;}
    }
    internal static bool Admit(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!ServerScope(actor,original)||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||
                !ValidateInput(original,out var position,out uint block,out string window,out var authored,out _,out _,out _,out int belt,out var payment,out bool turnsOn,out var input))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(source)||!KnownAdmissionFiles(source))return false;
            byte[] body=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));var journal=Admissions();var retained=journal.Read(source);
            if(retained==null)
            {
                if(!RebirthRemoteResourcePlacedStationScope.TryResolveLayout(actor,position,original.Creation,out var station,out string actualWindow,out var actualPayment,out bool actualTurnsOn)||
                    !HasActualAcceptedUpload(actor,original,source,station)||
                    actualWindow!=window||actualPayment!=payment||actualTurnsOn!=turnsOn||station.blockValue.rawData!=block||
                    RebirthRemoteResourceSourceCustody.BlocksStation(station)||RebirthCookingRemoteAdmissionBackend.Held(station)||
                    !RebirthRemoteResourcePlacedStationScope.AllowsRecipe(actor,station,authored)||
                    !CraftingManager.GetAllRecipes().Any(recipe=>recipe!=null&&recipe.craftingToolType==authored.craftingToolType&&
                        recipe.materialBasedRecipe==authored.materialBasedRecipe&&recipe.tags.ToString()==authored.tags.ToString()&&
                        RebirthRecipeOutputs.DefinitionIdentity(recipe)==original.Copy().Element("action").Element("outputDefinition").Value&&
                        RebirthRecipeOutputs.TryCaptureFrozenOutputs(recipe,out string frozen)&&frozen==original.Copy().Element("action").Element("outputImage").Value&&
                        RebirthRemoteResourceOwnerAction.RecipeImage(recipe)==original.Copy().Element("action").Element("authored").Value)||
                    belt!=actor.inventory.Length||input.Length!=station.Input?.Length||input.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,station.Input[index])).Any())return false;
                var queue=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Copy().Element("action").Element("queue").Value,RebirthRemoteResourcePlacedStationQueue.MaximumBytes);
                if(queue.Element("native")?.Value!=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue)||
                    !RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                    !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(before)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f)return false;
                var required=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
                var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
                if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory?!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,required,remote,belt):
                    !RebirthRemoteResourcePlacedStationInputPlan.TryCreate(input,required,remote,out _))return false;
                if(!ServerScope(actor,original)||!RebirthRemoteResourcePlacedStationIdentity.EnsureFresh(actor,original,source,station))return false;
                retained=journal.Prepare(source,original.Operation,original.Creation,0,body,body);
            }
            if(retained.Vehicle!=original.Operation||retained.OwnerCreation!=original.Creation||retained.Revision!=0||!retained.Before.SequenceEqual(body)||!retained.After.SequenceEqual(body))return false;
            if(retained.State==RebirthCamperTransferJournal.Phase.Prepared)retained=journal.Advance(source,retained.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(retained.State==RebirthCamperTransferJournal.Phase.Uncertain)retained=journal.Advance(source,retained.State,RebirthCamperTransferJournal.Phase.Applied);
            if(retained.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            RetireAdmittedUpload(source);return true;
        }
        catch{return false;}
    }
}
