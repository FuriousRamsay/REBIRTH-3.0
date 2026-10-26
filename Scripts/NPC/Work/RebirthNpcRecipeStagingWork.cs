using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Preparation only: one real ingredient enters the worker's native bag at an
// explicitly assigned stationary workstation. No queue, fuel, recipe effect,
// output or cooking-completion permission follows from this receipt.
internal static class RebirthNpcRecipeStagingWork
{
    internal const string DefinitionId="rebirth.work.recipe-ingredient-stage";
    internal sealed class Target
    {
        internal Recipe Recipe;internal RebirthNpcPhysicalResourceEndpoint Source;
        internal int SourceSlot;internal Vector3i Station;internal string Incarnation;
    }
    internal static void Register()=>RebirthNpcWorkDefinitionRegistry.Register(
        new RebirthNpcWorkDefinition(DefinitionId,RebirthNpcWorkKind.Crafting,
            RebirthNpcWorkCapability.Crafting,270,1,TimeSpan.FromSeconds(60),false),true);
    internal static bool IsDefinition(string id)=>id==DefinitionId;
    internal static RebirthNpcWorkAssignmentResult Assign(string session,string recipeName,Vector3i station,
        Vector3i source,int sourceSlot,out RebirthNpcWorkAssignment assignment,out string detail)
    {
        assignment=null;detail="Known recipe, owned stationary workstation and adjacent ingredient storage are required.";
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var context)!=RebirthNpcInteractionResult.Allowed||
            context==null||!RebirthHumanRecipeReading.Knows(context.NpcId,recipeName))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=false||world.GetTileEntity(station) is not TileEntityWorkstation tile||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,tile,out string incarnation))return RebirthNpcWorkAssignmentResult.InvalidRequest;
        string key="recipe-stage:"+recipeName+":S@"+source.x.ToString(CultureInfo.InvariantCulture)+","+
            source.y.ToString(CultureInfo.InvariantCulture)+","+source.z.ToString(CultureInfo.InvariantCulture)+":"+
            sourceSlot.ToString(CultureInfo.InvariantCulture)+":"+incarnation;
        if(!Parse(DefinitionId,key,true,station.ToVector3(),out var target)||
            !StationCurrent(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId)))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(new RebirthNpcWorkAssignmentRequest{SessionKey=session,
            DefinitionId=DefinitionId,TargetKey=key,TargetPosition=station.ToVector3(),HasTargetPosition=true,
            ExpectedRuntimeRevision=context.RuntimeRevision},out assignment,out detail);
    }
    // recipe-stage:<nativeRecipeName>:S@x,y,z:<sourceSlot>:<stationIncarnation>
    internal static bool Parse(string definition,string key,bool hasPosition,Vector3 position,out Target target)
    {
        target=null;
        try
        {
            if(!IsDefinition(definition)||!hasPosition||key==null||key.Length>512)return false;
            var parts=key.Split(':');
            if(parts.Length!=5||parts[0]!="recipe-stage"||parts[1].Length<1||parts[1].Length>256||
                !int.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out int slot)||slot<0||slot>255||
                !Guid.TryParseExact(parts[4],"N",out var incarnation)||incarnation==Guid.Empty)return false;
            var recipe=CraftingManager.GetRecipe(parts[1]);
            var source=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(parts[2],out _);
            if(recipe==null||recipe.IsScrap||recipe.ingredients==null||recipe.ingredients.Count<1||
                source?.Kind!=RemoteResourceSourceKind.StaticContainer)return false;
            var station=new Vector3i(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y),Mathf.FloorToInt(position.z));
            if(position!=station.ToVector3()||source.Position==station||
                (source.Position.ToVector3()-position).sqrMagnitude>
                    RebirthCompanionService.StorageDistance*RebirthCompanionService.StorageDistance)return false;
            target=new Target{Recipe=recipe,Source=source,SourceSlot=slot,Station=station,Incarnation=parts[4]};return true;
        }
        catch{return false;}
    }
    internal static bool Allows(RebirthNpcWorkAssignment a)=>a!=null&&Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out _);
    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)=>a!=null&&Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out _);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent intent)=>intent!=null&&intent.Path=="concrete"&&
        intent.PrimaryId!=0&&intent.SecondaryId!=0&&intent.Actor is EntityRebirthHumanoidNPC&&intent.MatchesOriginal()&&
        intent.ExecutorId=="rebirth.work.crafting-workstation"&&
        RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var assignment)&&Allows(assignment)&&
        assignment.NpcId==intent.StableId&&RebirthNpcWorkReservationService.TryGetForAssignment(intent.PrimaryId,out var reservation)&&
        reservation.ReservationId==intent.SecondaryId;
    internal static string TargetId(RebirthNpcWorkAssignment a)=>"block:"+
        Mathf.FloorToInt(a.TargetPosition.x).ToString(CultureInfo.InvariantCulture)+":"+
        Mathf.FloorToInt(a.TargetPosition.y).ToString(CultureInfo.InvariantCulture)+":"+
        Mathf.FloorToInt(a.TargetPosition.z).ToString(CultureInfo.InvariantCulture);
    internal static bool Ingredient(Target target,string frame)
    {
        try{var stack=RebirthHumanEquipment.Decode(frame);return stack?.itemValue?.ItemClass!=null&&stack.count==1&&
            target.Recipe.ingredients.Any(item=>item!=null&&item.count>0&&item.itemValue?.type==stack.itemValue.type);}
        catch{return false;}
    }
    internal static bool StationCurrent(Target target,EntityPlayer owner)
    {
        var world=GameManager.Instance?.World;
        return world?.IsRemote()==false&&owner!=null&&!owner.IsDead()&&ReferenceEquals(owner.world,world)&&
            ReferenceEquals(world.GetEntity(owner.entityId),owner)&&
            world.GetTileEntity(target.Station) is TileEntityWorkstation station&&station is not RebirthCamperStationState&&
            !station.IsUserAccessing()&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var incarnation)&&
            incarnation==target.Incarnation&&new WorkstationOutputResourceSource(world,target.Station).IsAuthorized(owner,true,out _);
    }
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc)
    {
        if(!Allows(a)||!Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out var target)||
            !RebirthHumanRecipeReading.Knows(a.NpcId,target.Recipe.GetName())||
            !StationCurrent(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId)))return false;
        var operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
        if(RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,operation,out var original))return Matches(a,target,original);
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        for(int bag=0;bag<npc.bag.SlotCount;bag++)
            if((npc.bag.ItemGrid[bag]==null||npc.bag.ItemGrid[bag].IsEmpty())&&
                target.Source.TryProbeSourceSlotForNpc(owner,npc,target.SourceSlot,out var seen)&&
                Ingredient(target,RebirthHumanEquipment.Frame(new ItemStack(seen.itemValue.Clone(),1))))return true;
        return false;
    }
    internal static bool Matches(RebirthNpcWorkAssignment a,Target target,RebirthNpcWorkNativeTransferOriginal.Record row)=>
        row!=null&&row.Npc==a.NpcId&&row.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(a.ActorId)&&
        row.Operation==RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId)&&
        row.Source==RemoteResourceIdentity.Static(target.Source.Position)&&row.SourceSlot==target.SourceSlot&&Ingredient(target,row.Frame);
    private static XElement Receipt(RebirthNpcWorkAssignment a,RebirthNpcWorkNativeTransferOriginal.Record row,bool legacy=false)
    {
        var receipt=new XElement("recipeIngredientStaged",new XAttribute("version",legacy?1:2),new XAttribute("assignment",a.AssignmentId),
            new XAttribute("operation",row.Operation.ToString("N")),new XAttribute("target",a.TargetKey),
            new XAttribute("binding",row.Binding),new XAttribute("sourceProof",row.SourceProof));
        if(!legacy)
        {
            receipt.SetAttributeValue("world",row.World);receipt.SetAttributeValue("owner",a.ActorId);
            receipt.SetAttributeValue("startedTicks",a.CreatedUtcTicks);
            receipt.SetAttributeValue("stationX",Mathf.FloorToInt(a.TargetPosition.x));
            receipt.SetAttributeValue("stationY",Mathf.FloorToInt(a.TargetPosition.y));
            receipt.SetAttributeValue("stationZ",Mathf.FloorToInt(a.TargetPosition.z));
        }
        return receipt;
    }
    private static XElement SavedReceipt(RebirthNpcStableId npc,Guid operation)
    {
        if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return null;
        var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
        return equipment?.Elements("recipeIngredientStaged").SingleOrDefault(x=>(string)x.Attribute("operation")==operation.ToString("N"));
    }
    internal static bool SavedStage(RebirthNpcWorkAssignment a,RebirthNpcWorkNativeTransferOriginal.Record row)
    {
        try
        {
            if(row?.Phase!=4)return false;
            var receipt=SavedReceipt(a.NpcId,row.Operation);int? version=(int?)receipt?.Attribute("version");
            return receipt!=null&&(version==1||version==2)&&XNode.DeepEquals(receipt,Receipt(a,row,version==1));
        }
        catch{return false;}
    }
    internal static bool SavedStationStage(RebirthNpcWorkAssignment a,RebirthNpcWorkNativeTransferOriginal.Record row)
    {
        try{return row?.Phase==4&&XNode.DeepEquals(SavedReceipt(a.NpcId,row.Operation),Receipt(a,row));}
        catch{return false;}
    }
    // Detached paid-source description only. This does not restore callbacks,
    // create a job, issue a reservation or adopt an old embodiment.
    internal static bool TryDescribeSavedStationStage(RebirthNpcStableId npc,string owner,ulong assignment,
        out RebirthNpcWorkAssignment description,out Target target,out RebirthNpcWorkNativeTransferOriginal.Record source)
    {
        description=null;target=null;source=null;
        try
        {
            Guid operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignment);
            if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(npc,operation,out source)||source.Phase!=4||
                source.Owner!=RebirthStablePlayerIdentity.ComputeStorageKey(owner)||source.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance?.World))return false;
            var receipt=SavedReceipt(npc,operation);
            if((int?)receipt?.Attribute("version")!=2||(ulong?)receipt.Attribute("assignment")!=assignment||
                !string.Equals((string)receipt.Attribute("owner"),owner,StringComparison.Ordinal)||
                ((long?)receipt.Attribute("startedTicks")??0)<=0)return false;
            var position=new Vector3((int)receipt.Attribute("stationX"),(int)receipt.Attribute("stationY"),(int)receipt.Attribute("stationZ"));
            var saved=new RebirthNpcWorkAssignment{AssignmentId=assignment,NpcId=npc,ActorId=owner,DefinitionId=DefinitionId,
                TargetKey=(string)receipt.Attribute("target"),HasTargetPosition=true,TargetPosition=position,CreatedUtcTicks=(long)receipt.Attribute("startedTicks")};
            if(!Parse(saved.DefinitionId,saved.TargetKey,true,position,out target)||!Matches(saved,target,source)||!SavedStationStage(saved,source))return false;
            description=saved;return true;
        }
        catch{description=null;target=null;source=null;return false;}
    }
    internal static bool SaveStage(RebirthNpcWorkAssignment a,Target target,EntityRebirthHumanoidNPC npc,RebirthNpcWorkNativeTransferOriginal.Record row)
    {
        try
        {
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(intent==null||!Allows(intent)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!ReferenceEquals(intent.Actor,npc)||
            intent.PrimaryId!=a.AssignmentId||!Matches(a,target,row)||row.Phase!=4||
            !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,row.Operation,out var saved)||
            saved.Phase!=4||saved.Binding!=row.Binding||saved.SourceProof!=row.SourceProof||
            !string.Equals(npc.RebirthRuntimeState.OwnerId,a.ActorId,StringComparison.OrdinalIgnoreCase)||
            !StationCurrent(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId))||
            !RebirthHumanRecipeReading.Knows(a.NpcId,target.Recipe.GetName())||
            (npc.position-a.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared||
            row.BagSlot<0||row.BagSlot>=npc.bag.SlotCount||RebirthHumanEquipment.Frame(npc.bag.ItemGrid[row.BagSlot])!=row.Frame)return false;
        var equipment=RebirthHumanEquipment.Read(npc);var expected=Receipt(a,row);
        var previous=equipment.Elements("recipeIngredientStaged").SingleOrDefault(x=>(string)x.Attribute("operation")==row.Operation.ToString("N"));
        if(previous!=null&&!XNode.DeepEquals(previous,expected))
        {
            // A legacy receipt may acquire station/time fields only at this
            // genuine current EAI arrival with the exact positively saved item.
            if((int?)previous.Attribute("version")!=1||!XNode.DeepEquals(previous,Receipt(a,row,true)))return false;
            previous.ReplaceWith(expected);
        }
        if(previous==null)equipment.Add(expected);
        RebirthHumanEquipment.Save(npc,equipment);return SavedStage(a,row);
        }
        catch{return false;}
    }
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        return outcome!=null&&IsDefinition(outcome.DefinitionId)&&outcome.State==RebirthNpcWorkOutcomeState.Completed&&
            RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var a)&&Allows(a)&&a.NpcId==outcome.NpcId&&
            Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out var target)&&
            RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId),out var row)&&
            Matches(a,target,row)&&SavedStage(a,row)&&outcome.OutcomeId==row.Operation&&outcome.TargetId==TargetId(a)&&
            outcome.StartedUtcTicks==a.CreatedUtcTicks&&outcome.DurableWitness=="recipe-ingredient-staged:"+row.Binding&&
            outcome.Detail=="One recipe ingredient staged in worker custody; recipe execution remains pending."&&
            outcome.Produced==null&&outcome.Consumed==null;
    }
}

public sealed class RebirthNpcRecipeStagingWorkAdapter:IRebirthNpcCraftingWorkAdapter
{
    public string AdapterId=>"rebirth.recipe.physical-ingredient-stage";
    public int Priority=>790;
    public bool CanHandle(RebirthNpcConcreteWorkContext c)=>RebirthNpcRecipeStagingWork.Allows(c?.Assignment);
    private static RebirthNpcWorkMutationResult Hold(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=RebirthNpcWorkFailureCategory.MissingInput,Detail=detail};
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext c)=>
        CanHandle(c)?RebirthNpcWorkMutationResult.Continue(0f,"Recipe ingredient staging accepted; worker must reach the station."):
        Hold("Exact saved recipe staging assignment is unavailable.");
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext c)
    {
        if(!CanHandle(c)||!RebirthNpcRecipeStagingWork.Parse(c.Assignment.DefinitionId,c.Assignment.TargetKey,c.Assignment.HasTargetPosition,c.Assignment.TargetPosition,out var target)||
            RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcRecipeStagingWork.Allows(intent)||
            intent.Actor is not EntityRebirthHumanoidNPC npc||npc.bag?.ItemGrid==null||
            npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy)
            return Hold("Original EAI recipe staging custody is unavailable.");
        var a=c.Assignment;var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        if(!RebirthNpcRecipeStagingWork.StationCurrent(target,owner)||!RebirthHumanRecipeReading.Knows(a.NpcId,target.Recipe.GetName())||
            (npc.position-a.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)
            return Hold("Known recipe, assigned station access or physical arrival is unavailable.");
        if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,c.OperationId,out var row))
        {
            for(int bag=0;bag<npc.bag.SlotCount;bag++)
            {
                if(npc.bag.ItemGrid[bag]!=null&&!npc.bag.ItemGrid[bag].IsEmpty())continue;
                if(!target.Source.TryObserveExactStackForNpc(owner,npc,target.SourceSlot,bag,out var seen)||
                    !RebirthNpcRecipeStagingWork.Ingredient(target,RebirthHumanEquipment.Frame(new ItemStack(seen.Stack.itemValue.Clone(),1))))continue;
                return RebirthNpcWorkNativeTransfer.TryStart(owner,npc,target.Source,target.SourceSlot,bag,c.OperationId,out _)
                    ?RebirthNpcWorkMutationResult.Continue(0f,"Exact recipe ingredient transfer prepared."):
                    Hold("Exact ingredient custody is awaiting its saved original.");
            }
            return Hold("Selected recipe ingredient, source access or empty worker bag cell is unavailable.");
        }
        if(!RebirthNpcRecipeStagingWork.Matches(a,target,row)||!RebirthNpcWorkNativeTransfer.TryAdvance(owner,npc,c.OperationId)||
            !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,c.OperationId,out row)||row.Phase!=4)
            return Hold("Original physical ingredient debit and native bag credit are settling.");
        if(!RebirthNpcRecipeStagingWork.SavedStationStage(a,row)&&!RebirthNpcRecipeStagingWork.SaveStage(a,target,npc,row))
            return Hold("Physical station arrival and saved ingredient staging receipt are pending.");
        var outcome=new RebirthNpcWorkOutcomeRecord{OutcomeId=row.Operation,AssignmentId=a.AssignmentId,NpcId=a.NpcId,
            DefinitionId=RebirthNpcRecipeStagingWork.DefinitionId,TargetId=RebirthNpcRecipeStagingWork.TargetId(a),
            DurableWitness="recipe-ingredient-staged:"+row.Binding,StartedUtcTicks=a.CreatedUtcTicks,CompletedUtcTicks=DateTime.UtcNow.Ticks,
            State=RebirthNpcWorkOutcomeState.Completed,FailureCategory=RebirthNpcWorkFailureCategory.None,
            Detail="One recipe ingredient staged in worker custody; recipe execution remains pending."};
        return RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome)?new RebirthNpcWorkMutationResult{
            Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,Detail=outcome.Detail}:
            Hold("Saved ingredient staging is awaiting its durable preparation outcome.");
    }
    public void CancelOperation(RebirthNpcConcreteWorkContext c,string reason){}
}
