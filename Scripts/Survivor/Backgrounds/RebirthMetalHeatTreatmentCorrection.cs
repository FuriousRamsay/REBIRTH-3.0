using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using HarmonyLib;

#nullable disable

// Heat treatment owns one original native craft output. It never hydrates or
// rewrites the server's remote bag and never replaces a current ItemValue whole.
internal static class RebirthMetalHeatTreatmentCorrection
{
    private const string OriginalKey="RebirthHeatOriginalV1",AppliedKey="RebirthHeatAppliedV1";
    private const string RecipeKey="RebirthHeatRecipeV1";
    private const string WorldKey="RebirthHeatWorldV1",CreationKey="RebirthHeatCreationV1",OwnerKey="RebirthHeatOwnerV1",DeathsKey="RebirthHeatDeathsV1";
    private const int MaximumImage=262144;
    internal sealed class Receipt
    {
        internal Guid Operation;internal string World,Owner,Creation,Recipe,Before,After=string.Empty;
        internal int Deaths;internal bool Published;
    }
    private sealed class Original
    {internal World World;internal EntityPlayerLocal Player;internal ItemValue Item;internal XUiC_RecipeStack Stack;internal string Recipe,Creation,Owner,Directory;internal int Deaths;internal Guid Operation;}
    private sealed class Client
    {internal Receipt Receipt;internal World World;internal string Directory;internal EntityPlayerLocal Player;internal float Next;}
    private sealed class Upload
    {internal World World;internal EntityPlayer Player;internal string Creation,Directory;internal int Deaths;internal ItemStack[] Bag,Belt;}
    private static readonly Dictionary<XUiC_RecipeStack,Original> Originals=new Dictionary<XUiC_RecipeStack,Original>();
    private static readonly Dictionary<Guid,Client> Clients=new Dictionary<Guid,Client>();
    private static readonly Dictionary<int,Upload> Uploads=new Dictionary<int,Upload>();
    private static World activeWorld;private static string activeDirectory;private static float nextScan;
    internal static string Image(ItemValue item)=>RebirthNativeItemCodec.Encode(item);
    private static ItemValue ReadItem(string image)
    {if(string.IsNullOrEmpty(image)||image.Length>MaximumImage||!RebirthNativeItemCodec.TryDecode(image,out var item)||item?.ItemClass==null||item.IsEmpty()||float.IsNaN(item.UseTimes)||float.IsInfinity(item.UseTimes)||item.UseTimes<0f)throw new InvalidDataException();return item;}
    private static bool SameExceptWear(ItemValue current,ItemValue original)
    {
        if(current==null||original==null||float.IsNaN(current.UseTimes)||float.IsInfinity(current.UseTimes)||current.UseTimes<original.UseTimes)return false;
        var clone=ReadItem(Image(current));clone.UseTimes=original.UseTimes;return Image(clone)==Image(original);
    }
    private static bool SameOperation(ItemValue value,Guid operation,string key)=>value!=null&&value.TryGetMetadata(key,out string id)&&id==operation.ToString("N");
    private static MemberInfo Member(Type type,string name)=> (MemberInfo)type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)??type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    private static object Get(MemberInfo member,object target)=>member is FieldInfo field?field.GetValue(target):((PropertyInfo)member).GetValue(target,null);
    private static void Set(MemberInfo member,object target,object value){if(member is FieldInfo field)field.SetValue(target,value);else ((PropertyInfo)member).SetValue(target,value,null);}
    private static bool Stat(ItemValue item,out Array stats,out int index,out MemberInfo value)
    {
        stats=null;index=-1;value=null;var member=Member(item.GetType(),"Stats");if(member==null)return false;stats=Get(member,item) as Array;
        if(stats==null||stats.Length>256||!stats.GetType().GetElementType().IsValueType)return false;
        for(int i=0;i<stats.Length;i++)
        {
            object row=stats.GetValue(i);if(row==null)return false;var type=Member(row.GetType(),"type");var candidate=Member(row.GetType(),"value");
            if(type==null||candidate==null)return false;if(Convert.ToInt32(Get(type,row))!=(int)PassiveEffects.DegradationMax)continue;
            if(index>=0)return false;index=i;value=candidate;
        }
        return index>=0;
    }
    // The before/after pair is the server's immutable accepted operation. Copy
    // only its one supported native stat, then provenance-owned metadata.
    internal static bool TryMerge(ItemValue current,Receipt receipt,out ItemValue merged)
    {
        merged=null;
        try
        {
            var before=ReadItem(receipt.Before);var after=ReadItem(receipt.After);
            if(!SameOperation(before,receipt.Operation,OriginalKey)||!SameOperation(after,receipt.Operation,OriginalKey)||!SameOperation(after,receipt.Operation,AppliedKey))return false;
            if(!after.TryGetMetadata("RebirthProvBonus",out string bonus)||bonus!=RebirthWorkmanshipSignatureService.HeatTreatmentBonusId||!after.TryGetMetadata("RebirthProvBatch",out string batch)||batch!="metal-original:"+receipt.Operation.ToString("N"))return false;
            bool already=SameOperation(current,receipt.Operation,AppliedKey);
            if((already?!SameExceptWear(current,after):!SameExceptWear(current,before))||after.MaxUseTimes<=before.MaxUseTimes||!Stat(before,out var beforeStats,out int beforeIndex,out var beforeMember)||!Stat(after,out var afterStats,out int afterIndex,out var afterMember)||beforeIndex!=afterIndex||beforeStats.Length!=afterStats.Length)return false;
            // A normalization back to the original image proves all unrelated
            // native stats, modifiers, textures and metadata stayed unchanged.
            var normalized=ReadItem(Image(after));if(!Stat(normalized,out var normalStats,out int normalIndex,out var normalMember))return false;
            object original=beforeStats.GetValue(beforeIndex);object row=normalStats.GetValue(normalIndex);Set(normalMember,row,Get(beforeMember,original));normalStats.SetValue(row,normalIndex);
            foreach(string key in new[]{"RebirthProvVersion","RebirthProvKind","RebirthProvSource","RebirthProvCreator","RebirthProvBackground","RebirthProvBonus","RebirthProvSkill","RebirthProvSkillValue","RebirthProvWorkmanship","RebirthProvOriginalMaxUseTimes","RebirthProvBatch","RebirthDurabilityOriginalMaxUseTimes",AppliedKey})normalized.RemoveMetaData(key);
            // Original outputs with pre-existing provenance are excluded at admission.
            if(Image(normalized)!=Image(before))return false;
            if(already){merged=ReadItem(Image(current));return true;}
            var result=ReadItem(Image(current));if(!Stat(result,out var resultStats,out int resultIndex,out var resultMember)||resultIndex!=afterIndex)return false;
            row=resultStats.GetValue(resultIndex);Set(resultMember,row,Get(afterMember,afterStats.GetValue(afterIndex)));resultStats.SetValue(row,resultIndex);
            if(!RebirthItemProvenanceAdapter.TryApplyOwnedCorrection(result,after))return false;result.SetMetadata(AppliedKey,receipt.Operation.ToString("N"));
            if(result.MaxUseTimes!=after.MaxUseTimes||result.UseTimes!=current.UseTimes)return false;
            var expected=ReadItem(Image(after));expected.UseTimes=current.UseTimes;if(Image(expected)!=Image(result))return false;
            merged=result;return true;
        }
        catch{return false;}
    }
    private static string Encode(Receipt r)=>new XElement("metalHeat",new XAttribute("version",1),new XAttribute("operation",r.Operation.ToString("N")),new XAttribute("world",r.World),new XAttribute("owner",r.Owner),new XAttribute("creation",r.Creation),new XAttribute("deaths",r.Deaths),new XAttribute("recipe",r.Recipe),new XAttribute("published",r.Published),new XElement("before",r.Before),new XElement("after",r.After)).ToString(SaveOptions.DisableFormatting);
    private static Receipt Decode(string image)
    {
        if(image==null||image.Length>MaximumImage*3)throw new InvalidDataException();XElement e;
        using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumImage*3}))e=XElement.Load(reader);
        if(e.Name!="metalHeat"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();
        var r=new Receipt{Operation=Guid.ParseExact((string)e.Attribute("operation"),"N"),World=(string)e.Attribute("world"),Owner=(string)e.Attribute("owner"),Creation=(string)e.Attribute("creation"),Deaths=(int)e.Attribute("deaths"),Recipe=(string)e.Attribute("recipe"),Published=(bool)e.Attribute("published"),Before=(string)e.Element("before"),After=(string)e.Element("after")};
        if(r.Operation==Guid.Empty||!Guid.TryParse(r.World,out _)||string.IsNullOrEmpty(r.Owner)||r.Owner.Length>512||string.IsNullOrEmpty(r.Creation)||r.Creation.Length>512||r.Deaths<0||string.IsNullOrEmpty(r.Recipe)||r.Recipe.Length>256||!SameOperation(ReadItem(r.Before),r.Operation,OriginalKey)||Encode(r)!=image)throw new InvalidDataException();
        var original=ReadItem(r.Before);
        if(!original.TryGetMetadata(RecipeKey,out string recipe)||recipe!=r.Recipe||!original.TryGetMetadata(WorldKey,out string world)||world!=r.World||!original.TryGetMetadata(CreationKey,out string creation)||creation!=r.Creation||!original.TryGetMetadata(OwnerKey,out string owner)||owner!=r.Owner||!original.TryGetMetadata(DeathsKey,out int deaths)||deaths!=r.Deaths)throw new InvalidDataException();
        if(r.After.Length!=0&&!TryMerge(original,r,out _))throw new InvalidDataException();if(r.Published&&r.After.Length==0)throw new InvalidDataException();return r;
    }
    private static string PathFor(Receipt r,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","metal-heat",server?"server":"owner",r.Operation.ToString("N")+".xml");
    private static bool Read(Receipt key,bool server,out Receipt receipt)
    {receipt=null;try{string path=PathFor(key,server);if(new FileInfo(path).Length>MaximumImage*12L)return false;var r=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));if(r.Operation!=key.Operation||r.World!=key.World||r.Owner!=key.Owner||r.Creation!=key.Creation||r.Deaths!=key.Deaths||r.Recipe!=key.Recipe||r.Before!=key.Before)return false;receipt=r;return true;}catch{return false;}}
    private static bool Save(Receipt r,bool server,string expected=null)
    {
        string path=PathFor(r,server),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            string image=Encode(r);Decode(image);
            if(File.Exists(path)||File.Exists(path+".bak")){if(!Read(r,server,out var prior))return false;if(expected==null)return Encode(prior)==image;if(Encode(prior)!=expected)return false;}else if(expected!=null)return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            return Encode(Decode(File.ReadAllText(temporary,new UTF8Encoding(false,true))))==image&&RebirthDurableFileCommit.TryPublish(temporary,path,out _);
        }
        catch{return false;}
    }
    private static void WorldScope(World world)
    {string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(activeWorld,world)&&activeDirectory==directory)return;activeWorld=world;activeDirectory=directory;Originals.Clear();Clients.Clear();Uploads.Clear();nextScan=0;}
    private static bool Current(Client c)=>ReferenceEquals(GameManager.Instance?.World,c.World)&&GameIO.GetSaveGameDir()==c.Directory&&ReferenceEquals(c.World.GetPrimaryPlayer(),c.Player)&&ReferenceEquals(c.Player.world,c.World)&&ReferenceEquals(c.World.GetEntity(c.Player.entityId),c.Player)&&c.Player.IsSpawned()&&!c.Player.IsDead()&&c.Player.Died==c.Receipt.Deaths&&c.World.worldState?.Guid==c.Receipt.World&&RebirthCamperCharacterAccess.CurrentCreation(c.Player)==c.Receipt.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(c.Player,out var identity)&&identity.CanonicalId==c.Receipt.Owner;
    internal static void OriginalOutput(XUiC_RecipeStack stack,EntityPlayerLocal owner,ItemValue item,string recipe)
    {try{OriginalOutputCore(stack,owner,item,recipe);}catch(Exception failure){Log.Warning("[REBIRTH Heat] original capture refused: "+failure.GetType().Name);}}
    private static void OriginalOutputCore(XUiC_RecipeStack stack,EntityPlayerLocal owner,ItemValue item,string recipe)
    {
        if(owner==null||stack==null||item==null||!owner.world.IsRemote()||stack.AmountToRepair>0||stack.recipe?.count!=1||stack.originalItem!=null&&!stack.originalItem.IsEmpty()||item.MaxUseTimes<=0||RebirthItemProvenanceAdapter.HasProvenance(item))return;
        WorldScope((World)owner.world);if(Originals.Count>=16&&!Originals.ContainsKey(stack)||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity))return;
        Guid operation=Guid.NewGuid();item.SetMetadata(OriginalKey,operation.ToString("N"));item.SetMetadata(RecipeKey,recipe);
        item.SetMetadata(WorldKey,owner.world.worldState.Guid);item.SetMetadata(CreationKey,RebirthCamperCharacterAccess.CurrentCreation(owner));item.SetMetadata(OwnerKey,identity.CanonicalId);item.SetMetadata(DeathsKey,owner.Died);
        Originals[stack]=new Original{World=(World)owner.world,Player=owner,Stack=stack,Item=item,Recipe=recipe,Operation=operation,Creation=RebirthCamperCharacterAccess.CurrentCreation(owner),Owner=identity.CanonicalId,Deaths=owner.Died,Directory=GameIO.GetSaveGameDir()};
    }
    internal static void OutputSucceeded(XUiC_RecipeStack stack,bool result)
    {try{OutputSucceededCore(stack,result);}catch(Exception failure){Log.Warning("[REBIRTH Heat] original output proof retained: "+failure.GetType().Name);}}
    private static void OutputSucceededCore(XUiC_RecipeStack stack,bool result)
    {
        if(!Originals.TryGetValue(stack,out var original))return;Originals.Remove(stack);
        if(!result||!ReferenceEquals(stack.outputItemValue,original.Item)||!ReferenceEquals(GameManager.Instance?.World,original.World)||original.Player.IsDead()||original.Player.Died!=original.Deaths||RebirthCamperCharacterAccess.CurrentCreation(original.Player)!=original.Creation||GameIO.GetSaveGameDir()!=original.Directory||!RebirthStablePlayerIdentity.TryResolveServerEntity(original.Player,out var owner)||owner.CanonicalId!=original.Owner)return;
        // Success alone is insufficient: this exact one-item output must exist in
        // the real owner grids or native workstation output, not a failed retry.
        var rows=new List<ItemStack>();rows.AddRange(original.Player.bag.ItemGrid.items);rows.AddRange(original.Player.inventory.ItemGrid.items);
        var grid=stack.windowGroup?.Controller?.GetChildByType<XUiC_WorkstationOutputGrid>();if(grid!=null)rows.AddRange(grid.GetSlots());
        if(!OriginalAdmittedOutput(result,original.Item,stack.outputItemValue,rows,original.Operation))return;
        var receipt=new Receipt{Operation=original.Operation,World=original.World.worldState.Guid,Owner=owner.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(original.Player),Deaths=original.Player.Died,Recipe=original.Recipe,Before=Image(original.Item)};
        Clients[receipt.Operation]=new Client{Receipt=receipt,World=original.World,Player=original.Player,Directory=GameIO.GetSaveGameDir()};if(Save(receipt,false))UploadOwner(original.Player);
    }
    internal static bool OriginalAdmittedOutput(bool succeeded,ItemValue original,ItemValue output,IEnumerable<ItemStack> rows,Guid operation)
    {
        if(!succeeded||!ReferenceEquals(original,output)||!SameOperation(original,operation,OriginalKey)||rows==null)return false;
        int found=0;foreach(var row in rows)
        {if(row==null||row.IsEmpty()||!SameOperation(row.itemValue,operation,OriginalKey))continue;if(row.count!=1||Image(row.itemValue)!=Image(original)||++found!=1)return false;}
        return found==1;
    }
    private static void UploadOwner(EntityPlayerLocal owner)
    {GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    private static ItemValue Unique(ItemStack[] bag,ItemStack[] belt,Receipt r,bool after)
    {
        var expected=ReadItem(after?r.After:r.Before);ItemValue found=null;
        foreach(var stack in (bag??Array.Empty<ItemStack>()).Concat(belt??Array.Empty<ItemStack>()))
        {if(stack==null||stack.IsEmpty()||!SameOperation(stack.itemValue,r.Operation,OriginalKey))continue;if(found!=null||stack.count!=1||!SameExceptWear(stack.itemValue,expected))return null;found=stack.itemValue;}
        return found;
    }
    // Postfix consumes only authenticated native incoming blobs. Missing areas
    // remain missing; no fallback to stale live EntityPlayer grids is permitted.
    internal static void NativeUpload(NetPackage package,World world)
    {try{NativeUploadCore(package,world);}catch(Exception failure){Log.Warning("[REBIRTH Heat] native upload proof refused: "+failure.GetType().Name);}}
    private static void NativeUploadCore(NetPackage package,World world)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||package?.Sender==null||!ReferenceEquals(GameManager.Instance?.World,world))return;
        var player=world.GetEntity(package.Sender.entityId) as EntityPlayer;if(player==null||!ReferenceEquals(player.world,world)||player.IsDead())return;
        var latest=package is NetPackagePlayerData full?full.playerDataFile:package.Sender.latestPlayerData;if(latest==null||latest.deaths!=player.Died||package is NetPackagePlayerData&&latest.id!=player.entityId)return;
        var bag=RebirthGearPlayerDataInventory.ReadSlots(latest,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(latest,false);if(bag==null||belt==null)return;
        WorldScope(world);Uploads[player.entityId]=new Upload{World=world,Player=player,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=player.Died,Directory=GameIO.GetSaveGameDir(),Bag=bag,Belt=belt};
    }
    private static bool NativeProof(EntityPlayer owner,Receipt r,bool after)
    {
        if(!Uploads.TryGetValue(owner.entityId,out var upload)||!ReferenceEquals(upload.World,GameManager.Instance?.World)||!ReferenceEquals(upload.Player,owner)||upload.Creation!=r.Creation||upload.Deaths!=r.Deaths||upload.Directory!=GameIO.GetSaveGameDir()||Unique(upload.Bag,upload.Belt,r,after)==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actor,out var saved)||saved.deaths!=r.Deaths||Unique(actor.Bag,actor.Belt,r,after)==null)return false;
        return true;
    }
    internal static void ReceiveServer(World world,int playerId,string image)
    {
        try
        {
            if(world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world))return;WorldScope(world);var request=Decode(image);var owner=world.GetEntity(playerId) as EntityPlayer;
            if(owner==null||owner.IsDead()||owner.Died!=request.Deaths||request.World!=world.worldState.Guid||request.Creation!=RebirthCamperCharacterAccess.CurrentCreation(owner)||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||identity.CanonicalId!=request.Owner)return;
            Receipt r;if(Read(request,true,out var existing))r=existing;else
            {
                if(File.Exists(PathFor(request,true))||File.Exists(PathFor(request,true)+".bak")||request.After.Length!=0||request.Published||!NativeProof(owner,request,false))return;
                r=Decode(Encode(request));var corrected=ReadItem(r.Before);
                var recipe=CraftingManager.GetRecipe(r.Recipe);
                if(recipe==null||recipe.IsScrap||recipe.count!=1||recipe.itemValueType!=corrected.type||!string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipe),"skill.metalworking",StringComparison.OrdinalIgnoreCase))return;
                if(!RebirthWorkmanshipSignatureService.TryApplyMetalHeatTreatment(owner,r.Recipe,corrected,"authenticated-original-native-heat-output"))return;
                if(!RebirthItemProvenanceAdapter.TryRead(corrected,out var provenance))return;provenance.BatchToken="metal-original:"+r.Operation.ToString("N");RebirthItemProvenanceAdapter.Stamp(corrected,provenance);corrected.SetMetadata(AppliedKey,r.Operation.ToString("N"));r.After=Image(corrected);
                if(!TryMerge(ReadItem(r.Before),r,out _)||!Save(r,true))return;
            }
            if(!r.Published&&NativeProof(owner,r,true)){string prior=Encode(r);r.Published=true;if(!Save(r,true,prior))return;}
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetalHeatReply>().Setup(playerId,Encode(r)),_attachedToEntityId:playerId);
        }
        catch(Exception failure){Log.Warning("[REBIRTH Heat] original native correction retained: "+failure.GetType().Name);}
    }
    internal static void ReceiveClient(World world,int playerId,string image)
    {
        try
        {
            if(world?.IsRemote()!=true)return;WorldScope(world);var r=Decode(image);var owner=world.GetPrimaryPlayer();if(owner==null||owner.entityId!=playerId)return;
            if(!Clients.TryGetValue(r.Operation,out var c)){if(!Read(r,false,out var original))return;c=new Client{Receipt=original,World=world,Player=owner,Directory=GameIO.GetSaveGameDir()};Clients.Add(r.Operation,c);}
            if(!Current(c)||c.Receipt.Before!=r.Before||c.Receipt.Recipe!=r.Recipe||r.After.Length==0||c.Receipt.After.Length!=0&&c.Receipt.After!=r.After||c.Receipt.Published&&!r.Published)return;
            string prior=Encode(c.Receipt);if(!Save(r,false,prior))return;c.Receipt=r;
        }
        catch(Exception failure){Log.Warning("[REBIRTH Heat] owner correction retained: "+failure.GetType().Name);}
    }
    private static bool Apply(Client c)
    {
        var p=c.Player;var r=c.Receipt;
        if(!Current(c)||RebirthCookingOutputDelivery.IsHeld(p)||RebirthLogisticsOwnerReceipt.IsHeld(p)||AdvancedFarmingHarvestOwnerDelta.IsHeld(p)||RebirthGearOwnerReservation.IsHeld(p)||RebirthBackpackLibraryReservation.IsHeld(p)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(p)||RebirthCamperStationNativeActorPayment.Blocks(p.bag)||RebirthCamperStationNativeActorPayment.Blocks(p.inventory))return false;
        var bag=p.bag.ItemGrid.items;var belt=p.inventory.ItemGrid.items;ItemStack candidate=null;bool inBag=false;int index=-1;
        for(int area=0;area<2;area++){var rows=area==0?bag:belt;for(int slot=0;slot<rows.Length;slot++){var row=rows[slot];if(row==null||row.IsEmpty()||!SameOperation(row.itemValue,r.Operation,OriginalKey))continue;if(candidate!=null||row.count!=1)return false;candidate=row;inBag=area==0;index=slot;}}
        if(candidate==null||!TryMerge(candidate.itemValue,r,out var merged))return false;if(Image(candidate.itemValue)==Image(merged))return true;
        var next=new ItemStack(merged,1);if(inBag)p.bag.SetSlot(index,next);else p.inventory.SetItem(index,next);
        var after=inBag?p.bag.ItemGrid.items[index]:p.inventory.ItemGrid.items[index];return after!=null&&after.count==1&&Image(after.itemValue)==Image(merged);
    }
    internal static void Pump(World world)
    {
        if(world==null)return;WorldScope(world);if(!world.IsRemote())return;var owner=world.GetPrimaryPlayer();if(owner==null)return;
        if(UnityEngine.Time.realtimeSinceStartup>=nextScan)
        {
            nextScan=UnityEngine.Time.realtimeSinceStartup+2f;
            try
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","metal-heat","owner");if(Directory.Exists(directory))foreach(string path in Directory.EnumerateFiles(directory,"*.xml").Take(100001))
                {if(Clients.Count>=16)break;try{if(new FileInfo(path).Length>MaximumImage*12L)continue;var r=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));if(Clients.ContainsKey(r.Operation)||r.Published&&File.Exists(path+".done")&&File.ReadAllText(path+".done",new UTF8Encoding(false,true))==Encode(r))continue;var c=new Client{Receipt=r,World=world,Player=owner,Directory=GameIO.GetSaveGameDir()};if(Current(c))Clients.Add(r.Operation,c);}catch{/* Preserve the corrupt primary; continue unrelated original receipts. */}}
            }
            catch(Exception failure){Log.Warning("[REBIRTH Heat] owner journal recovery retained: "+failure.GetType().Name);}
        }
        foreach(var c in Clients.Values.ToArray())
        {
            if(!Current(c)||UnityEngine.Time.realtimeSinceStartup<c.Next)continue;c.Next=UnityEngine.Time.realtimeSinceStartup+1f;
            try
            {
                if(!File.Exists(PathFor(c.Receipt,false))&&!Save(c.Receipt,false))continue;
                if(c.Receipt.After.Length!=0&&!Apply(c))continue;UploadOwner(owner);
                if(c.Receipt.Published)
                {
                    string path=PathFor(c.Receipt,false),done=path+".done";
                    if(!Read(c.Receipt,false,out var saved)||Encode(saved)!=Encode(c.Receipt))continue;
                    if(!File.Exists(done))File.Move(path,done);
                    else if(File.ReadAllText(done,new UTF8Encoding(false,true))!=Encode(c.Receipt))continue;
                    Clients.Remove(c.Receipt.Operation);continue;
                }
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMetalHeatRequest>().Setup(owner.entityId,Encode(c.Receipt)));
            }
            catch(Exception failure){Log.Warning("[REBIRTH Heat] original correction pending: "+failure.GetType().Name);}
        }
    }
    internal static void Install(Harmony harmony)
    {RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthMetalHeatOutputSuccess));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthMetalHeatNativeUpload));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthMetalHeatNativePlayerData));}
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"outputStack")]
internal static class RebirthMetalHeatOutputSuccess
{private static void Postfix(XUiC_RecipeStack __instance,bool __result)=>RebirthMetalHeatTreatmentCorrection.OutputSucceeded(__instance,__result);}
[HarmonyPatch(typeof(NetPackagePlayerInventory),nameof(NetPackagePlayerInventory.ProcessPackage))]
internal static class RebirthMetalHeatNativeUpload
{private static void Postfix(NetPackagePlayerInventory __instance,World __0)=>RebirthMetalHeatTreatmentCorrection.NativeUpload(__instance,__0);}
[HarmonyPatch(typeof(NetPackagePlayerData),nameof(NetPackagePlayerData.ProcessPackage))]
internal static class RebirthMetalHeatNativePlayerData
{private static void Postfix(NetPackagePlayerData __instance,World __0,bool __runOriginal){if(__runOriginal)RebirthMetalHeatTreatmentCorrection.NativeUpload(__instance,__0);}}
