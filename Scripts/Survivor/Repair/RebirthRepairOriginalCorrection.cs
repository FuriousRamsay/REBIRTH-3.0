using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using HarmonyLib;

#nullable disable

// Repair owns one accepted original native paid queue/output. It never hydrates or
// rewrites the server's remote bag and never replaces a current ItemValue whole.
internal static partial class RebirthRepairOriginalCorrection
{
    private const string OriginalKey="RebirthRepairOriginalV1",AppliedKey="RebirthRepairAppliedV1";

    private const string WorldKey="RebirthRepairWorldV1",CreationKey="RebirthRepairCreationV1",OwnerKey="RebirthRepairOwnerV1",DeathsKey="RebirthRepairDeathsV1";
    private const string PaidKitTypeKey="RebirthRepairPaidKitTypeV1",PaidKitCountKey="RebirthRepairPaidKitCountV1";
    private const int MaximumImage=262144;
    internal sealed class Receipt
    {
        internal Guid Operation;internal string World,Owner,Creation,Input,Expected=string.Empty,Before=string.Empty,After=string.Empty;
        internal string Payment=string.Empty,PaidInput=string.Empty,PaymentProof=string.Empty,CancelInput=string.Empty;
        internal int Amount;internal bool Admitted,Cancelled,CancelConfirmed,CancelReturned,OutputInvoking,NativeDelivered;
        internal int Deaths;internal bool Published,BaseOnly,BaseOnlyConfirmed;
    }
    private sealed class Activation
    {internal ItemActionEntryRepair Entry;internal EntityPlayerLocal Player;internal IItemStackController Controller;internal XUiC_EquipmentStack Equipment;internal int KitType,BeforeKits,ExpectedKits,TotalKits;internal string NativeInput=string.Empty,Payment=string.Empty,PaidInput=string.Empty;internal List<Client> Queued=new List<Client>();}
    private sealed class Prepared {internal ItemActionEntryRepair Entry;internal Receipt Receipt;internal EntityPlayerLocal Player;internal World World;internal string Directory;}
    private static readonly Dictionary<ItemActionEntryRepair,Prepared> PreparedRemote=new Dictionary<ItemActionEntryRepair,Prepared>();
    [ThreadStatic] private static Activation activation;
    private static readonly Dictionary<XUiC_RecipeStack,Guid> Staged=new Dictionary<XUiC_RecipeStack,Guid>();
    private static readonly Dictionary<XUiC_RecipeStack,Client> Cards=new Dictionary<XUiC_RecipeStack,Client>();
    private sealed class Client
    {internal Receipt Receipt,PendingOutcome;internal ItemActionEntryRepair Entry;internal bool NativePaid,PaidAccepted;internal string PendingExpected;internal World World;internal string Directory;internal EntityPlayerLocal Player;internal float Next;}
    private sealed class Upload
    {internal World World;internal EntityPlayer Player;internal ClientInfo Sender;internal PlayerDataFile Source;internal MemoryStream BagSource,BeltSource,EquipmentSource;internal ItemStack CursorSource;internal string Creation,Directory;internal int Deaths;internal ItemStack[] Bag,Belt,Equipment;internal string[] Queue,QueueOperations,NativeQueueItems;internal string CursorOperation;internal bool HeadFrozen;internal string BagHash,BeltHash,EquipmentHash,CursorImage;}
    
    private static readonly Dictionary<Guid,Client> Clients=new Dictionary<Guid,Client>();
    private static readonly Dictionary<int,Upload> Uploads=new Dictionary<int,Upload>();
    private static World activeWorld;private static string activeDirectory;private static float nextScan;
    internal static string Image(ItemValue item)=>RebirthNativeItemCodec.Encode(item);
    // Repair custody never resolves an account through the entity-ID cache.
    // Local platform and actual current authenticated peer are separate roots.
    private static bool TryRepairIdentity(EntityPlayer player,out RebirthStablePlayerIdentity identity)
    {
        identity=null;var world=GameManager.Instance?.World;
        if(player==null||world==null||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player))return false;
        if(player is EntityPlayerLocal)
            return ReferenceEquals(world.GetPrimaryPlayer(),player)&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity);
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(player.entityId);
        return peer!=null&&peer.entityId==player.entityId&&RebirthStablePlayerIdentity.TryFromClientInfo(peer,out identity);
    }
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
    // Preserve the entire native repaired item except the one supported raw
    // DegradationMax row, owned durability/applied markers, and the two fetch
    // markers only when the server attests their durable terminal retirement.
    internal static bool TryMerge(ItemValue current,Receipt r,out ItemValue merged)
    {
        merged=null;try
        {
            if(r.BaseOnly||r.BaseOnlyConfirmed)return false;
            var before=ReadItem(r.Before);var after=ReadItem(r.After);
            if(!SameOperation(before,r.Operation,OriginalKey)||!SameOperation(after,r.Operation,OriginalKey)||!SameOperation(after,r.Operation,AppliedKey)||after.MaxUseTimes<before.MaxUseTimes)return false;
            bool already=SameOperation(current,r.Operation,AppliedKey);
            if(!(already?SameExceptWear(current,after):SameExceptWear(current,before)))return false;
            bool changed=after.MaxUseTimes!=before.MaxUseTimes;Array bs=null,af=null;int bi=-1,ai=-1;MemberInfo bm=null,am=null;
            bool retiredFetch=false;
            if(r.Payment.Length!=0&&!after.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)&&!after.HasMetadata(RebirthRemoteResourceOwnerAction.BindingMarker))
            {
                if(r.PaymentProof.Length!=64||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(r.Payment,out var payment)||!before.TryGetMetadata(RebirthRemoteResourceOwnerAction.Marker,out string source)||source!=payment.SourceOperation.ToString("N")||!before.TryGetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,out string binding)||binding!=payment.Binding)return false;
                retiredFetch=true;
            }
            if(changed&&(!Stat(before,out bs,out bi,out bm)||!Stat(after,out af,out ai,out am)||bi!=ai||bs.Length!=af.Length))return false;
            var normalized=ReadItem(Image(after));if(changed){if(!Stat(normalized,out var ns,out int ni,out var nm))return false;object originalRow=ns.GetValue(ni);Set(nm,originalRow,Get(bm,bs.GetValue(bi)));ns.SetValue(originalRow,ni);}
            if(before.TryGetMetadata(AppliedKey,out string previousApplied))normalized.SetMetadata(AppliedKey,previousApplied);else normalized.RemoveMetaData(AppliedKey);
            if(before.TryGetMetadata("RebirthDurabilityOriginalMaxUseTimes",out int previousCap))normalized.SetMetadata("RebirthDurabilityOriginalMaxUseTimes",previousCap);else normalized.RemoveMetaData("RebirthDurabilityOriginalMaxUseTimes");
            var normalizedBefore=ReadItem(Image(before));if(retiredFetch){normalizedBefore.RemoveMetaData(RebirthRemoteResourceOwnerAction.Marker);normalizedBefore.RemoveMetaData(RebirthRemoteResourceOwnerAction.BindingMarker);}
            if(Image(normalized)!=Image(normalizedBefore))return false;
            if(already){merged=ReadItem(Image(current));return true;}
            var result=ReadItem(Image(current));if(changed){if(!Stat(result,out var rs,out int ri,out var rm)||ri!=ai)return false;object resultRow=rs.GetValue(ri);Set(rm,resultRow,Get(am,af.GetValue(ai)));rs.SetValue(resultRow,ri);}
            if(retiredFetch){result.RemoveMetaData(RebirthRemoteResourceOwnerAction.Marker);result.RemoveMetaData(RebirthRemoteResourceOwnerAction.BindingMarker);}
            if(after.TryGetMetadata("RebirthDurabilityOriginalMaxUseTimes",out int cap))result.SetMetadata("RebirthDurabilityOriginalMaxUseTimes",cap);
            result.SetMetadata(AppliedKey,r.Operation.ToString("N"));var expected=ReadItem(Image(after));expected.UseTimes=current.UseTimes;
            if(Image(expected)!=Image(result)||result.MaxUseTimes!=after.MaxUseTimes||result.UseTimes!=current.UseTimes)return false;merged=result;return true;
        }catch{return false;}
    }
    // Ended-life base settlement changes only positively terminal fetch markers.
    internal static bool TryMergeBaseOnly(ItemValue current,Receipt r,out ItemValue merged)
    {
        merged=null;try
        {
            if(r.BaseOnly&&r.Admitted&&!r.Published&&!r.Cancelled&&r.Payment.Length==0&&r.PaymentProof.Length==0&&r.PaidInput.Length==0&&r.Before.Length!=0&&r.Before==r.After&&
                SameOperation(ReadItem(r.Before),r.Operation,OriginalKey)&&SameExceptWear(ReadItem(r.Before),ReadItem(r.Expected))&&SameExceptWear(current,ReadItem(r.Before)))
            {merged=ReadItem(Image(current));return true;} // Already-paid native base; no item correction.
            if(!r.BaseOnly||!r.Admitted||r.Published||r.Cancelled||r.PaymentProof.Length!=64||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(r.Payment,out var payment))return false;
            var before=ReadItem(r.Before);var after=ReadItem(r.After);
            if(!SameOperation(before,r.Operation,OriginalKey)||!SameOperation(after,r.Operation,OriginalKey)||!SameExceptWear(before,ReadItem(r.Expected))||!before.TryGetMetadata(RebirthRemoteResourceOwnerAction.Marker,out string source)||source!=payment.SourceOperation.ToString("N")||!before.TryGetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,out string binding)||binding!=payment.Binding)return false;
            var clean=ReadItem(Image(before));clean.RemoveMetaData(RebirthRemoteResourceOwnerAction.Marker);clean.RemoveMetaData(RebirthRemoteResourceOwnerAction.BindingMarker);
            if(Image(clean)!=Image(after))return false;
            if(SameExceptWear(current,after)){merged=ReadItem(Image(current));return true;}
            if(!SameExceptWear(current,before))return false;
            merged=ReadItem(Image(current));merged.RemoveMetaData(RebirthRemoteResourceOwnerAction.Marker);merged.RemoveMetaData(RebirthRemoteResourceOwnerAction.BindingMarker);return true;
        }catch{return false;}
    }
    internal static bool ValidNativeOutput(Receipt r,ItemValue output)
    {
        try
        {
            var input=ReadItem(r.Input);if(!SameOperation(input,r.Operation,OriginalKey)||!SameOperation(output,r.Operation,OriginalKey)||r.Amount<=0||r.Amount>ushort.MaxValue)return false;
            var expected=ReadItem(Image(input));ItemAction.HandleDegradation(expected,null);expected.UseTimes=Math.Max(0f,expected.UseTimes-r.Amount);
            return output.UseTimes<input.UseTimes&&Image(expected)==Image(output);
        }catch{return false;}
    }
    private static string Encode(Receipt r)=>new XElement("repairOriginal",new XAttribute("version",1),new XAttribute("operation",r.Operation.ToString("N")),new XAttribute("world",r.World),new XAttribute("owner",r.Owner),new XAttribute("creation",r.Creation),new XAttribute("deaths",r.Deaths),new XAttribute("amount",r.Amount),new XAttribute("admitted",r.Admitted),new XAttribute("cancelled",r.Cancelled),new XAttribute("cancelConfirmed",r.CancelConfirmed),new XAttribute("published",r.Published),r.BaseOnly?new XAttribute("baseOnly",true):null,r.BaseOnlyConfirmed?new XAttribute("baseOnlyConfirmed",true):null,r.CancelReturned?new XAttribute("cancelReturned",true):null,new XAttribute("invoking",r.OutputInvoking),new XAttribute("nativeDelivered",r.NativeDelivered),new XElement("input",r.Input),new XElement("expected",r.Expected),new XElement("before",r.Before),new XElement("after",r.After),r.Payment.Length==0?null:new XElement("payment",r.Payment),r.PaidInput.Length==0?null:new XElement("paidInput",r.PaidInput),r.PaymentProof.Length==0?null:new XElement("paymentProof",r.PaymentProof),r.CancelInput.Length==0?null:new XElement("cancelInput",r.CancelInput)).ToString(SaveOptions.DisableFormatting);
    private static Receipt Decode(string image)
    {
        if(image==null||image.Length>MaximumImage*8)throw new InvalidDataException();XElement e;
        using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumImage*8}))e=XElement.Load(reader);
        if(e.Name!="repairOriginal"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();
        var r=new Receipt{Operation=Guid.ParseExact((string)e.Attribute("operation"),"N"),World=(string)e.Attribute("world"),Owner=(string)e.Attribute("owner"),Creation=(string)e.Attribute("creation"),Deaths=(int)e.Attribute("deaths"),Amount=(int)e.Attribute("amount"),Admitted=(bool)e.Attribute("admitted"),Cancelled=(bool)e.Attribute("cancelled"),CancelConfirmed=(bool)e.Attribute("cancelConfirmed"),Published=(bool)e.Attribute("published"),BaseOnly=(bool?)e.Attribute("baseOnly")??false,BaseOnlyConfirmed=(bool?)e.Attribute("baseOnlyConfirmed")??false,Input=(string)e.Element("input"),Expected=(string)e.Element("expected"),OutputInvoking=(bool)e.Attribute("invoking"),NativeDelivered=(bool)e.Attribute("nativeDelivered"),Before=(string)e.Element("before"),After=(string)e.Element("after"),Payment=(string)e.Element("payment")??string.Empty,PaidInput=(string)e.Element("paidInput")??string.Empty,PaymentProof=(string)e.Element("paymentProof")??string.Empty,CancelInput=(string)e.Element("cancelInput")??string.Empty};
        r.CancelReturned=(bool?)e.Attribute("cancelReturned")??false;
        if(r.Operation==Guid.Empty||!Guid.TryParse(r.World,out _)||string.IsNullOrEmpty(r.Owner)||r.Owner.Length>512||string.IsNullOrEmpty(r.Creation)||r.Creation.Length>512||r.Deaths<0||r.Amount<=0||r.Amount>ushort.MaxValue||r.CancelReturned&&!r.Cancelled||Encode(r)!=image)throw new InvalidDataException();
        var input=ReadItem(r.Input);if(input.UseTimes<=0||!SameOperation(input,r.Operation,OriginalKey)||!input.TryGetMetadata(WorldKey,out string world)||world!=r.World||!input.TryGetMetadata(CreationKey,out string creation)||creation!=r.Creation||!input.TryGetMetadata(OwnerKey,out string owner)||owner!=r.Owner||!input.TryGetMetadata(DeathsKey,out int deaths)||deaths!=r.Deaths)throw new InvalidDataException();
        if(r.Payment.Length==0)
        {if(r.PaidInput.Length!=0||r.PaymentProof.Length!=0||r.CancelInput.Length!=0)throw new InvalidDataException();}
        else
        {
            if(!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(r.Payment,out var payment)||payment.World!=r.World||payment.CanonicalOwner!=r.Owner||payment.Creation!=r.Creation||payment.Deaths!=r.Deaths||r.PaidInput.Length==0||RebirthRemoteResourceDurableJournal.Digest(r.PaidInput)!=payment.InputHash||r.Admitted&&r.PaymentProof.Length==0||r.PaymentProof.Length!=0&&(!r.Admitted||r.PaymentProof.Length!=64||r.PaymentProof.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f'))))throw new InvalidDataException();
            var bound=ReadItem(r.PaidInput);if(bound.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)||bound.HasMetadata(RebirthRemoteResourceOwnerAction.BindingMarker))throw new InvalidDataException();
            bound.SetMetadata(RebirthRemoteResourceOwnerAction.Marker,payment.SourceOperation.ToString("N"));bound.SetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,payment.Binding);
            RebirthItemProvenanceAdapter.EnsureDurabilityOriginalMaxUseTimes(bound,bound.MaxUseTimes);
            bound.SetMetadata(OriginalKey,r.Operation.ToString("N"));bound.SetMetadata(WorldKey,r.World);bound.SetMetadata(CreationKey,r.Creation);bound.SetMetadata(OwnerKey,r.Owner);bound.SetMetadata(DeathsKey,r.Deaths);
            bound.SetMetadata(PaidKitTypeKey,payment.KitType);bound.SetMetadata(PaidKitCountKey,payment.TotalKits);
            if(Image(bound)!=r.Input)throw new InvalidDataException();
            if(!ValidCancellationInput(r))throw new InvalidDataException();
        }
        if(r.Expected.Length!=0&&!ValidNativeOutput(r,ReadItem(r.Expected))||r.BaseOnly&&(r.Published||r.Cancelled||r.OutputInvoking||!r.NativeDelivered||!r.Admitted||(r.Payment.Length==0?r.PaymentProof.Length!=0||r.PaidInput.Length!=0:r.PaymentProof.Length!=64)||r.Before.Length==0||r.After.Length==0||!SameExceptWear(ReadItem(r.Before),ReadItem(r.Expected))||!TryMergeBaseOnly(ReadItem(r.Before),r,out _))||r.BaseOnlyConfirmed&&!r.BaseOnly||!r.BaseOnly&&(r.Before.Length!=0&&r.Before!=r.Expected||r.Before.Length!=0&&!ValidNativeOutput(r,ReadItem(r.Before))||r.After.Length!=0&&(r.Before.Length==0||!TryMerge(ReadItem(r.Before),r,out _)))||r.Published&&r.After.Length==0||r.CancelConfirmed&&!r.Cancelled||(r.OutputInvoking||r.NativeDelivered)&&r.Before.Length==0||r.OutputInvoking&&r.NativeDelivered)throw new InvalidDataException();return r;
    }
    internal static bool ValidCancellationInput(Receipt r)
    {
        try
        {
            if(r.CancelInput.Length==0)return true;
            if(r.Payment.Length==0||!r.Admitted||r.PaymentProof.Length!=64||r.PaymentProof.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f')))return false;
            var cancelled=ReadItem(r.Input);if(!cancelled.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)||!cancelled.HasMetadata(RebirthRemoteResourceOwnerAction.BindingMarker))return false;
            cancelled.RemoveMetaData(RebirthRemoteResourceOwnerAction.Marker);cancelled.RemoveMetaData(RebirthRemoteResourceOwnerAction.BindingMarker);return Image(cancelled)==r.CancelInput;
        }catch{return false;}
    }
    private static string PathFor(Receipt r,bool server,string directory=null)=>Path.Combine(directory??GameIO.GetSaveGameDir(),"rebirth","repair-original",server?"server":"owner",r.Operation.ToString("N")+".xml");
    private static bool Read(Receipt key,bool server,out Receipt receipt,string directory=null)
    {receipt=null;try{string path=PathFor(key,server,directory);if(new FileInfo(path).Length>MaximumImage*12L)return false;var r=Decode(ReadRepairText(path));if(r.Operation!=key.Operation||r.World!=key.World||r.Owner!=key.Owner||r.Creation!=key.Creation||r.Deaths!=key.Deaths||r.Input!=key.Input||r.Expected!=key.Expected||r.Amount!=key.Amount||r.Payment!=key.Payment||r.PaidInput!=key.PaidInput)return false;receipt=r;return true;}catch{return false;}}
    private static bool Save(Receipt r,bool server,string expected=null,string directory=null)
    {
        string path=PathFor(r,server,directory);
        try
        {
            string image=Encode(r);Decode(image);
            if(File.Exists(path)||File.Exists(path+".bak"))
            {
                if(!Read(r,server,out var prior,directory))return false;string current=Encode(prior);
                if(current==image){ReleaseCommittedRepairCandidate(path,image);return true;}
                if(expected==null||current!=expected)return false;
            }
            else if(expected!=null)return false;
            return PublishRepairCandidate(path,image,expected);
        }
        catch{return false;}
    }
    private static void WorldScope(World world)
    {if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(GameManager.Instance?.World,world))return;string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(activeWorld,world)&&activeDirectory==directory)return;activeWorld=world;activeDirectory=directory;Cards.Clear();Staged.Clear();PreparedRemote.Clear();activation=null;Clients.Clear();Uploads.Clear();ownedCandidates.Clear();nextScan=0;}
    private static bool Current(Client c)=>ReferenceEquals(GameManager.Instance?.World,c.World)&&GameIO.GetSaveGameDir()==c.Directory&&ReferenceEquals(c.World.GetPrimaryPlayer(),c.Player)&&ReferenceEquals(c.Player.world,c.World)&&ReferenceEquals(c.World.GetEntity(c.Player.entityId),c.Player)&&c.Player.IsSpawned()&&!c.Player.IsDead()&&c.Player.Died==c.Receipt.Deaths&&c.World.worldState?.Guid==c.Receipt.World&&RebirthCamperCharacterAccess.CurrentCreation(c.Player)==c.Receipt.Creation&&TryRepairIdentity(c.Player,out var identity)&&identity.CanonicalId==c.Receipt.Owner;
    private static bool SamePhysicalActor(Client c)=>ReferenceEquals(GameManager.Instance?.World,c.World)&&GameIO.GetSaveGameDir()==c.Directory&&ReferenceEquals(c.World.GetPrimaryPlayer(),c.Player)&&ReferenceEquals(c.World.GetEntity(c.Player.entityId),c.Player)&&!c.Player.IsDead()&&c.Player.Died>=c.Receipt.Deaths&&c.World.worldState?.Guid==c.Receipt.World&&RebirthCamperCharacterAccess.CurrentCreation(c.Player)==c.Receipt.Creation&&TryRepairIdentity(c.Player,out var identity)&&identity.CanonicalId==c.Receipt.Owner;
    private static string Preview(Receipt r){var item=ReadItem(r.Input);ItemAction.HandleDegradation(item,null);item.UseTimes=Math.Max(0f,item.UseTimes-r.Amount);return Image(item);}
    private static int NativeKitCount(EntityPlayerLocal player,int type)
    {return checked(player.bag.ItemGrid.items.Concat(player.inventory.ItemGrid.items).Where(s=>s!=null&&!s.IsEmpty()&&s.itemValue.type==type).Sum(s=>s.count));}
    // Pure pre-source-debit support check. No operation ID, marker, prepared
    // object, journal or native effect is created by this method.
    internal static bool CanPrepareRemoteOriginal(ItemActionEntryRepair entry,int kitType,int totalKits)
        =>CanPrepareRemoteOriginalCore(entry,kitType,totalKits,true,out _);
    internal static bool TryAssessRemoteOriginal(ItemActionEntryRepair entry,int kitType,int totalKits,out Guid priorOperation)
        =>CanPrepareRemoteOriginalCore(entry,kitType,totalKits,false,out priorOperation);
    private static bool CanPrepareRemoteOriginalCore(ItemActionEntryRepair entry,int kitType,int totalKits,bool requireLocalTerminal,out Guid priorOperation)
    {
        priorOperation=Guid.Empty;
        try
        {
            if(!ThreadManager.IsMainThread()||entry?.ItemController==null||totalKits<=0)return false;
            var controller=entry.ItemController;var player=controller.xui?.playerUI?.entityPlayer;
            var stack=controller is XUiC_EquipmentStack equipment?equipment.ItemStack:(controller as IItemStackController)?.ItemStack;
            if(player==null||player.IsDead()||!player.IsSpawned()||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||stack==null||stack.count!=1||!TryRepairIdentity(player,out var owner))return false;
            string image=Image(stack.itemValue);var item=ReadItem(image);var tools=item.ItemClass.RepairTools;
            var kit=tools==null||tools.Length==0?null:ItemClass.GetItemClass(tools[0].Value);
            if(item.UseTimes<=0||kit==null||kit.Id!=kitType||kit.RepairAmount.Value<=0||totalKits>Math.Ceiling((float)Mathf.CeilToInt(item.UseTimes)/kit.RepairAmount.Value)||item.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)||item.HasMetadata(RebirthRemoteResourceOwnerAction.BindingMarker))return false;
            if(item.HasMetadata(OriginalKey))
            {
                if(!item.TryGetMetadata(OriginalKey,out string old)||!Guid.TryParseExact(old,"N",out var priorId)||priorId==Guid.Empty)return false;
                priorOperation=priorId;
                if(requireLocalTerminal)
                {
                string path=PathFor(new Receipt{Operation=priorId},false);if(!File.Exists(path)&&File.Exists(path+".done"))path+=".done";
                bool terminal=false;
                if(File.Exists(path))
                {
                    if(new FileInfo(path).Length>MaximumImage*12L)return false;
                    var prior=Decode(ReadRepairText(path));
                    terminal=prior.Owner==owner.CanonicalId&&prior.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&MatchesPriorTerminal(item,prior,player.world.worldState.Guid);
                }
                if(!terminal&&(!RebirthRemoteResourceRepairProtocol.TryGetAuthenticatedPriorRepairTerminal(entry,image,out var authenticatedPrior,out string proof)||authenticatedPrior!=priorId||proof?.Length!=64||proof.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f'))))return false;
                }
            }
            return SupportsOriginalMetadata(item,owner.CanonicalId,RebirthCamperCharacterAccess.CurrentCreation(player),player.world.worldState.Guid)&&TryCalculateNativeAmount(player,item,kit,totalKits,out _);
        }catch{return false;}
    }
    // Host terminal lookup is not current item custody authority: C must bind
    // this exact input to its same-file source slot BEFORE source admission.
    internal static bool TryReadPriorRepairTerminal(EntityPlayer actor,string currentPaidInput,out string terminalProof)
    {
        terminalProof=null;
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor==null||actor.IsDead()||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!TryRepairIdentity(actor,out _))return false;
            var item=ReadItem(currentPaidInput);if(item.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)||item.HasMetadata(RebirthRemoteResourceOwnerAction.BindingMarker))return false;
            if(!item.HasMetadata(OriginalKey)){terminalProof=string.Empty;return true;}
            if(!item.TryGetMetadata(OriginalKey,out string marker)||!Guid.TryParseExact(marker,"N",out var operation)||operation==Guid.Empty)return false;
            string path=PathFor(new Receipt{Operation=operation},true);if(!File.Exists(path)&&File.Exists(path+".done"))path+=".done";
            if(!File.Exists(path)||new FileInfo(path).Length>MaximumImage*12L)return false;
            var prior=Decode(ReadRepairText(path));
            if(prior.Operation!=operation||!MatchesPriorTerminal(item,prior,world.worldState.Guid))return false;
            terminalProof=RebirthRemoteResourceDurableJournal.Digest(new XElement("priorRepairTerminal",new XAttribute("operation",operation.ToString("N")),new XElement("terminal",Encode(prior)),new XElement("currentInput",currentPaidInput)).ToString(SaveOptions.DisableFormatting));return true;
        }catch{terminalProof=null;return false;}
    }
    internal static bool MatchesPriorTerminal(ItemValue item,Receipt prior,string world)
    {
        try{return item!=null&&prior!=null&&SameOperation(item,prior.Operation,OriginalKey)&&prior.World==world&&item.TryGetMetadata(WorldKey,out string oldWorld)&&oldWorld==prior.World&&item.TryGetMetadata(OwnerKey,out string oldOwner)&&oldOwner==prior.Owner&&item.TryGetMetadata(CreationKey,out string oldCreation)&&oldCreation==prior.Creation&&item.TryGetMetadata(DeathsKey,out int oldDeaths)&&oldDeaths==prior.Deaths&&((prior.Published||prior.BaseOnlyConfirmed)&&prior.After.Length!=0&&SameExceptWear(item,ReadItem(prior.After))||prior.CancelConfirmed&&SameExceptWear(item,ReadItem(prior.CancelInput.Length==0?prior.Input:prior.CancelInput)));}catch{return false;}
    }
    internal static bool SupportsOriginalMetadata(ItemValue item,string owner,string creation,string world)
    {
        try
        {
            if(item==null||string.IsNullOrEmpty(owner)||owner.Length>512||string.IsNullOrEmpty(creation)||creation.Length>512||!Guid.TryParse(world,out _))return false;
            var keys=new[]{OriginalKey,AppliedKey,WorldKey,CreationKey,OwnerKey,DeathsKey,PaidKitTypeKey,PaidKitCountKey,"RebirthDurabilityOriginalMaxUseTimes",RebirthRemoteResourceOwnerAction.Marker,RebirthRemoteResourceOwnerAction.BindingMarker};
            if((item.Metadata?.Count??0)+keys.Count(key=>!item.HasMetadata(key))>byte.MaxValue)return false;
            // Bounds are serialization overhead, not fictitious authority IDs.
            // Reserve full fixed GUID/binding strings plus actual scope lengths;
            // include conservative per-entry length/type framing and int values.
            int overhead=checked(keys.Sum(key=>Encoding.UTF8.GetByteCount(key)+16)+32+32+32+64+Encoding.UTF8.GetByteCount(world)+Encoding.UTF8.GetByteCount(owner)+Encoding.UTF8.GetByteCount(creation)+64);
            return checked(Convert.FromBase64String(Image(item)).Length+overhead)<=MaximumImage/4*3;
        }catch{return false;}
    }
    // Prediction of the actual installed native recipe calculation, not payment
    // authority. The real SetRepairRecipe amount must still match on invocation.
    internal static bool TryCalculateRemoteOriginalAmount(ItemActionEntryRepair entry,string encodedPayment,out int amount)
    {
        amount=0;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(encodedPayment,out var payment))return false;
            var controller=entry?.ItemController;var player=controller?.xui?.playerUI?.entityPlayer;
            var stack=controller is XUiC_EquipmentStack equipment?equipment.ItemStack:(controller as IItemStackController)?.ItemStack;
            if(player==null||stack==null||stack.count!=1||!Current(new Client{Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir(),Receipt=new Receipt{World=payment.World,Owner=payment.CanonicalOwner,Creation=payment.Creation,Deaths=payment.Deaths}}))return false;
            var item=ReadItem(Image(stack.itemValue));var itemClass=ItemClass.GetForId(item.type);var tools=itemClass?.RepairTools;if(item.UseTimes<=0||tools==null||tools.Length==0)return false;
            var kit=ItemClass.GetItemClass(tools[0].Value);if(kit==null||kit.RepairAmount.Value<=0||kit.Id!=payment.KitType||payment.TotalKits<=0||payment.TotalKits>Math.Ceiling((float)Mathf.CeilToInt(item.UseTimes)/kit.RepairAmount.Value))return false;
            return TryCalculateNativeAmount(player,item,kit,payment.TotalKits,out amount);
        }catch{return false;}
    }
    private static bool TryCalculateNativeAmount(EntityPlayerLocal player,ItemValue item,ItemClass kit,int totalKits,out int amount)
    {
        amount=0;try
        {
            var recipe=new Recipe{count=1,craftExpGain=Mathf.CeilToInt(item.ItemClass.RepairExpMultiplier*totalKits),itemValueType=item.type,craftingTime=kit.RepairTime.Value*totalKits};recipe.ingredients.Add(new ItemStack(new ItemValue(kit.Id),totalKits));
            float calculated=EffectManager.GetValue(PassiveEffects.RepairAmount,null,checked(totalKits*kit.RepairAmount.Value),player,recipe,FastTags<TagGroup.Global>.Parse(recipe.GetName()));
            if(float.IsNaN(calculated)||float.IsInfinity(calculated)||calculated<1||calculated>ushort.MaxValue)return false;
            float nativeTime=EffectManager.GetValue(PassiveEffects.CraftingTime,null,recipe.craftingTime,player,recipe,FastTags<TagGroup.Global>.Parse(recipe.GetName()));
            if(float.IsNaN(nativeTime)||float.IsInfinity(nativeTime)||nativeTime<int.MinValue||nativeTime>=int.MaxValue)return false;
            float queueTime=(int)nativeTime;int queueAmount=(int)calculated;
            if(RebirthSurvivorMode.IsEnabledForCurrentWorld())RebirthServiceCraftSkillService.AdjustRepairQueue(player,item,ref queueTime,ref queueAmount);
            if(queueAmount<1||queueAmount>ushort.MaxValue)return false;amount=queueAmount;return true;
        }catch{return false;}
    }
    internal static bool TryPrepareRemoteOriginal(ItemActionEntryRepair entry,string payment,string paidInput,int amount,out Guid operation,out string queuedInput)
    {
        operation=Guid.Empty;queuedInput=null;
        try
        {
            if(!TryCalculateRemoteOriginalAmount(entry,payment,out int actual)||actual!=amount||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(payment,out var cost)||!CanPrepareRemoteOriginal(entry,cost.KitType,cost.TotalKits)||RebirthRemoteResourceDurableJournal.Digest(paidInput)!=cost.InputHash)return false;
            var player=entry.ItemController.xui.playerUI.entityPlayer;WorldScope((World)player.world);
            var controller=entry.ItemController;var original=controller is XUiC_EquipmentStack equipment?equipment.ItemStack:(controller as IItemStackController)?.ItemStack;if(original==null||original.count!=1||Image(original.itemValue)!=paidInput)return false;
            if(PreparedRemote.TryGetValue(entry,out var existing))
            {if(existing.Receipt.Payment!=payment||existing.Receipt.PaidInput!=paidInput||existing.Receipt.Amount!=amount)return false;operation=existing.Receipt.Operation;queuedInput=existing.Receipt.Input;return true;}
            if(PreparedRemote.Count>=16)return false;Guid id=Guid.NewGuid();var staged=ReadItem(paidInput);if(staged.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)||staged.HasMetadata(RebirthRemoteResourceOwnerAction.BindingMarker))return false;
            staged.SetMetadata(RebirthRemoteResourceOwnerAction.Marker,cost.SourceOperation.ToString("N"));staged.SetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,cost.Binding);RebirthItemProvenanceAdapter.EnsureDurabilityOriginalMaxUseTimes(staged,staged.MaxUseTimes);
            staged.SetMetadata(OriginalKey,id.ToString("N"));staged.SetMetadata(WorldKey,cost.World);staged.SetMetadata(CreationKey,cost.Creation);staged.SetMetadata(OwnerKey,cost.CanonicalOwner);staged.SetMetadata(DeathsKey,cost.Deaths);
            staged.SetMetadata(PaidKitTypeKey,cost.KitType);staged.SetMetadata(PaidKitCountKey,cost.TotalKits);
            var receipt=new Receipt{Operation=id,World=cost.World,Owner=cost.CanonicalOwner,Creation=cost.Creation,Deaths=cost.Deaths,Amount=amount,Input=Image(staged),Payment=payment,PaidInput=paidInput};receipt.Expected=Preview(receipt);Decode(Encode(receipt));
            PreparedRemote.Add(entry,new Prepared{Entry=entry,Receipt=receipt,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir()});operation=id;queuedInput=receipt.Input;return true;
        }catch{return false;}
    }
    internal static bool TryRestorePreparedRemoteOriginal(ItemActionEntryRepair entry,string payment,string paidInput,string queuedInput,int amount,out Guid operation)
    {
        operation=Guid.Empty;
        try
        {
            if(!TryCalculateRemoteOriginalAmount(entry,payment,out int actual)||actual!=amount||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(payment,out var cost)||!CanPrepareRemoteOriginal(entry,cost.KitType,cost.TotalKits))return false;
            var item=ReadItem(queuedInput);if(!item.TryGetMetadata(OriginalKey,out string marker)||!Guid.TryParseExact(marker,"N",out var id)||id==Guid.Empty)return false;
            var player=entry.ItemController.xui.playerUI.entityPlayer;WorldScope((World)player.world);var controller=entry.ItemController;var stack=controller is XUiC_EquipmentStack equipment?equipment.ItemStack:(controller as IItemStackController)?.ItemStack;
            if(stack==null||stack.count!=1||Image(stack.itemValue)!=paidInput)return false;
            var receipt=new Receipt{Operation=id,World=cost.World,Owner=cost.CanonicalOwner,Creation=cost.Creation,Deaths=cost.Deaths,Amount=amount,Input=queuedInput,Payment=payment,PaidInput=paidInput};receipt.Expected=Preview(receipt);Decode(Encode(receipt));
            if(PreparedRemote.TryGetValue(entry,out var existing)){if(Encode(existing.Receipt)!=Encode(receipt))return false;}else {if(PreparedRemote.Count>=16)return false;PreparedRemote.Add(entry,new Prepared{Entry=entry,Receipt=receipt,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir()});}
            operation=id;return true;
        }catch{return false;}
    }
    internal static void ReleaseRemotePreparation(ItemActionEntryRepair entry,string payment)
    {if(entry!=null&&PreparedRemote.TryGetValue(entry,out var prepared)&&prepared.Receipt.Payment==payment)PreparedRemote.Remove(entry);}
    internal static object BeginActivation(ItemActionEntryRepair entry)
    {
        var prior=activation;activation=null;
        try
        {
            var controller=entry.ItemController;var owner=controller?.xui?.playerUI?.entityPlayer;
            if(owner==null)return prior;WorldScope((World)owner.world);
            var equipment=controller as XUiC_EquipmentStack;var itemController=controller as IItemStackController;var stack=equipment!=null?equipment.ItemStack:itemController?.ItemStack;
            var item=stack?.itemValue;var tools=item?.ItemClass?.RepairTools;if(item==null||stack.count!=1||item.UseTimes<=0||tools==null||tools.Length==0)return prior;
            var kit=ItemClass.GetItemClass(tools[0].Value);if(kit==null)return prior;
            int before=NativeKitCount(owner,kit.Id);int needed=Convert.ToInt32(Math.Ceiling((float)Mathf.CeilToInt(item.UseTimes)/(float)kit.RepairAmount.Value));
            // Match the native original's requested quantity only. Projected
            // availability is NEVER evidence that any material was paid.
            int total=Math.Min(controller.xui.PlayerInventory.GetItemCount(new ItemValue(kit.Id)),needed);string paidInput=Image(item),paymentText=string.Empty;int local=total;
            if(RebirthRemoteResourceOwnerAction.TryCaptureRepairPaymentIntent(entry,paidInput,total,out var payment))
            {if(payment.KitType!=kit.Id||payment.LocalKits>before||payment.TotalKits!=total||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(payment.Encode(),out _))return prior;paymentText=payment.Encode();local=payment.LocalKits;}
            else if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||total!=Math.Min(before,needed))return prior;
            activation=new Activation{Entry=entry,Player=owner,Controller=itemController,Equipment=equipment,KitType=kit.Id,BeforeKits=before,ExpectedKits=local,TotalKits=total,NativeInput=paidInput,Payment=paymentText,PaidInput=paymentText.Length==0?string.Empty:paidInput};
        }catch{activation=null;}return prior;
    }
    internal static bool ShouldAdjustNativeRepairQueue(EntityPlayer player,ItemValue item)
    {
        if(item==null||!item.HasMetadata(OriginalKey))return true;
        // Restored cards already contain their saved adjusted time/amount. A
        // fresh repair of a previously repaired tool still gets one adjustment.
        try
        {
            if(activation!=null&&ReferenceEquals(activation.Player,player)&&activation.NativeInput==Image(item))return true;
            if(player is not EntityPlayerLocal local||!item.TryGetMetadata(OriginalKey,out string marker)||!Guid.TryParseExact(marker,"N",out var operation))return true;
            var path=PathFor(new Receipt{Operation=operation},false);if(!File.Exists(path)||new FileInfo(path).Length>MaximumImage*12L)return true;
            var stored=Decode(ReadRepairText(path));var restored=new Client{Receipt=stored,Player=local,World=(World)local.world,Directory=GameIO.GetSaveGameDir()};
            return stored.Input!=Image(item)||!(Current(restored)||SamePhysicalActor(restored));
        }catch{return true;}
    }
    internal static void EndActivation(object prior,Exception failure,bool nativeRan=true)
    {
        var captured=activation;activation=prior as Activation;if(captured==null)return;
        foreach(var c in captured.Queued)
        {
            try
            {
                var p=captured.Player;bool sourceEmpty=captured.Equipment!=null?captured.Equipment.ItemStack.IsEmpty():captured.Controller!=null&&captured.Controller.ItemStack.IsEmpty();
                // Native OnActivated has now removed the exact source controller
                // and actually debited its repair materials. No cost is repeated.
                bool paid=nativeRan&&failure==null&&Current(c)&&sourceEmpty&&(captured.ExpectedKits>0||captured.Payment.Length!=0)&&NativeKitCount(p,captured.KitType)==captured.BeforeKits-captured.ExpectedKits;
                if(!paid)continue;c.NativePaid=true;Clients[c.Receipt.Operation]=c;if(Save(c.Receipt,false)){c.PaidAccepted=true;UploadOwner(p);}
            }catch(Exception error){Log.Warning("[REBIRTH Repair] original paid action retained: "+error.GetType().Name);}
        }
    }
    internal static bool QueuePrefix(XUiC_RecipeStack card,ItemValue item,int amount)
    {
        bool remote=activation?.Payment.Length>0;
        try
        {
            var p=card?.xui?.playerUI?.entityPlayer;if(p==null||item==null||activation==null||!ReferenceEquals(activation.Player,p)||card.IsCrafting||card.originalItem!=null&&!card.originalItem.IsEmpty()||!TryRepairIdentity(p,out var identity))return true;
            if(amount<=0||amount>ushort.MaxValue)return !remote;
            WorldScope((World)p.world);if(Cards.Values.Count(Current)>=16&&!Cards.ContainsKey(card))return !remote;
            Guid op=Guid.NewGuid();Prepared prepared=null;var staged=ReadItem(Image(item));
            if(activation.Payment.Length!=0)
            {
                if(!PreparedRemote.TryGetValue(activation.Entry,out prepared)||!ReferenceEquals(prepared.Player,p)||!ReferenceEquals(prepared.World,p.world)||prepared.Directory!=GameIO.GetSaveGameDir()||!MatchesPreparedOriginal(prepared.Receipt,activation.Payment,activation.PaidInput,Image(item),amount)||!RebirthRemoteResourceOwnerAction.TryStampRepairQueueOriginal(staged,out var marked))return false;
                op=prepared.Receipt.Operation;staged=ReadItem(Image(marked));
            }
            RebirthItemProvenanceAdapter.EnsureDurabilityOriginalMaxUseTimes(staged,staged.MaxUseTimes);
            staged.SetMetadata(OriginalKey,op.ToString("N"));staged.SetMetadata(WorldKey,p.world.worldState.Guid);staged.SetMetadata(CreationKey,RebirthCamperCharacterAccess.CurrentCreation(p));staged.SetMetadata(OwnerKey,identity.CanonicalId);staged.SetMetadata(DeathsKey,p.Died);
            staged.SetMetadata(PaidKitTypeKey,activation.KitType);staged.SetMetadata(PaidKitCountKey,activation.TotalKits);
            Decode(Encode(new Receipt{Operation=op,World=p.world.worldState.Guid,Owner=identity.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(p),Deaths=p.Died,Amount=amount,Input=Image(staged),Payment=activation.Payment,PaidInput=activation.PaidInput}));
            if(prepared!=null&&Image(staged)!=prepared.Receipt.Input)return false;
            // Unsupported original images leave the native action unmarked.
            if(activation.Payment.Length!=0)foreach(string key in new[]{RebirthRemoteResourceOwnerAction.Marker,RebirthRemoteResourceOwnerAction.BindingMarker}){staged.TryGetMetadata(key,out string value);item.SetMetadata(key,value);}
            if(staged.TryGetMetadata("RebirthDurabilityOriginalMaxUseTimes",out int cap))item.SetMetadata("RebirthDurabilityOriginalMaxUseTimes",cap);
            foreach(string key in new[]{OriginalKey,WorldKey,CreationKey,OwnerKey}){staged.TryGetMetadata(key,out string value);item.SetMetadata(key,value);}item.SetMetadata(DeathsKey,p.Died);Staged[card]=op;
            item.SetMetadata(PaidKitTypeKey,activation.KitType);item.SetMetadata(PaidKitCountKey,activation.TotalKits);
            return true;
        }catch(Exception error){Log.Warning("[REBIRTH Repair] original queue capture refused: "+error.GetType().Name);return !remote;}
    }
    internal static bool MatchesPreparedOriginal(Receipt prepared,string payment,string paidInput,string actualInput,int amount)
    {
        return prepared!=null&&prepared.Operation!=Guid.Empty&&prepared.Payment.Length!=0&&prepared.Expected.Length!=0&&prepared.Payment==payment&&prepared.PaidInput==paidInput&&actualInput==paidInput&&amount>0&&prepared.Amount==amount;
    }
    internal static bool TryBindNativeRefund(XUiC_RecipeStack card,Receipt receipt)
    {
        try
        {
            if(card?.recipe==null||card.recipeCount!=1||receipt==null||Image(card.originalItem)!=receipt.Input)return false;
            var original=ReadItem(receipt.Input);
            if(!original.TryGetMetadata(PaidKitTypeKey,out int type)||!original.TryGetMetadata(PaidKitCountKey,out int count)||count<=0)return false;
            var tools=original.ItemClass.RepairTools;var kit=tools==null||tools.Length==0?null:ItemClass.GetItemClass(tools[0].Value);
            if(kit==null||kit.Id!=type||kit.RepairAmount.Value<=0||count>Math.Ceiling((float)Mathf.CeilToInt(original.UseTimes)/kit.RepairAmount.Value))return false;
            if(receipt.Payment.Length!=0&&(!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(receipt.Payment,out var payment)||payment.KitType!=type||payment.TotalKits!=count))return false;
            // Native OnActivated paid fresh kit ItemValues. Keep the native
            // cancellation delivery/drop path, changing only its exact quantity.
            card.recipe.ingredients.Clear();card.recipe.ingredients.Add(new ItemStack(new ItemValue(type),count));return true;
        }catch{return false;}
    }
    internal static void QueueAccepted(XUiC_RecipeStack card,bool accepted)
    {
        try
        {
            if(!accepted||card?.originalItem==null||!card.originalItem.TryGetMetadata(OriginalKey,out string key)||!Guid.TryParseExact(key,"N",out var op))return;
            var p=card.xui.playerUI.entityPlayer;WorldScope((World)p.world);
            if(activation!=null&&ReferenceEquals(activation.Player,p))
            {
                if(!Staged.TryGetValue(card,out var staged)||staged!=op)return;Staged.Remove(card);
                if(!TryRepairIdentity(p,out var identity))return;
                var r=new Receipt{Operation=op,World=p.world.worldState.Guid,Owner=identity.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(p),Deaths=p.Died,Amount=card.AmountToRepair,Input=Image(card.originalItem),Payment=activation.Payment,PaidInput=activation.PaidInput};r.Expected=Preview(r);Decode(Encode(r));
                if(activation.Payment.Length!=0)
                {if(!PreparedRemote.TryGetValue(activation.Entry,out var prepared)||prepared.Receipt.Operation!=op||prepared.Receipt.Input!=r.Input||prepared.Receipt.Amount!=r.Amount)return;PreparedRemote.Remove(activation.Entry);}
                if(!TryBindNativeRefund(card,r))return;
                var c=new Client{Receipt=r,Entry=activation.Entry,World=(World)p.world,Player=p,Directory=GameIO.GetSaveGameDir()};Cards[card]=c;activation.Queued.Add(c);return;
            }
            // Native reload/queue shift may recreate the UI card. Never mint a
            // new repair permission from a restored card or a type/seed match.
            var marker=card.originalItem;if(!marker.TryGetMetadata(WorldKey,out string world)||!marker.TryGetMetadata(OwnerKey,out string owner)||!marker.TryGetMetadata(CreationKey,out string creation)||!marker.TryGetMetadata(DeathsKey,out int deaths))return;
            var keyReceipt=new Receipt{Operation=op,World=world,Owner=owner,Creation=creation,Deaths=deaths};
            string path=PathFor(keyReceipt,false);if(!File.Exists(path)||new FileInfo(path).Length>MaximumImage*12L)return;var stored=Decode(ReadRepairText(path));var restored=new Client{Receipt=stored,World=(World)p.world,Player=p,Directory=GameIO.GetSaveGameDir()};
            if((Current(restored)||SamePhysicalActor(restored))&&Image(marker)==stored.Input&&(card.AmountToRepair==stored.Amount||card.AmountToRepair==0&&stored.Expected.Length!=0)){if(card.AmountToRepair>0&&!TryBindNativeRefund(card,stored))return;Cards[card]=restored;Clients[op]=restored;}
        }catch(Exception error){Log.Warning("[REBIRTH Repair] original queue restore retained: "+error.GetType().Name);}
    }
    internal static bool OutputPrefix(XUiC_RecipeStack card)
    {try{return OutputPrefixCore(card);}catch(Exception failure){Log.Warning("[REBIRTH Repair] original output held: "+failure.GetType().Name);return false;}}
    internal static bool TryGetCapturedRemoteOriginal(ItemActionEntryRepair entry,string payment,out Guid operation,out string queuedInput,out int amount)
    {
        operation=Guid.Empty;queuedInput=null;amount=0;
        try
        {
            if(entry==null||payment.Length==0||!ThreadManager.IsMainThread())return false;
            var matches=Cards.Where(pair=>ReferenceEquals(pair.Value.Entry,entry)&&pair.Value.NativePaid&&pair.Value.PaidAccepted&&Current(pair.Value)&&pair.Value.Receipt.Payment==payment&&pair.Key.AmountToRepair==pair.Value.Receipt.Amount&&pair.Key.originalItem!=null&&Image(pair.Key.originalItem)==pair.Value.Receipt.Input).Select(pair=>pair.Value).GroupBy(c=>c.Receipt.Operation).ToArray();
            if(matches.Length!=1)return false;var c=matches[0].First();if(!Read(c.Receipt,false,out var saved)||Encode(saved)!=Encode(c.Receipt))return false;
            operation=c.Receipt.Operation;queuedInput=c.Receipt.Input;amount=c.Receipt.Amount;return true;
        }catch{return false;}
    }
    internal static bool TryObserveRestoredRemoteOriginal(EntityPlayerLocal player,string payment,string paidInput,string queuedInput,int amount,out Guid operation)
    {
        operation=Guid.Empty;
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(payment,out var cost))return false;
            var input=ReadItem(queuedInput);if(!input.TryGetMetadata(OriginalKey,out string key)||!Guid.TryParseExact(key,"N",out var id)||id==Guid.Empty)return false;
            var receipt=new Receipt{Operation=id,World=cost.World,Owner=cost.CanonicalOwner,Creation=cost.Creation,Deaths=cost.Deaths,Input=queuedInput,Amount=amount,Payment=payment,PaidInput=paidInput};receipt.Expected=Preview(receipt);Decode(Encode(receipt));
            var actor=new Client{Receipt=receipt,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir()};
            if(!Current(actor)||!Read(receipt,false,out var stored)||stored.Cancelled||stored.OutputInvoking||stored.NativeDelivered)return false;
            // Initial owner journal publication occurs only after EndActivation's
            // exact native paid/source check. Restored UI custody must also still
            // contain that SAME original; no native callback is invoked here.
            if(!Cards.Any(pair=>ReferenceEquals(pair.Value.Player,player)&&Current(pair.Value)&&pair.Value.Receipt.Operation==id&&pair.Key.AmountToRepair==amount&&pair.Key.originalItem!=null&&Image(pair.Key.originalItem)==queuedInput))return false;
            operation=id;return true;
        }catch{return false;}
    }
    private static bool CompletedLaterRepair(XUiC_RecipeStack card,string marker)
    {
        try
        {
            if(!Guid.TryParseExact(marker,"N",out var old))return false;
            string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","repair-original","owner",old.ToString("N")+".xml.done");
            if(new FileInfo(path).Length>MaximumImage*12L)return false;
            var done=Decode(ReadRepairText(path));
            return (done.Published||done.BaseOnlyConfirmed)&&done.Operation==old&&Image(card.originalItem)!=done.Input;
        }catch{return false;}
    }
    private static bool OutputPrefixCore(XUiC_RecipeStack card)
    {
        if(!Cards.TryGetValue(card,out var c))
        {
            if(card?.originalItem==null||!card.originalItem.TryGetMetadata(OriginalKey,out string marker))return true;
            QueueAccepted(card,true);
            if(!Cards.TryGetValue(card,out c))
            {
                // An old completed marker on an ordinary later repair is not a
                // pending original queue. It confers no new signature permission.
                return CompletedLaterRepair(card,marker);
            }
        }
        bool originalEpisode=Current(c);
        if(c.Player==null||!ReferenceEquals(card.xui?.playerUI?.entityPlayer,c.Player)||(!originalEpisode&&!SamePhysicalActor(c))||!Clients.ContainsKey(c.Receipt.Operation)||originalEpisode&&!c.Receipt.Admitted||c.Receipt.Cancelled||c.Receipt.NativeDelivered||c.Receipt.OutputInvoking)return false;
        if(card.originalItem==null||Image(card.originalItem)!=c.Receipt.Input||card.AmountToRepair!=0&&card.AmountToRepair!=c.Receipt.Amount)return false;
        if(c.Receipt.Expected.Length==0||!Read(c.Receipt,false,out var staged)||staged.Expected!=c.Receipt.Expected)return false;
        // This domain invocation record is written BEFORE any native delivery.
        // An interrupted invocation is retained, never blindly replayed on reload.
        string prior=Encode(c.Receipt);var next=Decode(prior);next.Before=next.Expected;next.OutputInvoking=true;if(!Save(next,false,prior))return false;c.Receipt=next;
        if(card.AmountToRepair==0)
        {
            if(c.Receipt.Before.Length==0)return false;
            // Exact original full-bag retry/reload only. No second degradation,
            // repair quest callback, kit debit or native action is invoked.
            card.outputItemValue=ReadItem(c.Receipt.Before);
        }
        return true;
    }
    internal static void OutputSucceeded(XUiC_RecipeStack card,bool result)
    {
        try
        {
            if(!Cards.TryGetValue(card,out var c)||c.Player==null||!ReferenceEquals(card.xui?.playerUI?.entityPlayer,c.Player)||!SamePhysicalActor(c)||!c.Receipt.OutputInvoking||c.Receipt.Cancelled)return;
            if(card.outputItemValue==null||!ValidNativeOutput(c.Receipt,card.outputItemValue)||Image(card.outputItemValue)!=c.Receipt.Expected)return;
            if(!result&&(card.originalItem==null||card.originalItem.IsEmpty()||Image(card.originalItem)!=c.Receipt.Input))return;
            string prior=Encode(c.Receipt);var next=Decode(prior);next.OutputInvoking=false;next.NativeDelivered=result;
            // False native full-bag return is retryable. Success is recorded once;
            // a write failure retains Invoking and the exact live retry outcome.
            c.PendingOutcome=next;c.PendingExpected=prior;
            if(PublishPendingOutcome(c))UploadOwner(c.Player);
        }catch(Exception error){Log.Warning("[REBIRTH Repair] original native output retained: "+error.GetType().Name);}
    }
    internal static bool CancelPrefix(XUiC_RecipeStack card,out object state)
    {
        state=null;
        try
        {
            if(!Cards.TryGetValue(card,out var c))
            {
                if(card?.originalItem==null||!card.originalItem.TryGetMetadata(OriginalKey,out string marker))return true;
                QueueAccepted(card,true);
                if(!Cards.TryGetValue(card,out c))return CompletedLaterRepair(card,marker);
            }
            // An interrupted native delivery cannot also refund its source.
            // Retain the ambiguous original until actual publication resolves it.
            if(c.Player==null||!ReferenceEquals(card.xui?.playerUI?.entityPlayer,c.Player)||!(Current(c)||SamePhysicalActor(c))||
                c.Receipt.Cancelled||c.Receipt.OutputInvoking||c.Receipt.NativeDelivered||c.PendingOutcome!=null)return false;
            // The native refund includes the source-paid remote kits. Do not
            // let cancellation erase the positive queued-usage witness before
            // the host has durably retired that original source payment.
            if(c.Receipt.Payment.Length!=0&&(!c.Receipt.Admitted||c.Receipt.PaymentProof.Length!=64||c.Receipt.CancelInput.Length==0))return false;
            if(!c.NativePaid&&(!Read(c.Receipt,false,out var paid,c.Directory)||Encode(paid)!=Encode(c.Receipt)))return false;
            if(!TryBindNativeRefund(card,c.Receipt))return false;
            // Persist withdrawal of this original bonus permission BEFORE native
            // cancellation returns/refunds its own source. Failure runs no cancel.
            // Retiring a previous episode writes only its captured original
            // journal; it never admits work into the new world or creation.
            if(!File.Exists(PathFor(c.Receipt,false,c.Directory))&&!Save(c.Receipt,false,null,c.Directory))return false;
            var next=Decode(Encode(c.Receipt));next.Cancelled=true;if(!Save(next,false,Encode(c.Receipt),c.Directory))return false;
            c.Receipt=next;Clients[next.Operation]=c;
            // The host retired only C's exact terminal markers. The native
            // cancellation still returns its original tool and actual paid kits.
            if(next.CancelInput.Length!=0)card.originalItem=ReadItem(next.CancelInput);
            state=c;return true;
        }catch{return false;}
    }
    internal static void Cancelled(XUiC_RecipeStack card,object state)
    {
        if(state is not Client c||!SamePhysicalActor(c)||!ReferenceEquals(card.xui?.playerUI?.entityPlayer,c.Player)||
            !c.Receipt.Cancelled||c.Receipt.CancelReturned||card.originalItem!=null&&!card.originalItem.IsEmpty())return;
        // Queue absence is not proof that the native refund returned. Preserve
        // its positively returned outcome before accepting a delayed host ACK.
        string prior=Encode(c.Receipt);var returned=Decode(prior);returned.CancelReturned=true;
        c.PendingOutcome=returned;c.PendingExpected=prior;Clients[c.Receipt.Operation]=c;
        if(PublishPendingOutcome(c))UploadOwner(c.Player);
    }
    private static void UploadOwner(EntityPlayerLocal owner)
    {GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    private static ItemValue Unique(ItemStack[] bag,ItemStack[] belt,Receipt r,bool after,ItemStack[] equipment=null)
    {
        var expected=ReadItem(after?r.After:r.Before);ItemValue found=null;
        foreach(var stack in (bag??Array.Empty<ItemStack>()).Concat(belt??Array.Empty<ItemStack>()).Concat(equipment??Array.Empty<ItemStack>()))
        {if(stack==null||stack.IsEmpty()||!SameOperation(stack.itemValue,r.Operation,OriginalKey))continue;if(found!=null||stack.count!=1||!SameExceptWear(stack.itemValue,expected))return null;found=stack.itemValue;}
        return found;
    }
    // Postfix consumes only authenticated native incoming blobs. Missing areas
    // remain missing; no fallback to stale live EntityPlayer grids is permitted.
    internal static void NativeUpload(NetPackage package,World world)
    {try{NativeUploadCore(package,world);}catch(Exception failure){Log.Warning("[REBIRTH Repair] native upload proof refused: "+failure.GetType().Name);}}
    private static void NativeUploadCore(NetPackage package,World world)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||package?.Sender==null||!ReferenceEquals(GameManager.Instance?.World,world))return;
        WorldScope(world);PruneUploads(world);
        var player=world.GetEntity(package.Sender.entityId) as EntityPlayer;if(player==null||!ReferenceEquals(player.world,world)||player.IsDead())return;
        if(!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(player.entityId),package.Sender)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(package.Sender,out var authenticated)||!TryRepairIdentity(player,out var actual)||
            authenticated.CanonicalId!=actual.CanonicalId)return;
        var latest=package.Sender.latestPlayerData;
        if(latest==null||latest.deaths!=player.Died||latest.id!=player.entityId)return;
        // Native Process must have accepted this exact package's native images.
        // A merely executed early-returning PlayerData method is no witness.
        if(!AcceptedNativeUpload(package,latest))return;
        var bag=RebirthGearPlayerDataInventory.ReadSlots(latest,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(latest,false);var equipment=ReadEquipment(latest);if(bag==null||belt==null||equipment==null)return;
        WorldScope(world);var upload=new Upload{World=world,Player=player,Sender=package.Sender,Source=latest,BagSource=latest.bagData,BeltSource=latest.inventoryData,EquipmentSource=latest.equipmentData,CursorSource=latest.dragAndDropItem,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=player.Died,Directory=GameIO.GetSaveGameDir(),Bag=bag,Belt=belt,Equipment=equipment,Queue=QueueImages(latest),QueueOperations=QueueOperations(latest),NativeQueueItems=TryQueueNativeItems(latest,out var nativeQueue)?nativeQueue:null,CursorOperation=latest.dragAndDropItem==null||latest.dragAndDropItem.IsEmpty()?string.Empty:Marker(latest.dragAndDropItem.itemValue)};
        if(FreezeUploadHead(upload))Uploads[player.entityId]=upload;
    }
    // Read-only actual package/head relation, never an independent authority.
    // Caller additionally proves native ran, current authenticated peer/body and
    // original episode, then decodes the complete accepted native slot blobs.
    private static bool AcceptedNativeUpload(NetPackage package,PlayerDataFile latest)
    {
        if(latest==null)return false;
        if(package is NetPackagePlayerData full)return ReferenceEquals(full.playerDataFile,latest);
        if(package is not NetPackagePlayerInventory inventory)return false;
        return (inventory.inventoryData!=null||inventory.bagData!=null||inventory.equipmentData!=null||inventory.dragAndDropItem!=null)&&
            (inventory.inventoryData==null||ReferenceEquals(inventory.inventoryData,latest.inventoryData))&&
            (inventory.bagData==null||ReferenceEquals(inventory.bagData,latest.bagData))&&
            (inventory.equipmentData==null||ReferenceEquals(inventory.equipmentData,latest.equipmentData))&&
            (inventory.dragAndDropItem==null||ReferenceEquals(inventory.dragAndDropItem,latest.dragAndDropItem));
    }
    private static void PruneUploads(World world)
    {
        foreach(var pair in Uploads.ToArray())
        {
            var upload=pair.Value;
            if(!ReferenceEquals(upload.World,world)||upload.Directory!=GameIO.GetSaveGameDir()||
                !ReferenceEquals(world.GetEntity(pair.Key),upload.Player)||upload.Player.IsDead()||upload.Player.Died!=upload.Deaths||
                RebirthCamperCharacterAccess.CurrentCreation(upload.Player)!=upload.Creation||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(pair.Key),upload.Sender)||
                !ReferenceEquals(upload.Sender?.latestPlayerData,upload.Source))Uploads.Remove(pair.Key);
        }
    }
    private sealed class QueueWitness {internal string[] Images,Native;internal RecipeQueueItem[] Rows;internal ItemValue[] Items;internal string[] Typed,Shape;}
    private sealed class ItemReferenceComparer:IEqualityComparer<ItemValue>{public bool Equals(ItemValue a,ItemValue b)=>ReferenceEquals(a,b);public int GetHashCode(ItemValue item)=>RuntimeHelpers.GetHashCode(item);}
    private static readonly ConditionalWeakTable<PlayerDataFile,QueueWitness> NativeQueues=new ConditionalWeakTable<PlayerDataFile,QueueWitness>();
    [ThreadStatic] private static RawCapture rawCapture;
    [ThreadStatic] private static bool decodingRaw;
    private sealed class RawCapture:IDisposable
    {
        internal readonly Dictionary<ItemValue,string> Images=new Dictionary<ItemValue,string>(new ItemReferenceComparer());internal bool Failed;internal int Budget=16*1024*1024;
        internal RawCapture(){if(rawCapture!=null)throw new InvalidOperationException("Nested repair native read.");rawCapture=this;}
        public void Dispose(){if(ReferenceEquals(rawCapture,this))rawCapture=null;}
        internal void Bind(PlayerDataFile data)
        {
            if(data==null)return;lock(NativeQueues)NativeQueues.Remove(data);if(Failed)return;var rows=data.craftingData?.RecipeQueueItems??Array.Empty<RecipeQueueItem>();var images=new List<string>();
            foreach(var row in rows){if(row?.RepairItem==null||row.RepairItem.IsEmpty())continue;if(!Images.TryGetValue(row.RepairItem,out string image))return;images.Add(QueueKey(row,image));}
            lock(NativeQueues){NativeQueues.Remove(data);NativeQueues.Add(data,new QueueWitness{Images=images.Where(x=>x.Length!=0).ToArray(),Native=rows.Select(row=>row?.RepairItem==null||row.RepairItem.IsEmpty()?string.Empty:Images[row.RepairItem]).ToArray(),Rows=rows.ToArray(),Items=rows.Select(row=>row?.RepairItem).ToArray(),Typed=rows.Select(row=>row?.RepairItem==null?string.Empty:Image(row.RepairItem)).ToArray(),Shape=rows.Select(QueueShape).ToArray()});}
        }
    }
    internal static object BeginRawCapture(){try{return new RawCapture();}catch{return null;}}
    internal static void EndRawCapture(object state,PlayerDataFile data,Exception error)
    {if(state is not RawCapture capture)return;try{if(error==null)capture.Bind(data);}finally{capture.Dispose();}}
    internal static long BeginRawItem(PooledBinaryReader reader)
    {if(rawCapture==null||decodingRaw)return -1;try{return reader.BaseStream.CanSeek?reader.BaseStream.Position:-1;}catch{rawCapture.Failed=true;return -1;}}
    internal static void EndRawItem(PooledBinaryReader reader,long start,ItemValue value)
    {
        var capture=rawCapture;if(capture==null||decodingRaw||start<0)return;
        try
        {
            long end=reader.BaseStream.Position,length=end-start;if(length<=0||length>196608||length>capture.Budget||capture.Images.Count>=2048)throw new InvalidDataException();
            var bytes=new byte[(int)length];try{reader.BaseStream.Position=start;int offset=0,n;while(offset<bytes.Length&&(n=reader.BaseStream.Read(bytes,offset,bytes.Length-offset))>0)offset+=n;if(offset!=bytes.Length)throw new EndOfStreamException();}finally{reader.BaseStream.Position=end;}
            if(value==null||value.IsEmpty())return;string image=Convert.ToBase64String(bytes);decodingRaw=true;try{if(!RebirthNativeItemCodec.TryDecode(image,out var canonical)||canonical==null)throw new InvalidDataException();}finally{decodingRaw=false;}
            capture.Budget-=(int)length;capture.Images[value]=image;
        }catch{capture.Failed=true;}
    }
    private static bool ReadSaved(RebirthStablePlayerIdentity identity,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved)
    {
        actor=null;saved=null;using(var capture=new RawCapture()){if(!RebirthCamperEnergySavedOwner.TryRead(identity,out actor,out saved))return false;capture.Bind(saved);return !capture.Failed;}
    }
    internal static ItemStack[] ReadSavedEquipment(PlayerDataFile data)=>ReadEquipment(data);
    private static ItemStack[] ReadEquipment(PlayerDataFile data)
    {
        try
        {
            var bytes=data?.equipmentData?.ToArray();if(bytes==null||bytes.Length==0||bytes.Length>4*1024*1024)return null;
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                Encoding previous=reader.Encoding;try
                {
                    reader.Encoding=new UTF8Encoding(false,false);reader.SetBaseStream(stream);if(reader.ReadByte()!=6)return null;long start=stream.Position;
                    ushort version=reader.ReadUInt16();var size=StreamUtils.ReadVector2i(reader);int count=reader.ReadInt16();if(version<1||version>2||size.x!=13||size.y!=1||count!=13)return null;
                    stream.Position=start;ItemStackGrid.Read(reader,StreamModeRead.Persistency);for(int i=0;i<13;i++)reader.ReadInt32();int cosmetics=reader.ReadInt32();if(cosmetics<0||cosmetics>65536||(long)cosmetics*4!=stream.Length-stream.Position)return null;
                    Equipment detached;var capture=new RawCapture();using(capture){stream.Position=0;detached=Equipment.Read(reader,StreamModeRead.Persistency);if(capture.Failed||stream.Position!=stream.Length)return null;}
                    var rows=detached.itemGrid.items;if(rows==null||rows.Length!=13)return null;var result=new ItemStack[13];
                    for(int i=0;i<13;i++){var row=rows[i];if(row==null||row.IsEmpty()){result[i]=ItemStack.Empty.Clone();continue;}if(row.count!=1||!capture.Images.TryGetValue(row.itemValue,out string image))return null;result[i]=new ItemStack(ReadItem(image),1);}return result;
                }finally{reader.Encoding=previous;}
            }
        }catch{return null;}
    }
    private static string Marker(ItemValue item)=>item!=null&&item.TryGetMetadata(OriginalKey,out string value)?value:string.Empty;
    private static string QueueShape(RecipeQueueItem row)=>row==null?string.Empty:row.AmountToRepair+":"+row.Multiplier+":"+row.Recipe?.itemValueType+":"+row.Recipe?.count+":"+row.Recipe?.IsScrap;
    private static string QueueKey(RecipeQueueItem row,string nativeImage)
    {return row?.RepairItem==null||row.RepairItem.IsEmpty()||row.Recipe==null||row.Recipe.IsScrap||row.Recipe.count!=1||row.Recipe.itemValueType!=row.RepairItem.type||row.Multiplier!=1||row.AmountToRepair==0?string.Empty:nativeImage+":"+row.AmountToRepair;}
    private static string[] QueueImages(PlayerDataFile saved)
    {
        if(saved==null||!NativeQueues.TryGetValue(saved,out var witness))return Array.Empty<string>();var rows=saved.craftingData?.RecipeQueueItems??Array.Empty<RecipeQueueItem>();if(rows.Length!=witness.Rows.Length)return Array.Empty<string>();
        for(int i=0;i<rows.Length;i++)if(!ReferenceEquals(rows[i],witness.Rows[i])||!ReferenceEquals(rows[i]?.RepairItem,witness.Items[i])||(rows[i]?.RepairItem==null?string.Empty:Image(rows[i].RepairItem))!=witness.Typed[i]||QueueShape(rows[i])!=witness.Shape[i])return Array.Empty<string>();return witness.Images;
    }
    private static string[] QueueOperations(PlayerDataFile saved)=>saved?.craftingData?.RecipeQueueItems?.Where(row=>row?.RepairItem!=null&&row.RepairItem.HasMetadata(OriginalKey)).Select(row=>{row.RepairItem.TryGetMetadata(OriginalKey,out string operation);return operation;}).ToArray()??Array.Empty<string>();
    private static bool TryQueueNativeItems(PlayerDataFile saved,out string[] images)
    {
        images=null;if(saved==null||!NativeQueues.TryGetValue(saved,out var witness)||witness.Native==null)return false;var rows=saved.craftingData?.RecipeQueueItems??Array.Empty<RecipeQueueItem>();
        if(rows.Length!=witness.Rows.Length||rows.Length!=witness.Native.Length)return false;
        for(int i=0;i<rows.Length;i++)if(!ReferenceEquals(rows[i],witness.Rows[i])||!ReferenceEquals(rows[i]?.RepairItem,witness.Items[i])||(rows[i]?.RepairItem==null?string.Empty:Image(rows[i].RepairItem))!=witness.Typed[i]||QueueShape(rows[i])!=witness.Shape[i])return false;
        images=(string[])witness.Native.Clone();return true;
    }
    internal static bool HasOriginalQueuedItem(string[] images,string exactInput)
    {
        if(images==null)throw new InvalidDataException();var original=ReadItem(exactInput);if(!original.TryGetMetadata(OriginalKey,out string operation)||!Guid.TryParseExact(operation,"N",out _))throw new InvalidDataException();
        string source=null,binding=null;bool fetched=original.TryGetMetadata(RebirthRemoteResourceOwnerAction.Marker,out source)&&original.TryGetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,out binding);
        foreach(string image in images)
        {
            if(image.Length==0)continue;var item=ReadItem(image);
            if(image==exactInput||item.TryGetMetadata(OriginalKey,out string marker)&&marker==operation||fetched&&item.TryGetMetadata(RebirthRemoteResourceOwnerAction.Marker,out string otherSource)&&otherSource==source&&item.TryGetMetadata(RebirthRemoteResourceOwnerAction.BindingMarker,out string otherBinding)&&otherBinding==binding)return true;
        }
        return false;
    }
    // Observation only. Missing/invalid raw witnesses are unknown, not empty.
    // C must additionally prove native phase5, full Before/source and rejection.
    internal static bool TryReadSavedRejectedRepairOwner(EntityPlayer owner,string exactQueuedInput,int amount,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved)
    {
        actor=null;saved=null;
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||owner==null||owner is EntityPlayerLocal||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||owner.IsDead()||amount<=0||amount>ushort.MaxValue)return false;
            var item=ReadItem(exactQueuedInput);if(!item.TryGetMetadata(OriginalKey,out string marker)||!Guid.TryParseExact(marker,"N",out var operation)||operation==Guid.Empty||!item.TryGetMetadata(WorldKey,out string worldId)||worldId!=world.worldState.Guid||!item.TryGetMetadata(CreationKey,out string creation)||creation!=RebirthCamperCharacterAccess.CurrentCreation(owner)||!item.TryGetMetadata(OwnerKey,out string ownerId)||!TryRepairIdentity(owner,out var identity)||ownerId!=identity.CanonicalId||!item.TryGetMetadata(DeathsKey,out int deaths)||deaths!=owner.Died)return false;
            var scope=new Receipt{Operation=operation,World=worldId,Creation=creation,Owner=ownerId,Deaths=deaths};
            if(!UploadCurrent(owner,scope,out var incoming)||incoming.NativeQueueItems==null||HasOriginalQueuedItem(incoming.NativeQueueItems,exactQueuedInput)||!ReadSaved(identity,out var readActor,out var readFile)||readFile.id!=owner.entityId||readFile.deaths!=deaths||!TryQueueNativeItems(readFile,out var native)||HasOriginalQueuedItem(native,exactQueuedInput))return false;
            actor=readActor;saved=readFile;return true;
        }catch{actor=null;saved=null;return false;}
    }
    private static bool QueueProof(EntityPlayer owner,Receipt r)
    {
        string expected=r.Input+":"+r.Amount;
        if(!TryRepairIdentity(owner,out var identity)||!ReadSaved(identity,out var actor,out var saved)||saved.id!=owner.entityId||saved.deaths!=r.Deaths||QueueImages(saved).Count(x=>x==expected)!=1)return false;
        if(owner is EntityPlayerLocal local&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),local))return Cards.Values.Any(c=>ReferenceEquals(c.Player,local)&&c.Receipt.Operation==r.Operation&&c.Receipt.Input==r.Input);
        return UploadCurrent(owner,r,out var upload)&&upload.Queue.Count(x=>x==expected)==1;
    }
    // Observation only: this is not a payment, correction or delivery authority.
    // The fetch domain must independently prove its own exact source payment
    // and local debit against the same original action before settlement.
    internal static bool TryWitnessSavedOriginalQueue(EntityPlayer owner,string nativeInput,int amount,out Guid operation)
        =>TryReadSavedOriginalQueue(owner,nativeInput,amount,out operation,out _,out _);
    internal static bool TryReadSavedOriginalQueue(EntityPlayer owner,string nativeInput,int amount,out Guid operation,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved)
    {
        operation=Guid.Empty;actor=null;saved=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||owner==null||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||owner.IsDead()||amount<=0||amount>ushort.MaxValue)return false;
            var item=ReadItem(nativeInput);
            if(!item.TryGetMetadata(OriginalKey,out string marker)||!Guid.TryParseExact(marker,"N",out var id)||id==Guid.Empty||!item.TryGetMetadata(WorldKey,out string worldId)||worldId!=world.worldState.Guid||!item.TryGetMetadata(CreationKey,out string creation)||creation!=RebirthCamperCharacterAccess.CurrentCreation(owner)||!item.TryGetMetadata(OwnerKey,out string ownerId)||!TryRepairIdentity(owner,out var identity)||ownerId!=identity.CanonicalId||!item.TryGetMetadata(DeathsKey,out int deaths)||deaths!=owner.Died)return false;
            var receipt=new Receipt{Operation=id,World=worldId,Creation=creation,Owner=ownerId,Deaths=deaths,Input=nativeInput,Amount=amount};string expected=nativeInput+":"+amount;
            if(!ReadSaved(identity,out var readActor,out var readFile)||readFile.id!=owner.entityId||readFile.deaths!=deaths||QueueImages(readFile).Count(x=>x==expected)!=1)return false;
            if(owner is EntityPlayerLocal local&&ReferenceEquals(world.GetPrimaryPlayer(),local))
            {if(!Cards.Values.Any(c=>ReferenceEquals(c.Player,local)&&c.Receipt.Operation==id&&c.Receipt.Input==nativeInput))return false;}
            else if(!UploadCurrent(owner,receipt,out var upload)||upload.Queue.Count(x=>x==expected)!=1)return false;
            operation=id;actor=readActor;saved=readFile;return true;
        }catch{operation=Guid.Empty;actor=null;saved=null;return false;}
    }
    private static bool UploadCurrent(EntityPlayer owner,Receipt r,out Upload upload)=>Uploads.TryGetValue(owner.entityId,out upload)&&ReferenceEquals(upload.World,GameManager.Instance?.World)&&ReferenceEquals(upload.Player,owner)&&upload.Creation==r.Creation&&upload.Deaths==r.Deaths&&upload.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(owner.entityId),upload.Sender)&&ReferenceEquals(upload.Sender?.latestPlayerData,upload.Source)&&ReferenceEquals(upload.Source?.bagData,upload.BagSource)&&ReferenceEquals(upload.Source?.inventoryData,upload.BeltSource)&&ReferenceEquals(upload.Source?.equipmentData,upload.EquipmentSource)&&ReferenceEquals(upload.Source?.dragAndDropItem,upload.CursorSource)&&UploadHeadUnchanged(upload);
    private static bool QueueAbsentProof(EntityPlayer owner,Receipt r,bool laterLife=false)
    {
        if(laterLife&&(owner.Died<=r.Deaths||!r.Cancelled||!r.CancelReturned))return false;
        int nativeDeaths=laterLife?owner.Died:r.Deaths;
        if(!TryRepairIdentity(owner,out var identity)||!ReadSaved(identity,out var actor,out var saved)||saved.id!=owner.entityId||saved.deaths!=nativeDeaths||!TryQueueNativeItems(saved,out var native)||HasOriginalQueuedItem(native,r.Input))return false;
        if(owner is EntityPlayerLocal local&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),local))return !Cards.Any(pair=>ReferenceEquals(pair.Value.Player,local)&&SameOperation(pair.Key.originalItem,r.Operation,OriginalKey));
        var scope=laterLife?new Receipt{Creation=r.Creation,Deaths=nativeDeaths}:r;
        return UploadCurrent(owner,scope,out var upload)&&upload.NativeQueueItems!=null&&!HasOriginalQueuedItem(upload.NativeQueueItems,r.Input);
    }
    private static bool NativeProof(EntityPlayer owner,Receipt r,bool after)
    {
        if((after?r.After:r.Before).Length==0||!TryRepairIdentity(owner,out var identity)||!ReadSaved(identity,out var actor,out var saved)||saved.id!=owner.entityId||saved.deaths!=r.Deaths||actor.Cursor!=null&&!actor.Cursor.IsEmpty()&&SameOperation(actor.Cursor.itemValue,r.Operation,OriginalKey)||!TryQueueNativeItems(saved,out var native)||HasOriginalQueuedItem(native,r.Input))return false;
        var equipment=ReadEquipment(saved);if(equipment==null||Unique(actor.Bag,actor.Belt,r,after,equipment)==null)return false;
        if(owner is EntityPlayerLocal local&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),local))return (local.DragAndDropItem==null||local.DragAndDropItem.IsEmpty()||!SameOperation(local.DragAndDropItem.itemValue,r.Operation,OriginalKey))&&Unique(local.bag.ItemGrid.items,local.inventory.ItemGrid.items,r,after,local.equipment?.itemGrid?.items)!=null;
        return UploadCurrent(owner,r,out var upload)&&upload.CursorOperation!=r.Operation.ToString("N")&&upload.NativeQueueItems!=null&&!HasOriginalQueuedItem(upload.NativeQueueItems,r.Input)&&Unique(upload.Bag,upload.Belt,r,after,upload.Equipment)!=null;
    }
    private static bool BaseOnlyNativeProof(EntityPlayer owner,Receipt r,bool after,out ItemValue value)
    {
        value=null;
        if(owner.Died<=r.Deaths||!r.Admitted||(r.Payment.Length==0?r.PaymentProof.Length!=0||r.PaidInput.Length!=0:r.PaymentProof.Length!=64)||!TryRepairIdentity(owner,out var identity)||identity.CanonicalId!=r.Owner||!ReadSaved(identity,out var actor,out var saved)||saved.id!=owner.entityId||saved.deaths!=owner.Died||actor.Cursor!=null&&!actor.Cursor.IsEmpty()&&SameOperation(actor.Cursor.itemValue,r.Operation,OriginalKey)||!TryQueueNativeItems(saved,out var nativeQueue)||HasOriginalQueuedItem(nativeQueue,r.Input))return false;
        var equipment=ReadEquipment(saved);if(equipment==null)return false;
        var actual=Unique(actor.Bag,actor.Belt,r,after,equipment);if(actual==null)return false;
        if(owner is EntityPlayerLocal local&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),local))
        {
            if(local.DragAndDropItem!=null&&!local.DragAndDropItem.IsEmpty()&&SameOperation(local.DragAndDropItem.itemValue,r.Operation,OriginalKey)||
                Cards.Any(pair=>ReferenceEquals(pair.Value.Player,local)&&SameOperation(pair.Key.originalItem,r.Operation,OriginalKey)))return false;
            var live=Unique(local.bag.ItemGrid.items,local.inventory.ItemGrid.items,r,after,local.equipment?.itemGrid?.items);
            if(live==null||Image(live)!=Image(actual))return false;value=ReadItem(Image(actual));return true;
        }
        if(!UploadCurrent(owner,new Receipt{Creation=r.Creation,Deaths=owner.Died},out var incoming)||incoming.CursorOperation==r.Operation.ToString("N")||incoming.NativeQueueItems==null||HasOriginalQueuedItem(incoming.NativeQueueItems,r.Input))return false;
        var uploaded=Unique(incoming.Bag,incoming.Belt,r,after,incoming.Equipment);
        if(actual==null||uploaded==null||Image(actual)!=Image(uploaded))return false;value=ReadItem(Image(actual));return true;
    }
    private static void SettleEndedBase(EntityPlayer owner,Receipt request)
    {
        // Never admit an old episode here: existing server admission is mandatory.
        if(!Read(request,true,out var r)||!r.Admitted||r.Cancelled||r.Published||(r.Payment.Length==0?r.PaymentProof.Length!=0||r.PaidInput.Length!=0:r.PaymentProof.Length!=64)||r.Input!=request.Input||r.Payment!=request.Payment||r.Amount!=request.Amount||r.Owner!=request.Owner||r.World!=request.World||r.Creation!=request.Creation||r.Deaths!=request.Deaths)return;
        if(r.BaseOnlyConfirmed){Reply(owner,r);return;}
        if(!r.BaseOnly)
        {
            if(r.After.Length!=0)return;var candidate=Decode(Encode(r));candidate.Before=r.Expected;
            if(!BaseOnlyNativeProof(owner,candidate,false,out var native))return;
            ItemValue clean;if(r.Payment.Length==0)clean=ReadItem(Image(native));else if(!RebirthRemoteResourceOwnerAction.TryRetireRepairMarkers(owner,native,r.Payment,r.PaymentProof,out clean))return;
            candidate.Before=Image(native);candidate.After=Image(clean);candidate.BaseOnly=true;candidate.OutputInvoking=false;candidate.NativeDelivered=true;
            if(!TryMergeBaseOnly(native,candidate,out _)||!Save(candidate,true,Encode(r)))return;r=candidate;
        }
        if(BaseOnlyNativeProof(owner,r,true,out _))
        {var next=Decode(Encode(r));next.BaseOnlyConfirmed=true;if(!Save(next,true,Encode(r)))return;r=next;}
        Reply(owner,r);
    }
    private static void Reply(EntityPlayer owner,Receipt r)
    {if(owner is EntityPlayerLocal&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),owner))ReceiveClient((World)owner.world,owner.entityId,Encode(r));else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRepairOriginalReply>().Setup(owner.entityId,Encode(r)),_attachedToEntityId:owner.entityId);}
    internal static bool TryAuthorizeOriginalPayment(EntityPlayer owner,Receipt r,out string proof)
    {
        proof=string.Empty;if(r==null||r.PaymentProof.Length!=0)return false;
        if(r.Payment.Length==0)return r.PaidInput.Length==0;
        if(!RebirthRemoteResourceOwnerAction.TryCompleteSavedRepairPayment(owner,r.Payment,r.Input,r.Amount,out var original,out string verified)||original!=r.Operation||verified?.Length!=64||verified.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f')))return false;
        proof=verified;return true;
    }
    // Detached diagnostics only. Candidate IDs come from original runtime/native
    // custody, not a caller-supplied file path; no native item images are exposed.
    internal static object CaptureAuditState(EntityPlayer actor)
    {
        var records=new List<object>();var candidates=new HashSet<Guid>();var queued=new HashSet<Guid>();bool truncated=false,nativeReadable=false;
        RebirthCamperEnergyActorImage savedOwner=null;ItemStack[] savedEquipment=null;string[] savedQueueItems=null;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||actor==null||world==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!TryRepairIdentity(actor,out var identity))return new{scope=false,records=records.ToArray()};
        void Add(Guid id){if(id==Guid.Empty)return;if(candidates.Count<256)candidates.Add(id);else if(!candidates.Contains(id))truncated=true;}
        void Value(ItemValue item){if(item!=null&&item.TryGetMetadata(OriginalKey,out string value)&&Guid.TryParseExact(value,"N",out var id))Add(id);}
        void Slots(ItemStack[] slots){if(slots!=null)foreach(var stack in slots)if(stack!=null&&!stack.IsEmpty())Value(stack.itemValue);}
        foreach(var client in Clients.Values)if(ReferenceEquals(client.Player,actor)&&ReferenceEquals(client.World,world)&&client.Directory==GameIO.GetSaveGameDir())Add(client.Receipt.Operation);
        foreach(var pair in Cards)if(ReferenceEquals(pair.Value.Player,actor)&&ReferenceEquals(pair.Value.World,world)){Add(pair.Value.Receipt.Operation);if(pair.Key.originalItem!=null&&!pair.Key.originalItem.IsEmpty())queued.Add(pair.Value.Receipt.Operation);}
        if(actor is EntityPlayerLocal local){Slots(local.bag.ItemGrid.items);Slots(local.inventory.ItemGrid.items);Slots(local.equipment?.itemGrid?.items);Value(local.DragAndDropItem?.itemValue);}
        // Native dropped tools remain observable after owner journal archival.
        // This is the current loaded entity set, never a saved-world authority.
        var worldItems=world.Entities.list.OfType<EntityItem>().Take(4097).ToArray();
        bool worldItemsTruncated=worldItems.Length>4096;
        var droppedItems=worldItemsTruncated?null:worldItems.Select(item=>item.itemStack).ToArray();
        Slots(droppedItems);
        try
        {
            if(ReadSaved(identity,out var image,out var saved)&&saved.deaths==actor.Died)
            {nativeReadable=true;savedOwner=image;savedEquipment=ReadEquipment(saved);TryQueueNativeItems(saved,out savedQueueItems);Slots(image.Bag);Slots(image.Belt);Slots(savedEquipment);Value(image.Cursor?.itemValue);foreach(string key in QueueOperations(saved))if(Guid.TryParseExact(key,"N",out var id)){Add(id);queued.Add(id);}}
        }catch{/* An unreadable native file is a diagnostic result, never proof. */}
        foreach(Guid id in candidates.OrderBy(value=>value))
        {
            foreach(bool server in world.IsRemote()?new[]{false}:new[]{true,false})
            {
            var key=new Receipt{Operation=id};string path=PathFor(key,server);if(!File.Exists(path)&&File.Exists(path+".done"))path+=".done";
            try
            {
                if(new FileInfo(path).Length>MaximumImage*12L)throw new InvalidDataException();var r=Decode(ReadRepairText(path));
                if(r.Operation!=id)throw new InvalidDataException();
                if(r.World!=world.worldState?.Guid||r.Owner!=identity.CanonicalId||r.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor))continue;
                bool scope=r.Deaths==actor.Died&&!actor.IsDead();
                string Fingerprint(string value)=>string.IsNullOrEmpty(value)?null:RebirthRemoteResourceDurableJournal.Digest(value);
                string paidSource=null,binding=null;if(r.Payment.Length!=0&&RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(r.Payment,out var payment)){paidSource=payment.SourceOperation.ToString("N");binding=payment.Binding;}
                bool incomingCurrent=scope&&UploadCurrent(actor,r,out _);
                var nativeSaved=savedOwner==null?null:ObserveRepairAuditItem(savedOwner.Bag,savedOwner.Belt,savedEquipment,savedOwner.Cursor,r);
                var primary=actor as EntityPlayerLocal;
                var nativeLive=primary==null?null:ObserveRepairAuditItem(primary.bag?.ItemGrid.items,primary.inventory?.ItemGrid.items,primary.equipment?.itemGrid?.items,primary.DragAndDropItem,r);
                var nativeWorld=droppedItems==null?null:ObserveRepairAuditItem(droppedItems,Array.Empty<ItemStack>(),Array.Empty<ItemStack>(),null,r);
                bool ownerActive=Clients.TryGetValue(id,out var active)&&ReferenceEquals(active.Player,actor)&&ReferenceEquals(active.World,world);
                bool ownerCardHeld=Cards.Any(pair=>ReferenceEquals(pair.Value.Player,actor)&&ReferenceEquals(pair.Value.World,world)&&pair.Value.Receipt.Operation==id&&pair.Key.originalItem!=null&&!pair.Key.originalItem.IsEmpty());
                records.Add(new{operation=id.ToString("N"),journalRole=server?"server":"owner",journalReadable=true,currentOwnerScope=scope,nativeHeadCurrent=incomingCurrent,incomingRawQueueReadable=incomingCurrent&&Uploads[actor.entityId].NativeQueueItems!=null,sourceOperation=paidSource,sourceBinding=binding,admitted=r.Admitted,published=r.Published,baseOnly=r.BaseOnly,baseOnlyConfirmed=r.BaseOnlyConfirmed,cancelled=r.Cancelled,cancelConfirmed=r.CancelConfirmed,cancelReturned=r.CancelReturned,invoking=r.OutputInvoking,nativeDelivered=r.NativeDelivered,paymentProofPresent=r.PaymentProof.Length!=0,paymentProof=r.PaymentProof.Length==0?null:r.PaymentProof,inputFingerprint=Fingerprint(r.Input),stagedOutputFingerprint=Fingerprint(r.Expected),nativeOutputFingerprint=Fingerprint(r.Before),correctedOutputFingerprint=Fingerprint(r.After),cancellationInputFingerprint=Fingerprint(r.CancelInput),cancellationInputPresent=r.CancelInput.Length!=0,queuePresent=queued.Contains(id),nativeSaved,nativeLive,nativeWorld,originalDeaths=r.Deaths,ownerJournalActive=ownerActive,ownerCardHeld,pendingNativeOutcome=ownerActive&&active.PendingOutcome!=null,journalArchived=path.EndsWith(".done",StringComparison.Ordinal),savedQueueOriginalPresent=savedQueueItems==null?(bool?)null:HasOriginalQueuedItem(savedQueueItems,r.Input)});
            }
            catch{records.Add(new{operation=id.ToString("N"),journalRole=server?"server":"owner",journalReadable=false,currentOwnerScope=false,queuePresent=queued.Contains(id)});}
            }
        }
        return new{scope=true,authoritative=!world.IsRemote(),nativePrimaryReadable=nativeReadable,nativeRawQueueReadable=savedQueueItems!=null,nativeWorldItemsTruncated=worldItemsTruncated,deaths=actor.Died,truncated,records=records.ToArray()};
    }
    internal static void ReceiveServer(World world,int playerId,string image)
    {
        try
        {
            if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world))return;WorldScope(world);var request=Decode(image);var owner=world.GetEntity(playerId) as EntityPlayer;
            if(owner==null||!ReferenceEquals(owner.world,world)||owner.IsDead()||owner.Died<request.Deaths||request.World!=world.worldState.Guid||request.Creation!=RebirthCamperCharacterAccess.CurrentCreation(owner)||!TryRepairIdentity(owner,out var identity)||identity.CanonicalId!=request.Owner)return;
            if(owner.Died>request.Deaths){if(request.Cancelled)ConfirmHistoricalCancellation(owner,request);else SettleEndedBase(owner,request);return;}
            if(request.BaseOnly||request.BaseOnlyConfirmed)return;
            Receipt r;if(Read(request,true,out var existing))r=existing;else
            {
                if(File.Exists(PathFor(request,true))||File.Exists(PathFor(request,true)+".bak")||request.After.Length!=0||request.Before.Length!=0||request.Admitted||request.CancelConfirmed||request.OutputInvoking||request.NativeDelivered||request.Published||request.PaymentProof.Length!=0||request.Expected.Length==0)return;
                r=Decode(Encode(request));if(request.Cancelled?(!request.CancelReturned||!QueueAbsentProof(owner,r)):!QueueProof(owner,r))return;
                if(!request.Cancelled)
                {if(!TryAuthorizeOriginalPayment(owner,r,out string proof))return;r.PaymentProof=proof;}
                r.Admitted=!request.Cancelled;r.CancelConfirmed=request.Cancelled;
                if(r.Admitted&&r.Payment.Length!=0)
                {if(!RebirthRemoteResourceOwnerAction.TryRetireRepairMarkers(owner,ReadItem(r.Input),r.Payment,r.PaymentProof,out var cancelInput))return;r.CancelInput=Image(cancelInput);}
                if(!Save(r,true))return;
            }
            if(r.BaseOnly)return;
            if(r.Admitted&&!r.Cancelled&&!r.Published&&r.Payment.Length!=0&&r.CancelInput.Length==0)
            {
                if(!RebirthRemoteResourceOwnerAction.TryRetireRepairMarkers(owner,ReadItem(r.Input),r.Payment,r.PaymentProof,out var cancelInput))return;
                string prior=Encode(r);var next=Decode(prior);next.CancelInput=Image(cancelInput);if(!Save(next,true,prior))return;r=next;
            }
            if(r.CancelConfirmed||r.Published){Reply(owner,r);return;}
            if(request.Cancelled&&r.After.Length==0){if(!request.CancelReturned||!QueueAbsentProof(owner,r))return;string prior=Encode(r);r.Cancelled=true;r.CancelReturned=true;r.CancelConfirmed=true;if(!Save(r,true,prior))return;Reply(owner,r);return;}
            if(r.Cancelled){Reply(owner,r);return;}
            if(r.Before.Length==0&&request.Before.Length!=0)
            {
                if(!request.Admitted||!ValidNativeOutput(r,ReadItem(request.Before)))return;
                var candidate=Decode(Encode(r));candidate.Before=request.Before;candidate.OutputInvoking=false;candidate.NativeDelivered=true;if(!NativeProof(owner,candidate,false))return;
                if(!RebirthRepairSignatureService.TryApplyOriginalOutcome(owner,ReadItem(r.Input),ReadItem(candidate.Before),out var corrected))return;
                if(r.Payment.Length!=0)
                {if(!RebirthRemoteResourceOwnerAction.TryRetireRepairMarkers(owner,corrected,r.Payment,r.PaymentProof,out var clean))return;corrected=clean;}
                corrected.SetMetadata(AppliedKey,r.Operation.ToString("N"));candidate.After=Image(corrected);if(!TryMerge(ReadItem(candidate.Before),candidate,out _)||!Save(candidate,true,Encode(r)))return;r=candidate;
            }
            if(r.After.Length!=0&&!r.Published&&NativeProof(owner,r,true)){if(!RebirthEquipmentRepairTrainingService.TryAwardOriginal(owner,r))return;string prior=Encode(r);r.Published=true;if(!Save(r,true,prior))return;}
            Reply(owner,r);
        }catch(Exception failure){Log.Warning("[REBIRTH Repair] original native correction retained: "+failure.GetType().Name);}
    }
    internal static void ReceiveClient(World world,int playerId,string image)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(GameManager.Instance?.World,world))return;
            var owner=world.GetPrimaryPlayer();if(owner==null||owner.entityId!=playerId||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(playerId),owner))return;
            WorldScope(world);var r=Decode(image);
            if(!Clients.TryGetValue(r.Operation,out var c)){if(!Read(r,false,out var original))return;c=new Client{Receipt=original,World=world,Player=owner,Directory=GameIO.GetSaveGameDir()};Clients.Add(r.Operation,c);}
            if(!PublishPendingOutcome(c))return;
            var before=c.Receipt;if(!(Current(c)||SamePhysicalActor(c)&&(r.BaseOnly||r.CancelConfirmed&&r.Cancelled&&(r.CancelReturned||before.CancelReturned)))||before.BaseOnly&&!r.BaseOnly||before.BaseOnlyConfirmed&&!r.BaseOnlyConfirmed||before.Input!=r.Input||before.Expected!=r.Expected||before.Amount!=r.Amount||before.Payment!=r.Payment||before.PaidInput!=r.PaidInput||before.PaymentProof.Length!=0&&before.PaymentProof!=r.PaymentProof||before.CancelInput.Length!=0&&before.CancelInput!=r.CancelInput||before.Admitted&&!r.Admitted||before.Cancelled&&!r.Cancelled||before.CancelConfirmed&&!r.CancelConfirmed||before.Before.Length!=0&&r.Before.Length!=0&&before.Before!=r.Before&&!(r.BaseOnly&&!before.BaseOnly&&SameExceptWear(ReadItem(r.Before),ReadItem(before.Before)))||before.After.Length!=0&&before.After!=r.After||before.Published&&!r.Published)return;
            // A delayed admission ACK cannot erase an already captured original
            // output or cancellation that has not yet reached the host.
            if(r.Before.Length==0&&before.Before.Length!=0)r.Before=before.Before;if(before.Cancelled)r.Cancelled=true;if(before.CancelReturned)r.CancelReturned=true;if(before.NativeDelivered)r.NativeDelivered=true;if(before.OutputInvoking&&!r.NativeDelivered)r.OutputInvoking=true;
            if(!Save(r,false,Encode(before)))return;c.Receipt=r;
        }catch(Exception failure){Log.Warning("[REBIRTH Repair] owner correction retained: "+failure.GetType().Name);}
    }
    private static bool Apply(Client c)
    {
        var p=c.Player;var r=c.Receipt;
        if(!(Current(c)||r.BaseOnly&&SamePhysicalActor(c))||p.DragAndDropItem!=null&&!p.DragAndDropItem.IsEmpty()&&SameOperation(p.DragAndDropItem.itemValue,r.Operation,OriginalKey)||RebirthCookingOutputDelivery.IsHeld(p)||RebirthLogisticsOwnerReceipt.IsHeld(p)||AdvancedFarmingHarvestOwnerDelta.IsHeld(p)||AdvancedFarmingWaterExchangeOriginal.IsHeld(p)||RebirthGearOwnerReservation.IsHeld(p)||RebirthBackpackLibraryReservation.IsHeld(p)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(p)||RebirthCamperStationNativeActorPayment.Blocks(p.bag)||RebirthCamperStationNativeActorPayment.Blocks(p.inventory))return false;
        var bag=p.bag.ItemGrid.items;var belt=p.inventory.ItemGrid.items;var equipment=p.equipment?.itemGrid?.items;ItemStack candidate=null;int areaFound=-1,index=-1;
        for(int area=0;area<3;area++){var rows=area==0?bag:area==1?belt:equipment;if(rows==null)return false;for(int slot=0;slot<rows.Length;slot++){var row=rows[slot];if(row==null||row.IsEmpty()||!SameOperation(row.itemValue,r.Operation,OriginalKey))continue;if(candidate!=null||row.count!=1)return false;candidate=row;areaFound=area;index=slot;}}
        ItemValue merged;if(candidate==null||!(r.BaseOnly?TryMergeBaseOnly(candidate.itemValue,r,out merged):TryMerge(candidate.itemValue,r,out merged)))return false;if(Image(candidate.itemValue)==Image(merged))return true;
        var next=new ItemStack(merged,1);if(areaFound==0)p.bag.SetSlot(index,next);else if(areaFound==1)p.inventory.SetItem(index,next);else {p.equipment.SetSlotItem(index,merged);p.PlayerUI?.xui?.PlayerEquipment?.RefreshEquipment();}
        var after=areaFound==0?p.bag.ItemGrid.items[index]:areaFound==1?p.inventory.ItemGrid.items[index]:new ItemStack(p.equipment.GetSlotItem(index),1);return after!=null&&after.count==1&&Image(after.itemValue)==Image(merged);
    }
    internal static void Pump(World world)
    {
        if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(GameManager.Instance?.World,world))return;WorldScope(world);if(!world.IsRemote())PruneUploads(world);var owner=world.GetPrimaryPlayer();if(owner==null)return;
        if(UnityEngine.Time.realtimeSinceStartup>=nextScan)
        {
            nextScan=UnityEngine.Time.realtimeSinceStartup+2f;
            try
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","repair-original","owner");if(Directory.Exists(directory))foreach(string path in Directory.EnumerateFiles(directory,"*.xml").Take(100001))
                {if(Clients.Values.Count(CanPumpOriginal)>=16)break;try{if(new FileInfo(path).Length>MaximumImage*12L)continue;var r=Decode(ReadRepairText(path));if(Clients.ContainsKey(r.Operation)||(r.Published||r.CancelConfirmed||r.BaseOnlyConfirmed)&&File.Exists(path+".done")&&ReadRepairText(path+".done")==Encode(r))continue;var c=new Client{Receipt=r,World=world,Player=owner,Directory=GameIO.GetSaveGameDir()};if(CanPumpOriginal(c))Clients.Add(r.Operation,c);}catch{/* Preserve the corrupt primary; continue unrelated original receipts. */}}
            }
            catch(Exception failure){Log.Warning("[REBIRTH Repair] owner journal recovery retained: "+failure.GetType().Name);}
        }
        foreach(var c in Clients.Values.ToArray())
        {
            if(!PublishPendingOutcome(c))continue;
            if(!CanPumpOriginal(c)||UnityEngine.Time.realtimeSinceStartup<c.Next)continue;c.Next=UnityEngine.Time.realtimeSinceStartup+1f;
            try
            {
                if(!File.Exists(PathFor(c.Receipt,false))&&!Save(c.Receipt,false))continue;
                if(c.NativePaid&&c.Entry!=null&&Read(c.Receipt,false,out var paid)&&Encode(paid)==Encode(c.Receipt))c.PaidAccepted=true;
                // A positively terminal owner original can outlive its episode
                // before archival. Retire only its metadata; never reapply an
                // old correction to a new-life item or request native effects.
                if(!CanRetireHistoricalTerminal(c))
                {if(c.Receipt.After.Length!=0&&!Apply(c))continue;UploadOwner(owner);}
                if(c.Receipt.Published||c.Receipt.CancelConfirmed||c.Receipt.BaseOnlyConfirmed)
                {
                    string path=PathFor(c.Receipt,false),done=path+".done";
                    if(!RepairPath(path)||!RepairPath(done)||!Read(c.Receipt,false,out var saved)||Encode(saved)!=Encode(c.Receipt))continue;
                    if(!File.Exists(done))File.Move(path,done);
                    else if(ReadRepairText(done)!=Encode(c.Receipt))continue;
                    Clients.Remove(c.Receipt.Operation);continue;
                }
                if(world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthRepairOriginalRequest>().Setup(owner.entityId,Encode(c.Receipt)));else ReceiveServer(world,owner.entityId,Encode(c.Receipt));
            }
            catch(Exception failure){Log.Warning("[REBIRTH Repair] original correction pending: "+failure.GetType().Name);}
        }
    }
    internal static void Install(Harmony harmony)
    {RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRepairOriginalActivation));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRepairOriginalRawItem));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRepairOriginalRawPacket));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRepairSignatureCancelPatch));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRepairOriginalNativeUpload));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRepairOriginalNativePlayerData));}
}
[HarmonyPatch(typeof(ItemActionEntryRepair),nameof(ItemActionEntryRepair.OnActivated))]
internal static class RebirthRepairOriginalActivation
{
    private sealed class Scope {internal object Prior;}
    private static void Prefix(ItemActionEntryRepair __instance,bool __runOriginal,out object __state)
    {__state=__runOriginal?new Scope{Prior=RebirthRepairOriginalCorrection.BeginActivation(__instance)}:null;}
    private static Exception Finalizer(Exception __exception,object __state,bool __runOriginal)
    {if(__state is Scope scope)RebirthRepairOriginalCorrection.EndActivation(scope.Prior,__exception,__runOriginal);return __exception;}
}
[HarmonyPatch(typeof(NetPackagePlayerInventory),nameof(NetPackagePlayerInventory.ProcessPackage))]
internal static class RebirthRepairOriginalNativeUpload
{private static void Postfix(NetPackagePlayerInventory __instance,World __0,bool __runOriginal){if(__runOriginal)RebirthRepairOriginalCorrection.NativeUpload(__instance,__0);}}
[HarmonyPatch(typeof(NetPackagePlayerData),nameof(NetPackagePlayerData.ProcessPackage))]
internal static class RebirthRepairOriginalNativePlayerData
{private static void Postfix(NetPackagePlayerData __instance,World __0,bool __runOriginal){if(__runOriginal)RebirthRepairOriginalCorrection.NativeUpload(__instance,__0);}}
[HarmonyPatch(typeof(ItemValue),nameof(ItemValue.ReadOrNull),typeof(PooledBinaryReader))]
internal static class RebirthRepairOriginalRawItem
{
    private static void Prefix(PooledBinaryReader __0,out long __state)=>__state=RebirthRepairOriginalCorrection.BeginRawItem(__0);
    private static void Postfix(PooledBinaryReader __0,ItemValue __result,long __state)=>RebirthRepairOriginalCorrection.EndRawItem(__0,__state,__result);
}
[HarmonyPatch(typeof(NetPackagePlayerData),nameof(NetPackagePlayerData.read))]
internal static class RebirthRepairOriginalRawPacket
{
    private static void Prefix(out object __state)=>__state=RebirthRepairOriginalCorrection.BeginRawCapture();
    private static Exception Finalizer(NetPackagePlayerData __instance,Exception __exception,object __state){RebirthRepairOriginalCorrection.EndRawCapture(__state,__instance.playerDataFile,__exception);return __exception;}
}
