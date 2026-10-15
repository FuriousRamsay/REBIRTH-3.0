using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// A local custody journal is NOT a server outcome. Native owner-file witnesses
// admit reservation/credit; missing server history never permits refund/replay.
internal static class RebirthVehicleClientCustody
{
    private sealed class Entry
    {
        internal Guid Id,ReserveReceipt,SettleReceipt,HandoffReceipt,ParentCustody;
        internal string Root,Owner,World,Creation,Frame,BeforeBag,BeforeBelt,CursorBefore,CursorAfter,Reserved,Pickup;
        internal string CreditBefore,CreditAfter,CreditBelt,CreditBeltAfter,HandoffCursor;
        internal int Phase,Outcome=3,CreditSlot=-1;internal bool DispatchStarted,SetterAttempted,ReservationAttempted,WantsCursor;
        internal object CapturedPlayer,CapturedWorld,CapturedBag,CapturedBagGrid,CapturedInventory,CapturedBeltGrid;
        internal bool LiveReservation,LiveCursor;
        internal bool AwaitingDrop,NativeDropAttempted,CancellationPending;
        internal bool ReturnRefusalShown;
        internal float NextReturnAttempt;
        internal int LiveCursorType,LiveCursorCount;
        internal XUiWindowGroup DropGroup;internal long DropGeneration;
        internal GUIWindowManager CloseManager;internal bool ClosePending,CloseFromEscape,CloseTimedOut;internal float CloseStarted;
        internal string DestinationPayload;
        internal IRebirthVehicleCursorDestinationWitness Destination;
        internal bool DestinationIsGear;
    }
    private static readonly Dictionary<Guid,Entry> entries=new Dictionary<Guid,Entry>();
    private static string loadedScope;private static bool unavailable,installed;private static float nextPoll;private static int warnings;
    [ThreadStatic]private static XUiC_ItemStack continuingTarget;
    [ThreadStatic]private static bool continuingDropOne;
    [ThreadStatic]private static EntityPlayerLocal returningOwner;
    [ThreadStatic]private static Guid returningOperation;
    [ThreadStatic]private static bool returnFailed;
    internal static void Install()
    {if(installed)return;var harmony=new HarmonyLib.Harmony("rebirth.vehicle.client.custody");
        if(HarmonyLib.AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),"HandleStackSwap",Type.EmptyTypes)==null||HarmonyLib.AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),"HandleDropOne",Type.EmptyTypes)==null)throw new MissingMethodException("Native cursor custody anchors unavailable.");
        RebirthVehicleCustodyUiGeneration.Install(harmony);RebirthVehicleCursorReturnHooks.Install(harmony);RebirthVehicleCursorDestinationWitness.Install(harmony);RebirthVehicleCursorGearHooks.Install(harmony);RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthVehicleClientCustodyDrop));ModEvents.GameUpdate.RegisterHandler(Tick);installed=true;}
    private static string Digest(string value)
    {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value??""))).Replace("-","").ToLowerInvariant();}
    internal static string FrameHash(string frame)=>Digest(frame);
    private static bool Scope(EntityPlayerLocal player,out string root,out string owner,out string world,out string creation)
    {
        root=owner=world=creation=null;
        if(!ThreadManager.IsMainThread()||player?.world==null||!ReferenceEquals(player.world,GameManager.Instance?.World)||
            !ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||!XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(player,out creation))return false;
        owner=identity.CanonicalId;world=player.world.worldState?.Guid;
        if(string.IsNullOrEmpty(owner)||!Guid.TryParse(world,out _))return false;
        try{root=Path.GetFullPath(GameIO.GetPlayerDataDir());return Directory.Exists(root);}catch{return false;}
    }
    private static string DirectoryFor(string root,string owner,string world,string creation)
        =>Path.Combine(root,"RebirthVehicleClientCustody",Digest(owner+"|"+world+"|"+creation));
    private static bool Current(EntityPlayerLocal player,Entry e)
        =>Scope(player,out var root,out var owner,out var world,out var creation)&&root==e.Root&&owner==e.Owner&&world==e.World&&creation==e.Creation&&
            (e.CapturedPlayer==null||ReferenceEquals(e.CapturedPlayer,player)&&ReferenceEquals(e.CapturedWorld,player.world)&&
            ReferenceEquals(e.CapturedBag,player.bag)&&ReferenceEquals(e.CapturedBagGrid,player.bag?.ItemGrid)&&
            ReferenceEquals(e.CapturedInventory,player.inventory)&&ReferenceEquals(e.CapturedBeltGrid,player.inventory?.ItemGrid));
    private static void Capture(EntityPlayerLocal player,Entry e)
    {e.CapturedPlayer=player;e.CapturedWorld=player.world;e.CapturedBag=player.bag;e.CapturedBagGrid=player.bag?.ItemGrid;e.CapturedInventory=player.inventory;e.CapturedBeltGrid=player.inventory?.ItemGrid;}
    private static string Stack(ItemStack item)
    {if(item==null)return "0:";return item.count.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+(item.itemValue==null||item.itemValue.IsEmpty()?"":RebirthNativeItemCodec.Encode(item.itemValue));}
    private static ItemStack ReadStack(string image)
    {
        int colon=image.IndexOf(':');if(colon<0||!int.TryParse(image.Substring(0,colon),out int count)||count<0||count>ushort.MaxValue)throw new InvalidDataException("Invalid custody stack count.");
        string payload=image.Substring(colon+1);if(payload.Length==0)return new ItemStack(ItemValue.None.Clone(),count);
        if(!RebirthNativeItemCodec.TryDecode(payload,out var value))throw new InvalidDataException("Invalid custody native item.");return new ItemStack(value,count);
    }
    private static string Slots(ItemStack[] slots)
    {if(slots==null||slots.Length>169)throw new InvalidDataException("Native custody grid unavailable.");return string.Join(";",slots.Select(Stack));}
    private static ItemStack[] ReadSlots(string image)
    {var rows=image.Split(';');if(rows.Length>169)throw new InvalidDataException("Custody grid exceeds native bound.");return rows.Select(ReadStack).ToArray();}
    private static string PathFor(Entry e)=>Path.Combine(DirectoryFor(e.Root,e.Owner,e.World,e.Creation),e.Id.ToString("N")+".xml");
    private static void Save(EntityPlayerLocal player,Entry e)
    {
        if(!Current(player,e))throw new InvalidOperationException("Vehicle custody session changed.");
        var xml=new XElement("vehicleClientCustody",new XAttribute("version",1),new XAttribute("id",e.Id),new XAttribute("parent",e.ParentCustody),new XAttribute("reserve",e.ReserveReceipt),new XAttribute("settle",e.SettleReceipt),new XAttribute("handoff",e.HandoffReceipt),new XAttribute("reservationAttempted",e.ReservationAttempted),
            new XAttribute("owner",e.Owner),new XAttribute("world",e.World),new XAttribute("creation",e.Creation),new XAttribute("phase",e.Phase),new XAttribute("outcome",e.Outcome),
            new XAttribute("dispatch",e.DispatchStarted),new XAttribute("attempted",e.SetterAttempted),new XAttribute("nativeDropAttempted",e.NativeDropAttempted),new XAttribute("cancelPending",e.CancellationPending),new XAttribute("destinationGear",e.DestinationIsGear),new XAttribute("creditSlot",e.CreditSlot),
            new XElement("frame",e.Frame),new XElement("beforeBag",e.BeforeBag),new XElement("beforeBelt",e.BeforeBelt),new XElement("cursorBefore",e.CursorBefore),new XElement("cursorAfter",e.CursorAfter),
            new XElement("reserved",e.Reserved??"0:"),new XElement("pickup",e.Pickup??"0:"),new XElement("destination",e.DestinationPayload??""),new XElement("handoffCursor",e.HandoffCursor??"0:"),new XElement("creditBefore",e.CreditBefore??""),new XElement("creditAfter",e.CreditAfter??""),new XElement("creditBelt",e.CreditBelt??""),new XElement("creditBeltAfter",e.CreditBeltAfter??""));
        string path=PathFor(e),temp=path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {new XDocument(xml).Save(output);if(output.Length>8*1024*1024)throw new InvalidDataException("Custody image exceeds bound.");output.Flush(true);}
        if(!Current(player,e))throw new InvalidOperationException("Vehicle custody session changed before publication.");
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
    private static Entry Read(string path,string root,bool temporary=false)
    {
        if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("Custody file exceeds bound.");
        XElement x;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024}))x=XElement.Load(reader);
        if(x.Name!="vehicleClientCustody"||(int)x.Attribute("version")!=1)throw new InvalidDataException("Unsupported custody journal.");
        var e=new Entry{Root=root,Id=Guid.Parse((string)x.Attribute("id")),ParentCustody=Guid.Parse((string)x.Attribute("parent")??Guid.Empty.ToString()),ReserveReceipt=Guid.Parse((string)x.Attribute("reserve")),SettleReceipt=Guid.Parse((string)x.Attribute("settle")),HandoffReceipt=Guid.Parse((string)x.Attribute("handoff")),ReservationAttempted=(bool)x.Attribute("reservationAttempted"),
            Owner=(string)x.Attribute("owner"),World=(string)x.Attribute("world"),Creation=(string)x.Attribute("creation"),Phase=(int)x.Attribute("phase"),Outcome=(int)x.Attribute("outcome"),
            DispatchStarted=(bool)x.Attribute("dispatch"),SetterAttempted=(bool)x.Attribute("attempted"),NativeDropAttempted=(bool?)x.Attribute("nativeDropAttempted")??false,CancellationPending=(bool?)x.Attribute("cancelPending")??false,DestinationIsGear=(bool?)x.Attribute("destinationGear")??false,CreditSlot=(int)x.Attribute("creditSlot"),Frame=(string)x.Element("frame"),
            BeforeBag=(string)x.Element("beforeBag"),BeforeBelt=(string)x.Element("beforeBelt"),CursorBefore=(string)x.Element("cursorBefore"),CursorAfter=(string)x.Element("cursorAfter"),Reserved=(string)x.Element("reserved"),Pickup=(string)x.Element("pickup"),
            CreditBefore=(string)x.Element("creditBefore"),CreditAfter=(string)x.Element("creditAfter"),CreditBelt=(string)x.Element("creditBelt"),CreditBeltAfter=(string)x.Element("creditBeltAfter"),HandoffCursor=(string)x.Element("handoffCursor")??"0:",DestinationPayload=(string)x.Element("destination")??""};
        if(e.Id==Guid.Empty||e.ReserveReceipt==Guid.Empty||e.SettleReceipt==Guid.Empty||e.HandoffReceipt==Guid.Empty||e.ReserveReceipt==e.SettleReceipt||e.ReserveReceipt==e.HandoffReceipt||e.SettleReceipt==e.HandoffReceipt||e.Phase<0||e.Phase>5||e.Outcome<0||e.Outcome>3||!Guid.TryParse(e.World,out _)||e.Owner==null||e.Creation==null||Path.GetFileName(path)!=e.Id.ToString("N")+".xml"+(temporary?".tmp":""))throw new InvalidDataException("Invalid custody identity/phase.");
        ReadSlots(e.BeforeBag);ReadSlots(e.BeforeBelt);ReadStack(e.CursorBefore);ReadStack(e.CursorAfter);ReadStack(e.Reserved);ReadStack(e.Pickup);
        if(!NetPackageRebirthVehicleAssemblyRequest.ReadCustodyFrame(e.Frame).MatchesCustodyIdentity(e.Id,e.Creation))throw new InvalidDataException("Custody frame identity mismatch.");
        if(e.Phase==2||e.Phase==4||e.Phase==5){ReadSlots(e.CreditBefore);ReadSlots(e.CreditBelt);}
        if(e.Phase==2||e.Phase==5)ReadSlots(e.CreditAfter);
        if(e.Phase==5){ReadSlots(e.CreditBeltAfter);ReadStack(e.HandoffCursor);}
        return e;
    }
    private static bool Load(EntityPlayerLocal player)
    {
        if(!Scope(player,out var root,out var owner,out var world,out var creation))return false;
        string scope=DirectoryFor(root,owner,world,creation);if(loadedScope==scope)return !unavailable;
        entries.Clear();loadedScope=scope;unavailable=false;
        try
        {
            if(!Directory.Exists(scope))return true;
            foreach(string temp in Directory.EnumerateFiles(scope,"*.tmp"))
                if(!RecoverPublication(player,temp,root))throw new InvalidDataException("Interrupted custody publication has no exact native final receipt.");
            foreach(string path in Directory.EnumerateFiles(scope,"*.xml"))
            {var e=Read(path,root);if(!Current(player,e)||entries.ContainsKey(e.Id))throw new InvalidDataException("Foreign/duplicate custody record.");Capture(player,e);entries.Add(e.Id,e);}
            return true;
        }
        catch(Exception error){unavailable=true;Warn(error);return false;}
    }
    private static bool RecoverPublication(EntityPlayerLocal player,string temporary,string root)
    {
        var next=Read(temporary,root,true);if(!Current(player,next))return false;
        string path=PathFor(next);if(!File.Exists(path)||temporary!=path+".tmp")return false;
        var prior=Read(path,root);if(!Current(player,prior)||prior.Id!=next.Id||prior.Owner!=next.Owner||prior.World!=next.World||prior.Creation!=next.Creation||
            prior.Frame!=next.Frame||prior.ReserveReceipt!=next.ReserveReceipt||prior.SettleReceipt!=next.SettleReceipt||prior.ParentCustody!=next.ParentCustody||
            prior.BeforeBag!=next.BeforeBag||prior.BeforeBelt!=next.BeforeBelt||prior.CursorBefore!=next.CursorBefore||prior.CursorAfter!=next.CursorAfter||prior.Reserved!=next.Reserved||
            prior.Pickup!=next.Pickup)return false;
        bool proven=next.Phase==1&&prior.Phase==0&&Saved(player,next,next.ReserveReceipt,1f,next.BeforeBag,next.BeforeBelt);
        if(next.Phase==2&&(prior.Phase==1||prior.Phase==2)||next.Phase==3&&prior.Phase==2)
            proven=next.Outcome==prior.Outcome&&next.Outcome>=0&&next.Outcome<=1&&Saved(player,next,next.SettleReceipt,1f,next.CreditAfter,next.CreditBelt);
        if(next.Phase==3&&prior.Phase==5&&string.IsNullOrEmpty(prior.DestinationPayload)&&string.IsNullOrEmpty(next.DestinationPayload)&&
            prior.HandoffReceipt==next.HandoffReceipt&&prior.CreditAfter==next.CreditAfter&&prior.CreditBeltAfter==next.CreditBeltAfter&&prior.HandoffCursor==next.HandoffCursor)
            proven=Saved(player,next,next.HandoffReceipt,1f,next.CreditAfter,next.CreditBeltAfter);
        if(!proven||!Current(player,next))return false;
        // Publish only the already-native-persisted record. No item setter,
        // inferred cursor debit, refund, deletion, or unrelated temp cleanup.
        File.Replace(temporary,path,null);return true;
    }
    private static bool Saved(EntityPlayerLocal player,Entry e,Guid receipt,float stage,string bag=null,string belt=null)
    {
        if(!Current(player,e)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||!RebirthGearNativePlayerFile.TryRead(identity,out var file)||file.buffData==null||file.buffData.Length>1024*1024)return false;
        var scopeReceipt=ScopedReceipt(e,receipt);
        var expected=stage==1f?RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
        if(!RebirthVehicleReceiptReader.TryRead(file.buffData.ToArray(),new[]{receipt,scopeReceipt},out var receipts)||
            receipts[receipt]!=expected||receipts[scopeReceipt]!=expected)return false;
        return Current(player,e)&&(bag==null||Slots(RebirthGearPlayerDataInventory.ReadSlots(file,true))==bag)&&
            (belt==null||Slots(RebirthGearPlayerDataInventory.ReadSlots(file,false))==belt);
    }
    private static Guid ScopedReceipt(Entry e,Guid receipt)
    {using(var sha=System.Security.Cryptography.SHA256.Create()){var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("vehicle-custody-v1|"+e.Owner+"|"+e.World+"|"+e.Creation+"|"+receipt.ToString("N")));var bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);}}
    private static bool Checkpoint(EntityPlayerLocal player,Entry e,Guid receipt,float stage,string bag,string belt)
    {
        if(!Current(player,e)||Slots(player.bag?.ItemGrid.items)!=bag||Slots(player.inventory?.ItemGrid.items)!=belt)return false;
        player.Buffs.SetCustomVar("rbVehicle_"+receipt.ToString("N"),stage,true);
        player.Buffs.SetCustomVar("rbVehicle_"+ScopedReceipt(e,receipt).ToString("N"),stage,true);
        if(!Current(player,e))return false;GameManager.Instance.SaveLocalPlayerData();
        return Saved(player,e,receipt,stage,bag,belt);
    }
    internal static bool HasSavedHandoff(EntityPlayerLocal owner,Guid operation,PlayerDataFile saved)
    {
        if(saved?.buffData==null||saved.buffData.Length>1024*1024||!Load(owner))return false;
        var e=entries.Values.FirstOrDefault(v=>v.Phase==5&&v.HandoffReceipt==operation&&Current(owner,v));
        if(e==null)return false;var scoped=ScopedReceipt(e,operation);
        return RebirthVehicleReceiptReader.TryRead(saved.buffData.ToArray(),new[]{operation,scoped},out var receipts)&&
            receipts[operation]==RebirthVehicleOwnerTransferResult.Applied&&receipts[scoped]==RebirthVehicleOwnerTransferResult.Applied&&
            Slots(RebirthGearPlayerDataInventory.ReadSlots(saved,true))==e.CreditAfter&&Slots(RebirthGearPlayerDataInventory.ReadSlots(saved,false))==e.CreditBeltAfter;
    }
    internal static bool Prepare(EntityPlayerLocal player,Guid id,NetPackageRebirthVehicleAssemblyRequest request,ItemStack cursor,ItemStack reserved,ItemStack pickup)
    {
        if(!Load(player)||!Scope(player,out var root,out var owner,out var world,out var creation))return false;
        var unresolved=entries.Values.Where(v=>v.Phase!=3).ToArray();
        var parent=unresolved.Length==1&&CanTransferHeld(player,reserved)?unresolved[0]:null;
        if(unresolved.Length!=0&&parent==null)return false;
        var after=cursor?.Clone()??ItemStack.Empty.Clone();if(reserved!=null&&!reserved.IsEmpty()){after.count--;if(after.count<=0)after=ItemStack.Empty.Clone();}
        var e=new Entry{Id=id,Root=root,Owner=owner,World=world,Creation=creation,ParentCustody=parent?.Id??Guid.Empty,
            ReserveReceipt=Guid.NewGuid(),SettleReceipt=Guid.NewGuid(),HandoffReceipt=Guid.NewGuid(),Frame=request.CaptureCustodyFrame(),BeforeBag=Slots(player.bag.ItemGrid.items),BeforeBelt=Slots(player.inventory.ItemGrid.items),
            CursorBefore=Stack(cursor),CursorAfter=Stack(after),Reserved=Stack(reserved),Pickup=Stack(pickup)};
        if(!XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(player,out e.Creation)||!Current(player,e))return false;
        Capture(player,e);e.LiveReservation=true;
        Save(player,e);entries.Add(id,e);return true;
    }
    private static bool Reserve(EntityPlayerLocal player,Entry e)
    {
        if(e.Phase!=0)return e.Phase>=1&&e.Phase<=3&&Saved(player,e,e.ReserveReceipt,1f);
        if(Saved(player,e,e.ReserveReceipt,1f,e.BeforeBag,e.BeforeBelt)){e.Phase=1;Save(player,e);RetireParent(player,e);return true;}
        // A cold cursor is transient and can coincidentally equal either image.
        // Only an already persisted final receipt can retire an attempted debit.
        if(e.ReservationAttempted&&!e.LiveReservation)return false;
        var cursor=player.PlayerUI?.xui?.DragAndDropWindow;if(cursor==null)return false;
        if(Slots(player.bag?.ItemGrid.items)!=e.BeforeBag||Slots(player.inventory?.ItemGrid.items)!=e.BeforeBelt)return false;
        if(player.Buffs.GetCustomVar("rbVehicle_"+e.ReserveReceipt.ToString("N"))==1f)
        {
            if(Stack(cursor.CurrentStack)!=e.CursorAfter||!Checkpoint(player,e,e.ReserveReceipt,1f,e.BeforeBag,e.BeforeBelt))return false;
            e.Phase=1;Save(player,e);RetireParent(player,e);return true;
        }
        if(!Saved(player,e,e.ReserveReceipt,2f,e.BeforeBag,e.BeforeBelt)&&!Checkpoint(player,e,e.ReserveReceipt,2f,e.BeforeBag,e.BeforeBelt))return false;
        string actual=Stack(cursor.CurrentStack);
        if(actual==e.CursorBefore&&e.CursorBefore!=e.CursorAfter)
        {if(e.ReservationAttempted)return false;e.ReservationAttempted=true;Save(player,e);cursor.SetCurrentStack(ReadStack(e.CursorAfter),false);}
        else if(actual!=e.CursorAfter)return false;
        if(!Current(player,e)||Stack(cursor.CurrentStack)!=e.CursorAfter)return false;
        if(!Checkpoint(player,e,e.ReserveReceipt,1f,e.BeforeBag,e.BeforeBelt))return false;
        e.Phase=1;Save(player,e);RetireParent(player,e);return true;
    }
    private static void RetireParent(EntityPlayerLocal player,Entry child)
    {
        if(child.ParentCustody==Guid.Empty)return;
        if(!entries.TryGetValue(child.ParentCustody,out var parent)||!Current(player,parent)||(parent.Phase!=4&&parent.Phase!=3)||parent.Pickup!=child.Reserved)
            throw new InvalidDataException("Cursor custody lineage changed.");
        if(parent.Phase==3)return;
        // The exact item is now in the child's saved reservation escrow. This
        // transfers custody only; server refusal settles the child's credit.
        parent.Phase=3;Save(player,parent);
    }
    private static bool Settle(EntityPlayerLocal player,Entry e)
    {
        if(e.Phase==3)return true;if(e.Phase==4||e.Phase==5||e.WantsCursor||e.Outcome!=0&&e.Outcome!=1)return false;
        ItemStack credit=ReadStack(e.Outcome==0?e.Reserved:e.Pickup);
        if(e.Phase==1)
        {
            var bag=player.bag.ItemGrid.items;var after=bag.Select(s=>s?.Clone()??ItemStack.Empty.Clone()).ToArray();
            int slot=-1;
            if(!credit.IsEmpty())
            {
                var locks=player.bag.LockedSlots;
                for(int i=0;i<bag.Length;i++)if(bag[i].IsEmpty()&&(locks==null||i>=locks.Length||!locks[i])){slot=i;break;}
                if(slot<0)return false;after[slot]=credit.Clone();
            }
            e.CreditSlot=slot;e.CreditBefore=Slots(bag);e.CreditAfter=Slots(after);e.CreditBelt=Slots(player.inventory.ItemGrid.items);e.Phase=2;Save(player,e);
        }
        // A durable applying marker plus exact postimage may finish saving, but
        // an attempted setter never runs twice, even if inventory later matches before.
        if(Saved(player,e,e.SettleReceipt,1f,e.CreditAfter,e.CreditBelt)){e.Phase=3;Save(player,e);return true;}
        if(!e.SetterAttempted)
        {
            if(!Checkpoint(player,e,e.SettleReceipt,2f,e.CreditBefore,e.CreditBelt))return false;
            e.SetterAttempted=true;Save(player,e);
            if(!Current(player,e)||Slots(player.bag.ItemGrid.items)!=e.CreditBefore||Slots(player.inventory.ItemGrid.items)!=e.CreditBelt)return false;
            if(e.CreditSlot>=0)player.bag.SetSlot(e.CreditSlot,ReadSlots(e.CreditAfter)[e.CreditSlot]);
        }
        if(!Checkpoint(player,e,e.SettleReceipt,1f,e.CreditAfter,e.CreditBelt))return false;
        e.Phase=3;Save(player,e);return true;
    }
    internal static bool Confirm(EntityPlayerLocal player,Guid id,RebirthVehicleRequestOutcome outcome,bool toCursor=false)
    {
        if(!Load(player)||!entries.TryGetValue(id,out var e))return true;
        if(outcome!=RebirthVehicleRequestOutcome.Applied&&outcome!=RebirthVehicleRequestOutcome.Rejected)return false;
        if(e.Phase<1)return false;
        if(e.Outcome!=3&&e.Outcome!=(int)outcome)return false;
        if(e.Phase==4||e.Phase==5)return false; // Attempted transient handoff is never replayed.
        e.WantsCursor=toCursor;
        e.Outcome=(int)outcome;Save(player,e);
        var pickup=ReadStack(e.Pickup);var cursor=player.PlayerUI?.xui?.DragAndDropWindow;
        if(toCursor&&outcome==RebirthVehicleRequestOutcome.Applied&&!pickup.IsEmpty()&&cursor?.IsEmpty()==true&&e.Phase==1)
        {
            e.CreditBefore=Slots(player.bag.ItemGrid.items);e.CreditBelt=Slots(player.inventory.ItemGrid.items);
            if(!Checkpoint(player,e,e.SettleReceipt,2f,e.CreditBefore,e.CreditBelt))return false;
            e.Phase=4;e.SetterAttempted=true;Save(player,e); // Intent survives before transient native setter.
            if(!Current(player,e)||!cursor.IsEmpty())return false;
            cursor.SetCurrentStack(pickup.Clone(),false);
            e.LiveCursor=true;
            e.LiveCursorType=pickup.itemValue.type;e.LiveCursorCount=pickup.count;
            // Phase4 remains escrow, not a claim of native cursor persistence.
            Checkpoint(player,e,e.SettleReceipt,1f,e.CreditBefore,e.CreditBelt);
            return Stack(cursor.CurrentStack)==e.Pickup;
        }
        return Settle(player,e);
    }
    internal static bool Owns(EntityPlayerLocal player,Guid id)=>Load(player)&&entries.TryGetValue(id,out var e)&&e.Phase!=3;
    internal static bool HasRecord(EntityPlayerLocal player,Guid id)=>Load(player)&&entries.ContainsKey(id);
    internal static bool MatchesPickup(EntityPlayerLocal player,Guid id,string payload)
    {
        if(string.IsNullOrEmpty(payload)||!Load(player)||!entries.TryGetValue(id,out var e)||!Current(player,e))return false;
        var item=ReadStack(e.Pickup);
        return !item.IsEmpty()&&item.count==1&&RebirthNativeItemCodec.Encode(item.itemValue)==payload;
    }
    internal static bool IsBusy(EntityPlayerLocal player)=>!Load(player)||entries.Values.Any(e=>e.Phase!=3);
    internal static bool HasLiveCursorCustody(EntityPlayerLocal player)
    {
        if(entries.Count==0||player==null||loadedScope==null||unavailable||!ThreadManager.IsMainThread())return false;
        var cursor=player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack;if(cursor==null||cursor.IsEmpty())return false;
        return entries.Values.Any(e=>e.Phase==4&&e.LiveCursor&&cursor.count==e.LiveCursorCount&&cursor.itemValue?.type==e.LiveCursorType&&ReferenceEquals(e.CapturedPlayer,player)&&
            ReferenceEquals(e.CapturedWorld,player.world)&&ReferenceEquals(player.world,GameManager.Instance?.World)&&
            ReferenceEquals(e.CapturedBag,player.bag)&&ReferenceEquals(e.CapturedBagGrid,player.bag?.ItemGrid)&&
            ReferenceEquals(e.CapturedInventory,player.inventory)&&ReferenceEquals(e.CapturedBeltGrid,player.inventory?.ItemGrid)&&
            Stack(player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack)==e.Pickup);
    }
    internal static bool IsPendingCursorHandoff(EntityPlayerLocal player)
    {
        if(entries.Count==0||player==null||!ThreadManager.IsMainThread())return false;
        return entries.Values.Any(e=>e.Phase==5&&e.AwaitingDrop&&ReferenceEquals(e.CapturedPlayer,player)&&ReferenceEquals(e.CapturedWorld,player.world)&&
            ReferenceEquals(player.world,GameManager.Instance?.World)&&Stack(player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack)==e.Pickup);
    }
    internal static bool BeforeNativeReturn(XUiC_DragAndDropWindow cursor,out Guid operation)
    {
        operation=Guid.Empty;var player=cursor?.xui?.playerUI?.entityPlayer;
        if(player==null)return true;
        if(returningOperation!=Guid.Empty&&ReferenceEquals(returningOwner,player)||IsPendingCursorHandoff(player))return false;
        if(!HasLiveCursorCustody(player))return true;
        var e=entries.Values.First(v=>v.Phase==4&&v.LiveCursor&&ReferenceEquals(v.CapturedPlayer,player)&&Stack(cursor.CurrentStack)==v.Pickup);
        if(UnityEngine.Time.realtimeSinceStartup<e.NextReturnAttempt)return false;
        if(!Current(player,e))return false;
        if(!RebirthVehicleCursorReturnPlan.TryPlan(player,cursor.CurrentStack,out var afterBag,out var afterBelt))
        {e.NextReturnAttempt=UnityEngine.Time.realtimeSinceStartup+1f;if(!e.ReturnRefusalShown){e.ReturnRefusalShown=true;GameManager.ShowTooltip(player,Localization.Get("xuiRebirthVehicleInventoryUnavailable"));}return false;}
        e.CreditBefore=Slots(player.bag.ItemGrid.items);e.CreditBelt=Slots(player.inventory.ItemGrid.items);
        e.CreditAfter=Slots(afterBag);e.CreditBeltAfter=Slots(afterBelt);e.HandoffCursor=Stack(ItemStack.Empty);
        e.Destination=null;e.DestinationPayload="";e.DestinationIsGear=false;e.CancellationPending=false;e.AwaitingDrop=false;
        e.HandoffReceipt=Guid.NewGuid();e.NativeDropAttempted=true;e.Phase=5;Save(player,e);
        returningOwner=player;returningOperation=e.Id;returnFailed=false;operation=e.Id;return true;
    }
    internal static bool AllowReturnDrop(XUiM_PlayerInventory inventory,ItemStack stack)
    {
        var pendingOwner=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(pendingOwner!=null&&ReferenceEquals(pendingOwner.PlayerUI?.xui?.PlayerInventory,inventory)&&IsPendingCursorHandoff(pendingOwner)&&
            Stack(stack)==Stack(pendingOwner.PlayerUI.xui.DragAndDropWindow.CurrentStack))return false;
        if(returningOperation==Guid.Empty||!ReferenceEquals(returningOwner?.PlayerUI?.xui?.PlayerInventory,inventory))return true;
        if(!entries.TryGetValue(returningOperation,out var e)||Stack(stack)!=e.Pickup||Stack(returningOwner.PlayerUI.xui.DragAndDropWindow.CurrentStack)!=e.Pickup)return true;
        returnFailed=true;return false; // Unexpected native AddItem refusal must not DropItem.
    }
    internal static bool AllowReturnCursorSet(XUiC_DragAndDropWindow cursor,ItemStack value)
    {
        var owner=cursor?.xui?.playerUI?.entityPlayer;
        if(owner!=null&&IsPendingCursorHandoff(owner)&&Stack(value)!=Stack(cursor.CurrentStack))return false;
        return !(returnFailed&&returningOperation!=Guid.Empty&&ReferenceEquals(returningOwner?.PlayerUI?.xui?.DragAndDropWindow,cursor)&&(value==null||value.IsEmpty()));
    }
    internal static void AfterNativeReturn(EntityPlayerLocal owner,Guid operation)
    {
        if(operation==Guid.Empty)return;
        if(returningOperation==operation){returningOperation=Guid.Empty;returningOwner=null;returnFailed=false;}
        AfterDestinationDrop(owner,operation);
        if(entries.TryGetValue(operation,out var e)&&e.Phase==4)e.NextReturnAttempt=UnityEngine.Time.realtimeSinceStartup+1f;
    }
    internal static bool CanTransferHeld(EntityPlayerLocal player,ItemStack reserved)
    {
        if(!Load(player)||reserved==null||reserved.IsEmpty())return false;
        var pending=entries.Values.Where(v=>v.Phase!=3).ToArray();
        return pending.Length==1&&pending[0].Phase==4&&pending[0].LiveCursor&&Current(player,pending[0])&&Stack(reserved)==pending[0].Pickup&&
            Stack(player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack)==pending[0].Pickup;
    }
    internal static bool BeforeDestinationDrop(XUiController target,ItemStack afterTarget,ItemStack afterCursor,IRebirthVehicleCursorDestinationWitness witness,out Guid operation)
    {
        operation=Guid.Empty;var player=target?.xui?.playerUI?.entityPlayer;
        if(player==null||!Load(player))return true;
        var e=entries.Values.FirstOrDefault(v=>v.Phase==4&&v.LiveCursor&&Current(player,v)&&Stack(player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack)==v.Pickup);
        if(e==null||witness==null)return true;
        e.Destination=witness;e.DestinationPayload=witness.Encode();
        e.DestinationIsGear=target is XUiC_EquipmentStack;
        e.NativeDropAttempted=false;e.CancellationPending=false;e.AwaitingDrop=false;
        e.ClosePending=false;e.CloseTimedOut=false;
        if(e.DestinationPayload.Length>2796204)throw new InvalidDataException("Native destination witness exceeds bound.");
        e.CreditBefore=e.CreditAfter=Slots(player.bag.ItemGrid.items);e.CreditBelt=e.CreditBeltAfter=Slots(player.inventory.ItemGrid.items);
        e.HandoffCursor=Stack(afterCursor);e.HandoffReceipt=Guid.NewGuid();e.Phase=5;
        Save(player,e);operation=e.Id;return true;
    }
    internal static bool BeforeDrop(XUiC_ItemStack target,bool dropOne,out Guid operation)
    {
        if(ReferenceEquals(continuingTarget,target)&&continuingDropOne==dropOne){operation=Guid.Empty;return true;}
        operation=Guid.Empty;var player=target?.xui?.playerUI?.entityPlayer;
        if(player==null||!Load(player))return true;
        var cursor=player.PlayerUI?.xui?.DragAndDropWindow;
        if(entries.Values.Any(v=>v.Phase==5&&v.AwaitingDrop&&Current(player,v)&&Stack(cursor?.CurrentStack)==v.Pickup))return false;
        var e=entries.Values.FirstOrDefault(v=>v.Phase==4&&v.LiveCursor&&Current(player,v)&&Stack(cursor?.CurrentStack)==v.Pickup);
        if(e==null)return true;
        bool bag=target.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack,belt=target.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt;
        if(target.IsLocked||target.StackLock||target.UserLockedSlot)return true;
        if(!RebirthVehicleCursorDropPlan.TryPlan(target,cursor.CurrentStack,dropOne,out var afterTarget,out var afterCursor))return true;
        if(!bag&&!belt)
        {
            if(!RebirthVehicleCursorDestinationWitness.TryCapture(target,afterTarget,out var witness))return true;
            return PrepareStorageDrop(player,e,target,dropOne,afterTarget,afterCursor,witness);
        }
        var beforeBag=player.bag.ItemGrid.items;var beforeBelt=player.inventory.ItemGrid.items;
        int index=target.SlotNumber;if(index<0||index>=(bag?beforeBag.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,beforeBelt.Length)))return false;
        var afterBag=beforeBag.Select(s=>s.Clone()).ToArray();var afterBelt=beforeBelt.Select(s=>s.Clone()).ToArray();
        e.Destination=null;e.DestinationPayload="";
        e.NativeDropAttempted=false;e.CancellationPending=false;
        (bag?afterBag:afterBelt)[index]=afterTarget;
        e.HandoffCursor=Stack(afterCursor);e.HandoffReceipt=Guid.NewGuid();
        e.CreditBefore=Slots(beforeBag);e.CreditBelt=Slots(beforeBelt);e.CreditAfter=Slots(afterBag);e.CreditBeltAfter=Slots(afterBelt);e.Phase=5;
        Save(player,e);player.Buffs.SetCustomVar("rbVehicle_"+e.HandoffReceipt.ToString("N"),2f,true);operation=e.Id;return true;
    }
    private static bool PrepareStorageDrop(EntityPlayerLocal player,Entry e,XUiC_ItemStack target,bool dropOne,ItemStack afterTarget,ItemStack afterCursor,IRebirthVehicleCursorDestinationWitness witness)
    {
        var group=target.windowGroup;long generation=RebirthVehicleCustodyUiGeneration.Read(group);
        string targetBefore=Stack(target.ItemStack),expectedTarget=Stack(afterTarget),expectedCursor=Stack(afterCursor);
        if(group?.isShowing!=true)return true;
        if(!BeforeDestinationDrop(target,afterTarget,afterCursor,witness,out var operation)||operation==Guid.Empty)return false;
        e.AwaitingDrop=true;
        e.DropGroup=group;e.DropGeneration=generation;
        Guid receipt=e.HandoffReceipt;
        GameManager.ShowTooltip(player,Localization.Get("xuiRebirthVehicleRequestPending"));
        bool accepted=RebirthVehicleCursorDestinationWitness.PrepareForDrop(witness,player,e.HandoffReceipt,e.Id,e.Frame,ready=>
        {
            try
            {
                if(!ThreadManager.IsMainThread()||!Current(player,e)||e.Phase!=5||e.HandoffReceipt!=receipt||!ReferenceEquals(e.Destination,witness)||!e.AwaitingDrop||e.NativeDropAttempted||e.CancellationPending)return;
                bool current=ReferenceEquals(target.windowGroup,group)&&group.isShowing&&RebirthVehicleCustodyUiGeneration.Read(group)==generation&&
                    Stack(target.ItemStack)==targetBefore&&Stack(player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack)==e.Pickup&&
                    !target.IsLocked&&!target.StackLock&&!target.UserLockedSlot&&
                    RebirthVehicleCursorDropPlan.TryPlan(target,player.PlayerUI.xui.DragAndDropWindow.CurrentStack,dropOne,out var nextTarget,out var nextCursor)&&Stack(nextTarget)==expectedTarget&&Stack(nextCursor)==expectedCursor;
                if(!ready||!current)
                {CancelStorageDrop(player,e,witness,receipt);return;}
                e.NativeDropAttempted=true;Save(player,e);e.AwaitingDrop=false;
                continuingTarget=target;continuingDropOne=dropOne;
                try{if(dropOne)target.HandleDropOne();else target.HandleStackSwap();}
                finally{continuingTarget=null;continuingDropOne=false;AfterDestinationDrop(player,operation);}
            }
            catch(Exception error){Warn(error);}
        });
        if(!accepted&&e.AwaitingDrop)CancelStorageDrop(player,e,witness,receipt);
        // The captured original gesture runs only in the acknowledged callback.
        return false;
    }
    internal static bool AllowWindowClose(GUIWindowManager manager,GUIWindow window,bool fromEscape)
    {
        if(entries.Count==0||!(window is XUiWindowGroup group)||!ThreadManager.IsMainThread())return true;
        var e=entries.Values.FirstOrDefault(v=>v.Phase==5&&v.NativeDropAttempted&&!v.DestinationIsGear&&v.Destination!=null&&ReferenceEquals(v.DropGroup,group)&&
            ReferenceEquals(v.CapturedPlayer,group.xui?.playerUI?.entityPlayer));
        if(e==null)return true;var owner=group.xui.playerUI.entityPlayer;
        if(!Current(owner,e)||!ReferenceEquals(group.windowManager,manager)||!group.isShowing||RebirthVehicleCustodyUiGeneration.Read(group)!=e.DropGeneration)return true;
        if(e.CloseTimedOut)return true; // UI timeout is not custody retirement.
        if(RebirthVehicleCursorDestinationWitness.CanCloseAfterDrop(e.Destination,owner,e.HandoffReceipt)){e.ClosePending=false;return true;}
        if(!e.ClosePending){e.ClosePending=true;e.CloseManager=manager;e.CloseFromEscape=fromEscape;e.CloseStarted=UnityEngine.Time.realtimeSinceStartup;GameManager.ShowTooltip(owner,Localization.Get("xuiRebirthVehicleDestinationSavePending"));}
        return false;
    }
    private static void ContinueWindowClose(EntityPlayerLocal owner)
    {
        foreach(var e in entries.Values.Where(v=>v.ClosePending).ToArray())
        {
            var group=e.DropGroup;
            if(!Current(owner,e)||group==null||!group.isShowing||RebirthVehicleCustodyUiGeneration.Read(group)!=e.DropGeneration||!ReferenceEquals(group.windowManager,e.CloseManager))
            {e.ClosePending=false;continue;}
            if(e.Destination!=null&&RebirthVehicleCursorDestinationWitness.CanCloseAfterDrop(e.Destination,owner,e.HandoffReceipt))
            {var manager=e.CloseManager;bool escape=e.CloseFromEscape;e.ClosePending=false;manager.Close(group,escape);}
            else if(UnityEngine.Time.realtimeSinceStartup-e.CloseStarted>=30f)
            {var manager=e.CloseManager;bool escape=e.CloseFromEscape;e.ClosePending=false;e.CloseTimedOut=true;GameManager.ShowTooltip(owner,Localization.Get("xuiRebirthVehicleDestinationSaveDelayed"));manager.Close(group,escape);}
        }
    }
    private static void CancelStorageDrop(EntityPlayerLocal player,Entry e,IRebirthVehicleCursorDestinationWitness witness,Guid receipt)
    {
        if(!Current(player,e)||e.Phase!=5||e.NativeDropAttempted||e.HandoffReceipt!=receipt)return;
        bool first=!e.CancellationPending;e.CancellationPending=true;e.AwaitingDrop=true;if(first)Save(player,e);
        RebirthVehicleCursorDestinationWitness.CancelForDrop(witness,player,receipt,e.Id,e.Frame,aborted=>
        {
            try
            {
                if(!aborted||!ThreadManager.IsMainThread()||!Current(player,e)||e.Phase!=5||e.NativeDropAttempted||e.HandoffReceipt!=receipt||!e.CancellationPending)return;
                // Abort proves only that this captured gesture did not run. The
                // original cursor escrow is retained, including cold lost cursor.
                e.CancellationPending=false;e.AwaitingDrop=false;e.Phase=4;e.Destination=null;e.DestinationPayload="";Save(player,e);
            }
            catch(Exception error){Warn(error);}
        });
        // Local or already-proven abort can complete synchronously above.
        // Do not report uncertainty after that callback has released the gesture.
        if(first&&e.CancellationPending)GameManager.ShowTooltip(player,Localization.Get("xuiRebirthVehicleDeliveryUnknown"));
    }
    internal static void AfterDrop(XUiC_ItemStack target,Guid operation)
    {
        AfterDestinationDrop(target?.xui?.playerUI?.entityPlayer,operation);
    }
    internal static void AfterDestinationDrop(EntityPlayerLocal player,Guid operation)
    {
        if(operation==Guid.Empty)return;
        if(player==null||!entries.TryGetValue(operation,out var e)||!Current(player,e)||e.Phase!=5)return;
        var cursor=player.PlayerUI?.xui?.DragAndDropWindow;
        if(Slots(player.bag.ItemGrid.items)==e.CreditBefore&&Slots(player.inventory.ItemGrid.items)==e.CreditBelt&&Stack(cursor?.CurrentStack)==e.Pickup)
        {e.Phase=4;e.Destination=null;e.DestinationPayload="";Save(player,e);return;} // Exact same live native gesture was refused; no automatic setter retry.
        FinishHandoff(player,e);
    }
    private static bool FinishHandoff(EntityPlayerLocal player,Entry e)
    {
        if(e.Phase!=5)return false;
        if(e.AwaitingDrop)return false;
        if(!string.IsNullOrEmpty(e.DestinationPayload))
        {
            if(e.Destination==null&&!(e.DestinationIsGear?RebirthVehicleCursorGearWitness.TryRead(e.DestinationPayload,out e.Destination):RebirthVehicleCursorDestinationWitness.TryRead(e.DestinationPayload,out e.Destination)))return false;
            if(!e.DestinationIsGear)
            {
                if(!e.Destination.RequestAndVerifySaved(player,e.HandoffReceipt)||!e.Destination.MatchesLive(player))return false;
                var cursor=player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack;
                if(cursor==null||!cursor.IsEmpty()&&!(e.LiveCursor&&e.NativeDropAttempted&&Stack(cursor)==e.HandoffCursor))return false;
                if(Saved(player,e,e.HandoffReceipt,1f)){e.Phase=3;Save(player,e);return true;}
                // The authenticated destination already proves the original item
                // was durably delivered. This checkpoint writes only receipts;
                // unrelated subsequent owner inventory changes are not undone.
                e.CreditAfter=Slots(player.bag.ItemGrid.items);e.CreditBeltAfter=Slots(player.inventory.ItemGrid.items);Save(player,e);
            }
            else
            {
                if(!e.Destination.MatchesLive(player))return false;
                if(!e.LiveCursor)
                {if(!Saved(player,e,e.HandoffReceipt,1f,e.CreditAfter,e.CreditBeltAfter)||!e.Destination.RequestAndVerifySaved(player,e.HandoffReceipt))return false;e.Phase=3;Save(player,e);return true;}
            }
        }
        else if(!e.LiveCursor)
        {if(!Saved(player,e,e.HandoffReceipt,1f,e.CreditAfter,e.CreditBeltAfter))return false;e.Phase=3;Save(player,e);return true;}
        if(!Checkpoint(player,e,e.HandoffReceipt,1f,e.CreditAfter,e.CreditBeltAfter))return false;
        if(e.Destination!=null&&!e.Destination.RequestAndVerifySaved(player,e.HandoffReceipt))return false;
        var remaining=ReadStack(e.HandoffCursor);var original=ReadStack(e.Pickup);
        if(!remaining.IsEmpty()&&remaining.itemValue.type==original.itemValue.type&&remaining.count<original.count)
        {if(Stack(player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack)!=e.HandoffCursor)return false;e.Pickup=e.HandoffCursor;e.LiveCursorType=remaining.itemValue.type;e.LiveCursorCount=remaining.count;e.Phase=4;}
        else e.Phase=3;
        Save(player,e);return e.Phase==3;
    }
    internal static void Detach(){entries.Clear();loadedScope=null;unavailable=false;}
    internal static void Observe(World world,Guid id,string game,string creation,string frameHash,RebirthVehicleRequestOutcome outcome,string nativePickup=null)
    {
        var player=world?.GetPrimaryPlayer();
        if(player==null||!Load(player)||!entries.TryGetValue(id,out var e)||!Current(player,e)||e.World!=game||e.Creation!=creation||Digest(e.Frame)!=frameHash)return;
        if(outcome==RebirthVehicleRequestOutcome.Applied&&!ReadStack(e.Pickup).IsEmpty()&&!MatchesPickup(player,id,nativePickup))return;
        try{Confirm(player,id,outcome);}catch(Exception error){Warn(error);}
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();if(player==null||player.IsDead()||!player.IsSpawned())return;
        try{ContinueWindowClose(player);}catch(Exception error){Warn(error);}
        if(UnityEngine.Time.realtimeSinceStartup<nextPoll)return;
        nextPoll=UnityEngine.Time.realtimeSinceStartup+2f;
        try
        {
            if(!Load(player))return;
            foreach(var e in entries.Values.Where(v=>v.Phase!=3).ToArray())
            {
                if(!Current(player,e))return;
                if(e.Phase==5)
                {
                    if(e.CancellationPending&&!e.NativeDropAttempted)
                    {if(e.Destination==null)RebirthVehicleCursorDestinationWitness.TryRead(e.DestinationPayload,out e.Destination);if(e.Destination!=null)CancelStorageDrop(player,e,e.Destination,e.HandoffReceipt);}
                    else FinishHandoff(player,e);
                    continue;
                }
                if(e.Phase==4)continue;
                if(e.WantsCursor&&!XUiC_RebirthVehicleRestoration.IsOpenForCustody(player,e.Id))e.WantsCursor=false;
                if(e.Phase==0&&!Reserve(player,e))continue;
                if(e.ParentCustody!=Guid.Empty&&e.Phase>=1&&e.Phase<=3&&Saved(player,e,e.ReserveReceipt,1f))RetireParent(player,e);
                if(e.Outcome==0||e.Outcome==1){Settle(player,e);continue;}
                if(!Saved(player,e,e.ReserveReceipt,1f))continue;
                if(!e.DispatchStarted)
                {
                    e.DispatchStarted=true;Save(player,e);
                    var request=NetPackageRebirthVehicleAssemblyRequest.ReadCustodyFrame(e.Frame);
                    request.DispatchCustody(player); // First admission only; never inferred from a missing query.
                }
                else
                {
                    var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
                    if(player.world.IsRemote())connection?.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthVehicleCustodyQuery>().Setup(e.Id,e.World,e.Creation,e.Frame));
                    else NetPackageRebirthVehicleAssemblyRequest.QueryCustody(player.world as World,null,e.Id,e.World,e.Creation,e.Frame);
                }
            }
        }
        catch(Exception error){Warn(error);}
    }
    internal static void Warn(Exception error)
    {if(warnings++<16)Log.Warning("[REBIRTH Vehicle Custody] unresolved native item custody retained: "+error.GetType().Name);}
}

[HarmonyLib.HarmonyPatch]
internal static class RebirthVehicleClientCustodyDrop
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {yield return HarmonyLib.AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),"HandleStackSwap",Type.EmptyTypes);yield return HarmonyLib.AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),"HandleDropOne",Type.EmptyTypes);}
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]private static bool Prefix(XUiC_ItemStack __instance,System.Reflection.MethodBase __originalMethod,out Guid __state)
    {__state=Guid.Empty;try{return RebirthVehicleClientCustody.BeforeDrop(__instance,__originalMethod.Name=="HandleDropOne",out __state);}catch(Exception error){RebirthVehicleClientCustody.Warn(error);return false;}}
    private static Exception Finalizer(XUiC_ItemStack __instance,Guid __state,Exception __exception)
    {try{RebirthVehicleClientCustody.AfterDrop(__instance,__state);}catch{}return __exception;}
}
