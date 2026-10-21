using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine.Scripting;

public static partial class RebirthDogLifecycleService
{
    internal static string ReadHireWire(PooledBinaryReader reader)
    {
        int length=reader.ReadInt32();if(length<0||length>262144)throw new InvalidDataException();
        byte[] bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
        return new System.Text.UTF8Encoding(false,true).GetString(bytes);
    }
    internal static void WriteHireWire(PooledBinaryWriter writer,string text)
    {
        if(text==null||text.Length>200000)throw new InvalidDataException();
        byte[] bytes=new System.Text.UTF8Encoding(false,true).GetBytes(text);if(bytes.Length>262144)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);
    }
    private sealed class RemoteHireRequest
    {
        internal Guid Nonce;internal EntityPlayerLocal Actor;internal EntityRebirthDogCompanion Dog;internal World World;
        internal INetConnection Connection;internal string Creation,Account,Sources,Original;internal int Deaths;
        internal readonly List<HireDebitSource> Slots=new List<HireDebitSource>();
        internal bool DebitEntered,Debited,RefundEntered,Refunded;internal IDisposable Budget;
        internal HireRemoteRead ColdRead;
    }
    private sealed class RemoteHireAdmission {internal Guid Nonce;internal string Sources;internal EntityPlayer Actor;internal EntityRebirthDogCompanion Dog;}
    private static RemoteHireRequest RemoteHireClient;
#if REBIRTH_DEBUG
    private static string RemoteHireScenarioHold,RemoteHireLastOriginal;
    private static object RemoteHireLastJoin;
    private static int RemoteHireNativeDebits,RemoteHireNativeRefunds,RemoteHireTerminals;
#endif
    [ThreadStatic]private static RemoteHireAdmission RemoteHireEntering;
    [ThreadStatic]private static object RemoteHireInventory;
    [ThreadStatic]private static ItemStack RemoteHireStack;
    [ThreadStatic]private static int RemoteHireSlot;
    [ThreadStatic]private static bool RemoteHirePermitUsed;
    internal static Guid CaptureRemoteHireRequest(int actorId,int dogId,out string sources)
    {
        sources=string.Empty;
        if(!ThreadManager.IsMainThread())return Guid.Empty;
        var world=GameManager.Instance?.World;var actor=world?.GetPrimaryPlayer();var dog=world?.GetEntity(dogId) as EntityRebirthDogCompanion;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];
        if(world?.IsRemote()!=true||actor==null||actor.entityId!=actorId||actor.IsDead()||dog==null||connection==null||RemoteHireClient!=null||CompetingHireCustody(actor)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return Guid.Empty;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        if(string.IsNullOrEmpty(creation)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out var budget))return Guid.Empty;
        var pending=new RemoteHireRequest{Nonce=Guid.NewGuid(),Actor=actor,Dog=dog,World=world,Connection=connection,Creation=creation,Account=owner.CanonicalId,Deaths=actor.Died,Budget=budget};
        try
        {
            int remaining=HireItemCount;var meat=ItemClass.GetItem(HireItemName);
            foreach(bool belt in new[]{true,false})
            {
                var grid=belt?actor.inventory.ItemGrid.items:actor.bag.ItemGrid.items;
                for(int slot=0;slot<grid.Length&&remaining>0;slot++)
                {
                    var stack=grid[slot];if(stack==null||stack.IsEmpty()||stack.itemValue.type!=meat.type)continue;
                    string encoded=NativeHireItem(stack);if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9Structure(encoded,out _))return Guid.Empty;
                    int take=Math.Min(remaining,stack.count);var after=stack.Clone();after.count-=take;
                    pending.Slots.Add(new HireDebitSource{Belt=belt,Slot=slot,Grid=grid,OriginalReference=stack,Before=stack.Clone(),After=after.count==0?ItemStack.Empty.Clone():after});remaining-=take;
                }
            }
            if(remaining!=0)return Guid.Empty;
            pending.Sources=RemoteHireSources(pending.Slots);RemoteHireClient=pending;StampHireClick(pending);sources=pending.Sources;
            return pending.Nonce;
        }
        finally{if(!ReferenceEquals(RemoteHireClient,pending))budget.Dispose();}
    }
    private static string RemoteHireSources(IEnumerable<HireDebitSource> slots)=>new XElement("sources",slots.Select(source=>new XElement("source",new XAttribute("belt",source.Belt),new XAttribute("slot",source.Slot),new XAttribute("beforeCount",source.Before.count),new XAttribute("afterCount",source.After.count),new XElement("beforeItem",NativeHireItem(source.Before)),new XElement("afterItem",NativeHireItem(source.After))))).ToString(SaveOptions.DisableFormatting);
    internal static bool ExecuteRemoteHireRequest(World world,EntityPlayer actor,EntityRebirthDogCompanion dog,Guid nonce,string sources,out string reason)
    {
        reason=HireRetained;if(nonce==Guid.Empty||sources==null||sources.Length>200000||RemoteHireEntering!=null)return false;
        var prior=RemoteHireEntering;RemoteHireEntering=new RemoteHireAdmission{Nonce=nonce,Sources=sources,Actor=actor,Dog=dog};
        try
        {
            bool success=TryHire(world,actor,dog,out reason);
            if(!success&&!HireSettlements.ContainsKey(dog.RebirthRuntimeState.StableId)&&!HasColdHireHold(dog.RebirthRuntimeState.StableId))
                SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId)?.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthDogHireOwnerGrant>().Setup(actor.entityId,nonce,3,sources));
            return success;
        }
        finally{RemoteHireEntering=prior;}
    }
    private static bool IsRemoteHire(HireSettlement original)=>!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Player);
    private static bool BindRemoteHire(HireSettlement original)
    {
        if(!IsRemoteHire(original))return true;
        var request=RemoteHireEntering;
        if(request==null||!ReferenceEquals(request.Actor,original.Player)||!ReferenceEquals(request.Dog,original.Dog)||request.Sources!=RemoteHireSources(original.Sources))return false;
        original.RemoteNonce=request.Nonce;original.RemoteClientReady=true;return true;
    }
    private static bool SendRemoteHire(HireSettlement original,byte phase)
    {
        if(!HireCurrent(original)||original.RemoteNonce==Guid.Empty)return false;
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Player.entityId);
        if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.CanonicalId!=original.Account)return false;
        peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthDogHireOwnerGrant>().Setup(original.Player.entityId,original.RemoteNonce,phase,original.Original));
        return true;
    }
    private static bool RemoteHireCurrent(RemoteHireRequest request)=>ReferenceEquals(RemoteHireClient,request)&&ReferenceEquals(request.World,GameManager.Instance?.World)&&
        ReferenceEquals(request.Actor.world,request.World)&&ReferenceEquals(request.World.GetPrimaryPlayer(),request.Actor)&&ReferenceEquals(request.World.GetEntity(request.Dog.entityId),request.Dog)&&
        request.Actor.Died==request.Deaths&&!request.Actor.IsDead()&&!request.Dog.IsDead()&&RebirthCamperCharacterAccess.CurrentCreation(request.Actor)==request.Creation&&
        RebirthStablePlayerIdentity.TryFromLocalPlatform(out var account)&&account.CanonicalId==request.Account&&
        ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],request.Connection);
    internal static bool HireBlocksInventory(object inventory)
    {
        var pending=RemoteHireClient;return LocalHireBlocksInventory(inventory)||RemoteHireInventoryCurrent(pending)&&(ReferenceEquals(inventory,pending.Actor.inventory)||ReferenceEquals(inventory,pending.Actor.bag));
    }
    internal static bool BeforeHireInventoryMutation(object inventory,MethodBase method,object[] args)
    {
        if(!HireBlocksInventory(inventory))return true;
        if(LocalHireBlocksInventory(inventory))return BeforeLocalHireInventoryMutation(inventory,method,args);
        if(RemoteHirePermitUsed||!ReferenceEquals(inventory,RemoteHireInventory)||args==null||args.Length<2||!(args[0] is int slot)||slot!=RemoteHireSlot)return false;
        bool exact=method.Name=="SetSlot"&&ReferenceEquals(args[1],RemoteHireStack)||method.Name=="SetItem"&&args.Length==3&&ReferenceEquals(args[1],RemoteHireStack?.itemValue)&&args[2] is int count&&count==RemoteHireStack.count;
        if(exact)RemoteHirePermitUsed=true;return exact;
    }
    internal static void ReceiveRemoteHireGrant(NetPackage packet,World world,int actor,Guid nonce,byte phase,string original)
    {
        try
        {
            var pending=RemoteHireClient;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||pending==null||!RemoteHireCurrent(pending)||actor!=pending.Actor.entityId||nonce!=pending.Nonce||phase>5||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,pending.Connection))return;
            if(phase==3&&pending.ColdRead==null)
            {
                if(pending.DebitEntered||pending.RefundEntered||original!=pending.Sources&&(pending.Original==null||original!=pending.Original))return;
                ReleaseRemoteHireClient(pending);return;
            }
            var image=ReadRemoteHireXml(original);
            if(pending.ColdRead!=null&&pending.Sources==null&&!BindColdRemoteHireSources(pending,image,phase==2||phase==3))return;
            if((string)image.Attribute("account")!=pending.Account||(string)image.Attribute("creation")!=pending.Creation||
                ((phase==2||phase==3)?(int?)image.Attribute("deaths")>pending.Deaths:(int?)image.Attribute("deaths")!=pending.Deaths)||
                (int?)image.Attribute("price")!=HireItemCount||(string)image.Attribute("dog")!=pending.Dog.RebirthRuntimeState.StableId.ToString()||
                new XElement("sources",image.Elements("source").Select(row=>new XElement(row))).ToString(SaveOptions.DisableFormatting)!=pending.Sources)return;
            if(pending.Original!=null&&pending.Original!=original)return;pending.Original=original;
            if(phase==3)
            {
                if(pending.DebitEntered||pending.RefundEntered)return;
                ReleaseRemoteHireClient(pending);return;
            }
            if(phase==4||phase==5)
            {
                if(phase==4&&pending.DebitEntered||phase==5&&(!pending.Debited||pending.RefundEntered))return;
                // A purpose-specific publication request never authorizes a setter.
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(pending.Actor),false);return;
            }
            if(phase==2)
            {
                if(!pending.Debited)return;
#if REBIRTH_DEBUG
                if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"){RemoteHireLastOriginal=pending.Original;RemoteHireTerminals++;}
#endif
                ReleaseRemoteHireClient(pending);return;
            }
            bool refund=phase==1;
            if(refund?!pending.Debited:pending.RefundEntered)return;
            if(refund?pending.Refunded:pending.Debited){PublishRemoteHireOwner(pending,phase);return;}
            if(refund?pending.RefundEntered:pending.DebitEntered)return;
            foreach(var source in pending.Slots)
            {
                var grid=source.Belt?pending.Actor.inventory.ItemGrid.items:pending.Actor.bag.ItemGrid.items;
                if(!ReferenceEquals(grid,source.Grid)||!ReferenceEquals(grid[source.Slot],refund?source.AppliedReference:source.OriginalReference)||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(grid[source.Slot],refund?source.After:source.Before))return;
            }
            string marker="rbDogHire_"+nonce.ToString("N");float expected=refund?1f:0f;
            if(pending.Actor.Buffs.GetCustomVar(marker)!=expected)return;
            // Persistent attempted marker precedes every native setter. No attempted operation is replayed.
            pending.Actor.Buffs.SetCustomVar(marker,refund?4f:2f,true);
            pending.Actor.Buffs.SetCustomVar("rbDogHirePhase",refund?4f:2f,true);
            if(refund)pending.RefundEntered=true;else pending.DebitEntered=true;
            foreach(var source in pending.Slots)
            {
                var stack=(refund?source.Before:source.After).Clone();RemoteHireInventory=source.Belt?(object)pending.Actor.inventory:pending.Actor.bag;RemoteHireStack=stack;RemoteHireSlot=source.Slot;RemoteHirePermitUsed=false;
                try{if(source.Belt)pending.Actor.inventory.SetItem(source.Slot,stack.itemValue,stack.count);else pending.Actor.bag.SetSlot(source.Slot,stack);}
                finally{RemoteHireInventory=null;RemoteHireStack=null;}
                if(!RemoteHirePermitUsed||!RemoteHireCurrent(pending)||!RebirthStationGridIngredients.IsSameStackSnapshot(source.Grid[source.Slot],refund?source.Before:source.After))return;
                if(!refund)source.AppliedReference=source.Grid[source.Slot];
            }
            if(refund)pending.Refunded=true;else pending.Debited=true;
            pending.Actor.Buffs.SetCustomVar(marker,refund?3f:1f,true);
            pending.Actor.Buffs.SetCustomVar("rbDogHirePhase",refund?5f:3f,true);
#if REBIRTH_DEBUG
            if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")
            {
                if(refund)RemoteHireNativeRefunds++;else RemoteHireNativeDebits++;
                if(RemoteHireScenarioHold==(refund?"after-refund-native":"after-debit-native"))throw new IOException("CodexTest actual remote native return held before receipt/PDS publication");
            }
#endif
            PublishRemoteHireOwner(pending,phase);
        }
        catch(Exception error){Log.Warning("[REBIRTH Dog] remote original payment retained: "+error.Message);}
    }
    private static XElement ReadRemoteHireXml(string text)
    {
        if(text==null||text.Length>1048576)throw new InvalidDataException();
        using var reader=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576});
        var image=XElement.Load(reader);if(image.Name!="dogHireOriginal"||image.Attributes().Count()!=14||image.Elements().Any(row=>row.Name!="source"))throw new InvalidDataException();return image;
    }
    private static void PublishRemoteHireOwner(RemoteHireRequest pending,byte phase)
    {
        if(!RemoteHireCurrent(pending))return;
        // Actual PDS upload is independent of the receipt; only native saved full PDS can settle the host.
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthDogHireOwnerReceipt>().Setup(pending.Actor.entityId,pending.Nonce,phase,pending.Original),false);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(pending.Actor),false);
    }
    internal static void ReceiveRemoteHireReceipt(World world,int actor,Guid nonce,byte phase,string text)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||phase>1)return;
        foreach(var original in HireSettlements.Values)
        {
            if(original.Player.entityId!=actor||original.RemoteNonce!=nonce||original.Original!=text||!HireCurrent(original))continue;
            if(phase==0&&original.EnteredDebit&&!original.Debited)
            {original.Debited=true;WriteHireRecord(original,"debited");AdvanceHireControl(original,"Debited");}
            else if(phase==1&&original.RefundEntered&&!original.Refunded&&original.ReversalSaved&&!original.OwnershipSaved)
            {original.Refunded=true;WriteHireRecord(original,"refund-returned");AdvanceHireControl(original,"RefundReturned");}
            return;
        }
    }
    internal static object CaptureRemoteHireClientScenario(string hold)
    {
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable CodexTest main thread required");
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer();if(actor?.world?.IsRemote()!=true)throw new InvalidOperationException("Actual remote owner client required");
        if(hold!=null)
        {
            if(hold!="none"&&hold!="after-debit-native"&&hold!="after-refund-native")throw new InvalidDataException("Unknown original boundary");
            if(RemoteHireClient!=null)throw new InvalidOperationException("Arm only before actual Hire input");
            RemoteHireScenarioHold=hold;RemoteHireNativeDebits=RemoteHireNativeRefunds=RemoteHireTerminals=0;RemoteHireLastOriginal=null;RemoteHireLastJoin=null;
        }
        var pending=RemoteHireClient;
        return new{available=true,remote=true,process=System.Diagnostics.Process.GetCurrentProcess().Id,actor=actor.entityId,held=pending!=null,current=pending!=null&&RemoteHireCurrent(pending),
            nonce=pending?.Nonce.ToString("N"),original=pending?.Original??RemoteHireLastOriginal,
            debitEntered=pending?.DebitEntered??false,debited=pending?.Debited??false,refundEntered=pending?.RefundEntered??false,refunded=pending?.Refunded??false,
            nativeDebits=RemoteHireNativeDebits,nativeRefunds=RemoteHireNativeRefunds,terminals=RemoteHireTerminals,
            nativeJoin=pending?.ColdRead==null?RemoteHireLastJoin:new{pending.ColdRead.Returned,pending.ColdRead.Hydrated,pending.ColdRead.Joined,pending.ColdRead.Phase,pending.ColdRead.Dog},
            belt=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)),bag=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items)),
            meat=actor.inventory.GetItemCount(ItemClass.GetItem(HireItemName),false,-1,-1)+actor.bag.GetItemCount(ItemClass.GetItem(HireItemName),-1,-1,false)};
#else
        return new{available=false,reason="DEBUG source required"};
#endif
    }
}

[Preserve]public sealed class NetPackageRebirthDogHireOwnerGrant:NetPackage
{
    private int actor;private Guid nonce;private byte phase;private string text;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthDogHireOwnerGrant Setup(int a,Guid n,byte p,string t){actor=a;nonce=n;phase=p;text=t;return this;}
    public override void read(PooledBinaryReader r){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);actor=r.ReadInt32();nonce=new Guid(r.ReadBytes(16));phase=r.ReadByte();text=RebirthDogLifecycleService.ReadHireWire(r);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(nonce.ToByteArray());w.Write(phase);RebirthDogLifecycleService.WriteHireWire(w,text);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthDogLifecycleService.ReceiveRemoteHireGrant(this,world,actor,nonce,phase,text);}
    public int GetLength()=>0;
}
[Preserve]public sealed class NetPackageRebirthDogHireOwnerReceipt:NetPackage
{
    private int actor;private Guid nonce;private byte phase;private string text;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthDogHireOwnerReceipt Setup(int a,Guid n,byte p,string t){actor=a;nonce=n;phase=p;text=t;return this;}
    public override void read(PooledBinaryReader r){actor=r.ReadInt32();nonce=new Guid(r.ReadBytes(16));phase=r.ReadByte();text=RebirthDogLifecycleService.ReadHireWire(r);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(nonce.ToByteArray());w.Write(phase);RebirthDogLifecycleService.WriteHireWire(w,text);}
    public override void ProcessPackage(World world,GameManager callbacks){if(ValidEntityIdForSender(actor))RebirthDogLifecycleService.ReceiveRemoteHireReceipt(world,actor,nonce,phase,text);}
    public int GetLength()=>0;
}

public sealed class ConsoleCmdRebirthDogHireClientScenario:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbdoghireclientaudit","rbdoghireclienthold"};
    public override string getDescription()=>"DEBUG CodexTest actual remote dog hire native payment observation.";
    public override string getHelp()=>"rbdoghireclientaudit | rbdoghireclienthold boundary=none|after-debit-native|after-refund-native confirm=CodexTest";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            string hold=args.SingleOrDefault(value=>value.StartsWith("boundary=",StringComparison.Ordinal))?.Substring(9);
            if(hold!=null&&!args.Contains("confirm=CodexTest"))throw new InvalidOperationException("Disposable confirmation required");
            Log.Out("[REBIRTH dog hire client scenario] "+Newtonsoft.Json.JsonConvert.SerializeObject(RebirthDogLifecycleService.CaptureRemoteHireClientScenario(hold)));
        }
        catch(Exception error){Log.Error("[REBIRTH dog hire client scenario] "+error.Message);}
    }
}
