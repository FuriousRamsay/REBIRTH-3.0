using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using UnityEngine;

// Managed cooking uses the existing owner-native payment boundary. The request
// selects physical inputs; only the server constructs recipe/output authority.
internal static class RebirthCookingRemoteAdmission
{
    internal const string Pending="Cooking admission pending; retain the original request.";
    internal const string Accepted="Cooking batch admitted.";
    private const string PayloadKey="rebirth.cooking.admission.inputs";
    private static ItemValue DecodeItem(string image)
    {if(!RebirthNativeItemCodec.TryDecode(image,out var value)||value==null)throw new InvalidDataException("Canonical cooking item unavailable.");return value;}
    internal const string PendingKey="rebirth.cooking.queue.admissionPending";
    internal static string Receipt(Guid operation)=>"rbCookingAdmission_"+operation.ToString("N");
    private sealed class ClientOriginal
    {
        internal EntityPlayerLocal Owner;internal World World;
        internal string Creation,Location;internal Guid Operation;
        internal ItemValue Carrier;internal ItemStack[] After;internal Func<ItemStack[]> Read;
        internal Action<int,ItemStack> Write;internal float Next;internal bool Waiting;
        internal RebirthCookingOwnerDelta.Plan ReturnPlan;internal Guid ExpectedReturn;internal ItemStack[] ReturnBefore,ReturnAfter;
    }
    private static readonly List<ClientOriginal> Clients=new List<ClientOriginal>();
    internal static int HeldBy(EntityPlayer actor)=>Clients.Count(original=>ReferenceEquals(original.Owner,actor));
    internal static Newtonsoft.Json.Linq.JObject CaptureClient(EntityPlayer actor)
    {
        var original=Clients.FirstOrDefault(value=>ReferenceEquals(value.Owner,actor)&&ReferenceEquals(value.World,GameManager.Instance?.World));
        var workspace=XUiC_RebirthCookingWorkspace.ActiveInstance;
        var result=new Newtonsoft.Json.Linq.JObject{["held"]=HeldBy(actor),["token"]=original?.Operation.ToString("N"),
            ["phase"]=original==null?0f:actor.Buffs.GetCustomVar(Receipt(original.Operation)),["accepted"]=original!=null,["evidenceOnly"]=false,
            ["custody"]=RebirthCookingGridCustody.CaptureAuditState(actor),["cancellation"]=RebirthCookingRemoteCancellation.CaptureAuditState(actor)};
        if(actor is EntityPlayerLocal&&ReferenceEquals(actor.world?.GetPrimaryPlayer(),actor)&&workspace!=null)result["grid"]=workspace.CaptureAdmissionGrid();
        return result;
    }
    internal static bool IsHeld(XUiC_RebirthCookingWorkspace workspace)=>workspace?.HasPendingIngredientReturn==true||RebirthCookingGridCustody.IsHeld(workspace);
    internal static void CompleteUnpaidAdoption(EntityPlayerLocal owner,Guid parent)
    {Clients.RemoveAll(original=>original.Operation==parent&&ReferenceEquals(original.Owner,owner)&&ReferenceEquals(original.World,owner.world));}
    internal static bool TryOperation(ItemValue carrier,out Guid operation)
    {
        operation=Guid.Empty;
        try{return carrier!=null&&carrier.TryGetMetadata(PayloadKey,out string image)&&image.Length<=4*1024*1024&&
            Guid.TryParseExact((string)ReadProtocolXml(image,4*1024*1024).Attribute("operation"),"N",out operation)&&operation!=Guid.Empty;}catch{return false;}
    }
    internal static XElement ReadProtocolXml(string image,int maximum)
    {
        if(image==null||image.Length>maximum)throw new InvalidDataException();
        var settings=new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum};
        using(var text=new StringReader(image))using(var reader=System.Xml.XmlReader.Create(text,settings))return XDocument.Load(reader).Root??throw new InvalidDataException();
    }
    internal static void TickClients()
    {
        if(Clients.Count==0)return;
        foreach(var original in Clients.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,original.World)||!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Owner)||
                !RebirthSurvivorRequestScope.Matches(original.Creation,RebirthCamperCharacterAccess.CurrentCreation(original.Owner))||
                original.Waiting||Time.realtimeSinceStartup<original.Next)continue;
            RebirthCookingGridCustody.CompleteExternal(original.Operation);
            if(!RebirthCookingGridCustody.Adopting(original.Operation,original.Owner))TickReturn(original);
        }
    }
    private static void TickReturn(ClientOriginal original)
    {
        original.Waiting=true;original.Next=Time.realtimeSinceStartup+1f;
        try
        {
        if(original.World.IsRemote())
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;if(manager==null){original.Waiting=false;return;}
            manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(original.Owner),false);
        }
        else GameManager.Instance.SaveLocalPlayerData();
        RebirthCookingSessionService.Request(original.Owner,"returnRemoteCookingGrid",recipe:(original.ReturnPlan?.Operation??original.ExpectedReturn)==Guid.Empty?"":(original.ReturnPlan?.Operation??original.ExpectedReturn).ToString("N"),book:original.Creation,magazine:original.Location,item:original.Carrier,reply:response=>
        {
            original.Waiting=false;
            if(!Clients.Contains(original)||!ReferenceEquals(GameManager.Instance?.World,original.World)||!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Owner)||
                RebirthCamperCharacterAccess.CurrentCreation(original.Owner)!=original.Creation)return;
            try
            {
                bool returned=response.StartsWith("returned:",StringComparison.Ordinal),retry=response.StartsWith("retry-return:",StringComparison.Ordinal);
                int phase=0;string payload;
                if(returned)payload=response.Substring(9);else if(retry)payload=response.Substring(13);
                else if(response.StartsWith("return:0:",StringComparison.Ordinal)||response.StartsWith("return:1:",StringComparison.Ordinal))
                {phase=response[7]=='1'?1:0;payload=response.Substring(9);}
                else return;
                if(payload.Length>6*1024*1024)return;
                var plan=RebirthCookingOwnerDelta.Decode(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                if(!ThreadManager.IsMainThread()||original.Owner.IsDead()||!ReferenceEquals(original.Owner.world,original.World)||
                    !ReferenceEquals(original.World.GetEntity(original.Owner.entityId),original.Owner)||plan.World!=original.World.worldState.Guid||
                    plan.Creation!=original.Creation||plan.Deaths!=original.Owner.Died||
                    !RebirthStablePlayerIdentity.TryResolveServerEntity(original.Owner,out var returnOwner)||returnOwner.CanonicalId!=plan.Owner)return;
                var expected=original.ReturnPlan?.Operation??original.ExpectedReturn;
                if(expected!=Guid.Empty&&expected!=plan.Operation)return;
                if(retry)
                {
                    if(RebirthCookingOwnerDelta.AbandonRejectedWithoutEffect(original.Owner,plan)&&RebirthCookingGridCustody.RejectUnappliedExternal(plan.Operation,original.Owner))
                    {original.ReturnPlan=null;original.ExpectedReturn=Guid.Empty;original.ReturnBefore=null;original.ReturnAfter=null;}
                    return;
                }
                if(original.ReturnPlan!=null&&original.ReturnPlan.Operation!=plan.Operation)return;
                original.ReturnPlan=plan;original.ExpectedReturn=plan.Operation;
                if(original.ReturnBefore==null&&RebirthCookingGridCustody.TryExternalGrid(null,original.Owner,plan.Operation,out var originalGridBefore,out var originalGridAfter))
                {original.ReturnBefore=originalGridBefore;original.ReturnAfter=originalGridAfter;}
                if(original.ReturnBefore==null)
                {
                    var before=original.Read();var after=ItemStack.Clone(before);
                    if(before.Any(item=>!item.IsEmpty()))
                    {
                        foreach(var owed in original.After.Where(item=>!item.IsEmpty()))
                        {
                            long needed=owed.count;string image=RebirthNativeItemCodec.Encode(owed.itemValue);
                            for(int i=0;i<after.Length&&needed>0;i++)
                            {
                                if(after[i].IsEmpty()||RebirthNativeItemCodec.Encode(after[i].itemValue)!=image)continue;
                                int take=(int)Math.Min(needed,after[i].count);after[i].count-=take;needed-=take;if(after[i].count==0)after[i]=ItemStack.Empty.Clone();
                            }
                            if(needed!=0)throw new InvalidOperationException("Original excess cooking input changed.");
                        }
                    }
                    original.ReturnBefore=before;original.ReturnAfter=after;
                }
                RebirthCookingGridCustody.PrepareExternal(null,original.Owner,plan.Operation,plan.Receipt,original.ReturnBefore,original.ReturnAfter,plan,original.Operation);
                bool Current()=>ThreadManager.IsMainThread()&&Clients.Contains(original)&&ReferenceEquals(GameManager.Instance?.World,original.World)&&
                    ReferenceEquals(original.Owner.world,original.World)&&ReferenceEquals(original.World.GetPrimaryPlayer(),original.Owner)&&
                    ReferenceEquals(original.World.GetEntity(original.Owner.entityId),original.Owner)&&!original.Owner.IsDead()&&original.Owner.Died==plan.Deaths&&
                    RebirthCamperCharacterAccess.CurrentCreation(original.Owner)==original.Creation&&ReferenceEquals(original.ReturnPlan,plan)&&
                    RebirthStablePlayerIdentity.TryResolveServerEntity(original.Owner,out var currentOwner)&&currentOwner.CanonicalId==plan.Owner;
                bool Clear()
                {
                    if(!Current())return false;
                    for(int i=0;i<original.ReturnAfter.Length;i++)
                    {
                        var live=original.Read();if(!Current()||live.Length!=original.ReturnAfter.Length)return false;
                        for(int j=0;j<live.Length;j++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(live[j],original.ReturnBefore[j])&&!RebirthStationGridIngredients.IsSameStackSnapshot(live[j],original.ReturnAfter[j]))return false;
                        if(!RebirthStationGridIngredients.IsSameStackSnapshot(live[i],original.ReturnAfter[i]))
                        {
                            original.Write(i,original.ReturnAfter[i].Clone());
                            var written=original.Read();
                            if(!Current()||written.Length!=original.ReturnAfter.Length||!RebirthStationGridIngredients.IsSameStackSnapshot(written[i],original.ReturnAfter[i]))return false;
                        }
                    }
                    var final=original.Read();
                    return Current()&&final.Length==original.ReturnAfter.Length&&final.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,original.ReturnAfter[index])).All(value=>value);
                }
                if(!RebirthCookingGridCustody.WithExternalDelta(plan.Operation,()=>RebirthCookingOwnerDelta.Advance(original.Owner,plan,returned?2:phase,Current,Clear)))return;
                if(returned&&RebirthCookingOwnerDelta.Retire(original.Owner,plan)&&RebirthCookingGridCustody.CompleteExternal(plan.Operation))
                    Clients.Remove(original);
            }
            catch(Exception){/* Retain the original for a later return attempt. */}
        });
        }
        catch(Exception){original.Waiting=false;}
    }
    internal static void RecoverDetachedReturn(EntityPlayerLocal owner,string location,Guid admission,Guid returned,ItemStack[] before,ItemStack[] after,bool alreadyApplied)
    {
        if(owner?.world==null||before?.Length!=12||after?.Length!=12||Clients.Any(original=>ReferenceEquals(original.World,owner.world)&&ReferenceEquals(original.Owner,owner)&&original.Operation==admission))return;
        if(Clients.Count>=4)throw new InvalidOperationException("Too many retained cooking originals.");
        var logical=ItemStack.Clone(alreadyApplied?after:before);var owed=new ItemStack[12];
        for(int i=0;i<12;i++)
        {
            int count=before[i].count-after[i].count;if(count<0||!after[i].IsEmpty()&&RebirthNativeItemCodec.Encode(before[i].itemValue)!=RebirthNativeItemCodec.Encode(after[i].itemValue))throw new InvalidDataException();
            owed[i]=count==0?ItemStack.Empty.Clone():new ItemStack(before[i].itemValue.Clone(),count);
        }
        var carrier=new ItemValue(1);carrier.SetMetadata(PayloadKey,new XElement("cookingAdmission",new XAttribute("version",1),new XAttribute("operation",admission.ToString("N"))).ToString(SaveOptions.DisableFormatting));
        Clients.Add(new ClientOriginal{Owner=owner,World=owner.world,Creation=RebirthCamperCharacterAccess.CurrentCreation(owner),Location=location,Operation=admission,
            Carrier=carrier,After=owed,ExpectedReturn=returned,ReturnBefore=ItemStack.Clone(before),ReturnAfter=ItemStack.Clone(after),
            Read=()=>ItemStack.Clone(logical),Write=(index,value)=>logical[index]=value.Clone()});
    }
    internal sealed class AdmissionPlan
    {
        internal EntityPlayer Player;internal RebirthStablePlayerIdentity Identity;
        internal TileEntityWorkstation Station;internal string Location,Creation;
        internal Guid Operation;internal Recipe Recipe;internal RecipeQueueItem[] Queue;
        internal ItemStack[] GridBefore,GridAfter;internal string SourceImageHash,OwnerReceipt;
        internal int Batches;internal Guid ParentOperation;
    }
    internal static string SourceHash(ItemStack[] bag,ItemStack[] belt,bool[] locks,int owned)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            byte[] b=RebirthCamperInventorySnapshot.Capture(bag),t=RebirthCamperInventorySnapshot.Capture(belt);
            writer.Write(b.Length);writer.Write(b);writer.Write(t.Length);writer.Write(t);writer.Write(owned);
            writer.Write(locks!=null);if(locks!=null){writer.Write(locks.Length);foreach(bool value in locks)writer.Write(value);}
            writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
        }
    }
    internal static string Handle(EntityPlayer player,string recipeName,string creation,string location,ItemValue carrier,int batches)
    {
        try
        {
            RebirthCookingRemoteAdmissionBackend.Install();
            if(!ThreadManager.IsMainThread()||player?.world==null||player.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,player.world)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||batches<1||batches>9999||
                recipeName==null||recipeName.Length>256||!RebirthSurvivorRequestScope.TryNormalize(creation,out creation)||
                carrier==null||!carrier.TryGetMetadata(PayloadKey,out string payload)||payload.Length>4*1024*1024)return "Invalid original cooking request.";
            var root=ReadProtocolXml(payload,4*1024*1024);
            if(root.Name!="cookingAdmission"||(int?)root.Attribute("version")!=1||!Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)return "Invalid original cooking operation.";
            TileEntityWorkstation station;RebirthWorldCharacterRecord record;
            if(RebirthCamperStationAddress.TryParse(location,out var mobile))
            {if(!RebirthStationLiveAccess.TryResolveMobile(player,mobile,creation,out station,out record))return "Current cooking station lease required.";}
            else
            {if(!RebirthCookingJobRuntime.TryPosition(location,out var position)||!RebirthStationLiveAccess.TryResolve(player,position,creation,out station,out record))return "Current cooking station lease required.";}
            string original=RebirthCookingRemoteAdmissionBackend.Observe(player,location,creation,operation);if(original!=null)return original;
            if(!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                !RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var locks))return Pending;
            if(SourceHash(bag,belt,locks,player.inventory.Length)!=(string)root.Attribute("source"))
            {
                if(RebirthCookingRemoteAdmissionBackend.Phase(identity,Receipt(operation),out float sourcePhase)&&sourcePhase==1f)
                    return RebirthCookingRemoteAdmissionBackend.Reject(player,location,creation,operation,"Native owner source changed before admission; original grid remains available.",root);
                return Pending;
            }
            var nodes=root.Elements("input").ToArray();if(nodes.Length<1||nodes.Length>12)return "Invalid cooking input count.";
            string gridImage=root.Element("grid")?.Value;if(gridImage==null||gridImage.Length>4*1024*1024)return "Invalid original cooking grid.";
            var grid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(gridImage));
            if(grid==null||grid.Length!=12||Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(grid))!=gridImage)return "Invalid original cooking grid.";
            var afterGrid=ItemStack.Clone(grid);
            var inputs=new List<ItemStack>();var types=new HashSet<int>();var mainTypes=new HashSet<int>();
            foreach(var node in nodes)
            {
                int amount=(int)node.Attribute("count");string image=(string)node.Attribute("item");
                if(amount<1||amount>65535||image==null||image.Length>RebirthNativeItemConformanceReader.MaximumEncodedCharacters)return "Invalid cooking input.";
                var item=DecodeItem(image);if(item==null||item.IsEmpty()||!types.Add(item.type)||!RebirthCookingItemStats.FullIngredient(item))return "Cooking requires distinct full ingredients.";
                int slot=Array.FindIndex(grid,stack=>stack!=null&&!stack.IsEmpty()&&stack.itemValue.type==item.type&&RebirthNativeItemCodec.Encode(stack.itemValue)==image);
                long paid=(long)amount*batches;
                if(slot<0||paid>grid[slot].count||paid>int.MaxValue)return "Original cooking grid does not contain payment.";
                if(slot<9)mainTypes.Add(item.type);else if(!RebirthCookingCatalogue.IsHerb(grid[slot]))return "Invalid cooking herb slot.";
                afterGrid[slot].count-=(int)paid;if(afterGrid[slot].count==0)afterGrid[slot]=ItemStack.Empty.Clone();
                inputs.Add(new ItemStack(item,amount));
            }
            if(grid.Where(stack=>stack!=null&&!stack.IsEmpty()).GroupBy(stack=>stack.itemValue.type).Any(group=>group.Count()>1)||
                grid.Any(stack=>stack!=null&&!stack.IsEmpty()&&!types.Contains(stack.itemValue.type)))return "Original cooking grid has unmatched inputs.";
            string area;
            if(station is RebirthCamperStationState mobileStation)
            {
                var installed=RebirthCamperModuleRuntime.For(mobileStation.BoundVehicle).Stations.Find(mobileStation.InstallationId);
                if(installed==null||!RebirthCamperCraftingStation.TryGetPresentation(installed.InstalledItem,out var presentation))return "Cooking station unavailable.";
                area=presentation.RecipeWorkstationName;
            }
            else
            {
                var block=player.world.GetBlock(station.ToWorldPos()).Block;
                area=block?.Properties.GetString("RebirthCraftingStation");if(string.IsNullOrEmpty(area))area=block?.GetBlockName();
            }
            string Refuse(string reason)=>RebirthCookingRemoteAdmissionBackend.Reject(player,location,creation,operation,reason,root);
            bool milling=area=="WorkbenchMortarPestle001_FR";
            if(!TryCanonical(player,recipeName,area,inputs,mainTypes,batches,grid,out var recipe,out var hidden))return Refuse("Cooking ingredients do not match a canonical recipe.");
            if(!RebirthStationToolAvailability.HasRequirement(station.Tools,recipe))return Refuse(Localization.Get("ttMissingCraftingTools"));
            if(station.Queue==null||station.Queue.Length<1||(!milling&&station.Queue.Any(row=>row?.Recipe!=null))||!station.Queue.Any(row=>row!=null&&row.Recipe==null))return Refuse("Cooking queue has no available slot.");
            var prepared=RebirthCookingSessionService.Ready(player,recipeName);
            recipe.craftingTime*=RebirthCookingRules.TechniqueTime(RebirthCookingCatalogue.Effect(prepared?.Magazine));
            RebirthCookingBatch.Stamp(recipe,!string.IsNullOrEmpty(prepared?.Book),player,prepared?.Magazine,batches,true);
            recipe.ingredients[0].itemValue.SetMetadata("rebirth.cooking.queue.token",operation.ToString("N"));
            RebirthCookingHeat.Mark(recipe,batches,hidden,RebirthCookingHeatRules.BurnMethod(recipe),"",actor:player);
            recipe.ingredients[0].itemValue.SetMetadata(PendingKey,1);
            var memory=record.Progression.Cooking;string token=operation.ToString("N");
            if(memory.Awards.ContainsKey(token))return "Original cooking operation already terminal.";
            if(memory.Tickets.TryGetValue(token,out var prior)&&(prior.Recipe!=recipeName||prior.Portions!=batches||prior.OutputCount!=recipe.count||prior.StationPosition!=location))return "Original cooking ticket conflicts.";
            memory.Tickets[token]=new RebirthCookingMemory.Ticket{Recipe=recipeName,Portions=batches,OutputCount=recipe.count,StationPosition=location,XpMultiplier=string.IsNullOrEmpty(prepared?.Book)?1f:RebirthCookingRules.BookMultiplier};
            memory.JobsChanged();RebirthWorldCharacterService.MarkDirty(record,"cooking-remote-admission");
            if(!RebirthWorldCharacterService.FlushPlayer(player,"cooking-remote-admission"))return Pending;
            // Re-resolve after save callbacks; native coupled commit rechecks lease and images again.
            if(!ReferenceEquals(RebirthCookingStationLocator.Resolve(player.world,location),station)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return Pending;
            var queue=(RecipeQueueItem[])station.Queue.Clone();int queueSlot=Array.FindLastIndex(queue,row=>row!=null&&row.Recipe==null);
            queue[queueSlot]=new RecipeQueueItem{Recipe=recipe,Multiplier=checked((short)batches),Quality=1,StartingEntityId=player.entityId,
                CraftingTimeLeft=recipe.craftingTime*batches,OneItemCraftTime=recipe.craftingTime,IsCrafting=true};
            var plan=new AdmissionPlan{Player=player,Identity=identity,Station=station,Location=location,Creation=creation,Operation=operation,Recipe=recipe,Queue=queue,
                Batches=batches,
                GridBefore=grid,GridAfter=afterGrid,SourceImageHash=(string)root.Attribute("source"),OwnerReceipt=Receipt(operation),
                ParentOperation=Guid.TryParseExact((string)root.Attribute("parent"),"N",out var parentOperation)?parentOperation:Guid.Empty};
            return RebirthCookingRemoteAdmissionBackend.Begin(plan)?Pending:"Cooking admission retained without a payment acknowledgement; retry the original request.";
        }
        catch(Exception error){Log.Warning("[Cooking] Original remote admission retained: "+error.GetType().Name);return "Cooking admission unavailable; original ingredients were not assumed paid.";}
    }
    private static bool TryCanonical(EntityPlayer player,string name,string area,List<ItemStack> inputs,HashSet<int> mainTypes,int batches,ItemStack[] grid,out Recipe recipe,out bool hidden)
    {
        recipe=null;hidden=false;if(string.IsNullOrEmpty(area))return false;
        var main=inputs.Where(input=>mainTypes.Contains(input.itemValue.type)).ToList();
        var source=RebirthCookingRecipeLookup.Find(name);
        if(area=="WorkbenchMortarPestle001_FR")
        {
            if(grid.Skip(9).Any(item=>!item.IsEmpty())||inputs.Count!=main.Count)return false;
            foreach(var candidate in RebirthStationGridIngredients.Candidates(area,XUiM_Recipes.GetRecipes()).Where(value=>value.GetName()==name))
            {
                if(!RebirthStationGridIngredients.TryPlan(player,candidate,grid.Take(9).ToArray(),batches,1,out var exact))continue;
                if(exact.Inputs.Count!=inputs.Count||exact.Inputs.Any(allocation=>allocation.Count%batches!=0||!inputs.Any(input=>
                    input.count==allocation.Count/batches&&RebirthNativeItemCodec.Encode(input.itemValue)==RebirthNativeItemCodec.Encode(allocation.Snapshot().itemValue))))continue;
                recipe=new Recipe{itemValueType=candidate.itemValueType,count=candidate.count,craftingArea=area,craftingToolType=candidate.craftingToolType,
                    craftingTime=candidate.craftingTime,craftExpGain=candidate.craftExpGain,Effects=candidate.Effects,tags=candidate.tags,UseIngredientModifier=false,craftingTier=1};break;
            }
            if(recipe==null)return false;
        }
        else if(source!=null&&RebirthCookingCatalogue.IsCooking(source)&&RebirthCookingCatalogue.AtStation(source,area))
        {
            var canonical=RebirthCookingCatalogue.ForStation(source,area);var dish=RebirthCookingCatalogue.Get(name);
            if(main.Count!=canonical.ingredients.Count||!Assign(0,canonical.ingredients,main,new HashSet<int>(),dish))return false;
            if(inputs.Except(main).Any(input=>input.count!=1||dish==null||dish.Herbs.IndexOf(input.itemValue.ItemClass.GetItemName().Replace("foodCrop",""),StringComparison.OrdinalIgnoreCase)<0))return false;
            hidden=!RebirthCookingCatalogue.Known(player,canonical);
            recipe=new Recipe{itemValueType=canonical.itemValueType,count=canonical.count,craftingArea=area,craftingToolType=canonical.craftingToolType,
                craftingTime=canonical.craftingTime,craftExpGain=canonical.craftExpGain,Effects=canonical.Effects,tags=canonical.tags,UseIngredientModifier=false,craftingTier=1};
        }
        else
        {
            string method=RebirthCookingHeatRules.ImprovisedMethod(area,main);
            if(main.Count(input=>!RebirthCookingCatalogue.IsHerb(input))<3||!RebirthCookingHeatRules.ValidIngredients(main,method,out _)||
                !Enumerable.Range(1,4).Any(variant=>name=="rebirthImprovised"+method+variant.ToString("00")))return false;
            float nutrition=0f;foreach(var input in main)
            {if(!RebirthCookingCatalogue.IsIngredient(input))return false;if(RebirthConsumableResolver.TryResolve(input.itemValue,out var food))nutrition+=food.NutritionUnits*input.count;}
            if(float.IsNaN(nutrition)||float.IsInfinity(nutrition)||nutrition<=0f)return false;
            var output=ItemClass.GetItem(name);if(output==null||output.IsEmpty())return false;
            string tool=method=="Baked"?"FuriousRamsayBakingPan":method=="Pan"?"rebirthCookingFryingPan":"toolCookingPot";
            recipe=new Recipe{itemValueType=output.type,count=Math.Max(1,(int)Math.Ceiling(nutrition*1.1f/35f)),craftingArea=area,
                craftingToolType=ItemClass.GetItem(tool).type,craftingTime=RebirthCookingHeatRules.Duration(method,main.Count),craftExpGain=5,UseIngredientModifier=false,craftingTier=1};
        }
        if(recipe.count<1||!RebirthRecipeDiscoveryRules.Allows(player,name)||!RebirthCapabilityService.EvaluateRecipeForDiscovery(player,name).IsAllowed)return false;
        recipe.ingredients.AddRange(inputs.Select(input=>input.Clone()));
        float time=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,_entity:player,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
        if(float.IsNaN(time)||float.IsInfinity(time)||time<0f||time>86400f)return false;recipe.craftingTime=time;return true;
    }
    private static bool Assign(int role,List<ItemStack> requirements,List<ItemStack> inputs,HashSet<int> assigned,RebirthCookingCatalogue.Dish dish)
    {
        if(role==requirements.Count)return true;var required=requirements[role];
        foreach(var input in inputs)
        {
            string id=input.itemValue.ItemClass.GetItemName();
            bool allowed=input.itemValue.type==required.itemValue.type||dish!=null&&dish.Substitutes.TryGetValue(required.itemValue.ItemClass.GetItemName(),out var choices)&&choices.Contains(id);
            if(!allowed||input.count!=required.count||!assigned.Add(input.itemValue.type))continue;
            if(Assign(role+1,requirements,inputs,assigned,dish))return true;assigned.Remove(input.itemValue.type);
        }
        return false;
    }
}
