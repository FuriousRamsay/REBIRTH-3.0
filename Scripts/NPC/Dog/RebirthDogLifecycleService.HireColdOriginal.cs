using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

public static partial class RebirthDogLifecycleService
{
    private sealed class HireNativeOwnerLoad
    {
        internal PlayerDataFile Data;internal World World;internal EntityPlayer Actor;internal string Save,Path,Owner;
        internal Stream SourceStream;internal HireNativeSpawn Spawn;internal byte[] NativeBelt,NativeBag,NativeBuffs;internal int Deaths;internal bool Dead;internal ItemStack[] BeltGrid,BagGrid,BeltRefs,BagRefs;internal byte[] Native;internal bool ReadEntered,ReadReturned,Loaded,Hydrated,HydrationEntered;internal IDisposable Budget;
    }
    private sealed class HireNativeDogLoad
    {internal EntityRebirthDogCompanion Dog;internal World World;internal string Save,Owner;internal RebirthNpcStableId Stable;}
    private static readonly object HireColdGate=new object();
    private static readonly Dictionary<PlayerDataFile,HireNativeOwnerLoad> HireNativeOwners=new Dictionary<PlayerDataFile,HireNativeOwnerLoad>();
    private static readonly Dictionary<EntityRebirthDogCompanion,HireNativeDogLoad> HireNativeDogs=new Dictionary<EntityRebirthDogCompanion,HireNativeDogLoad>();
    [ThreadStatic]private static HireNativeOwnerLoad EnteredHireNativeOwnerLoad;
    internal static void InstallSharedNativeFileOpenObserver()
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.shared.native-file-open"),typeof(HireOwnerNativeOpen));
    }
    internal static void InstallTradeNativeOwnerReadObservers()
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.trade.native-owner-read"),typeof(HireOwnerNativeRead));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.trade.native-owner-load"),typeof(HireOwnerNativeLoad));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.trade.native-owner-open"),typeof(HireOwnerNativeOpen));
        RebirthRemoteResourcePlacedStationOriginal.InstallHireNativeHydrationObservers();
    }
    internal static void InstallActionPaymentNativeOwnerLoadObservers()
    {
        var harmony=new Harmony("rebirth.action-payment.native-owner-load");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeLoad));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeRead));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeOpen));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.survivor.progression.3.1"),typeof(RebirthSelfMedicalNativeRestoreBootstrap));
        RebirthRemoteResourcePlacedStationOriginal.InstallHireNativeHydrationObservers();
    }
    internal static void InstallRecyclerNativeOwnerLoadObservers()
    {
        var harmony=new Harmony("rebirth.recycler.placed.native-owner-load");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeLoad));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeRead));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeOpen));
        RebirthRemoteResourcePlacedStationOriginal.InstallHireNativeHydrationObservers();
    }
    internal static void InstallHireNativeObservers()
    {
        if(HireRemoteReadBudget==null)RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out HireRemoteReadBudget);
        var harmony=new Harmony("rebirth.dog.hire.native-original.3.0");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeLoad));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeRead));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(HireOwnerNativeOpen));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.survivor.progression.3.1"),typeof(RebirthSelfMedicalNativeRestoreBootstrap));
        RebirthProductiveEarnedUiOwnerFile.Install();
        RebirthRemoteResourcePlacedStationOriginal.InstallHireNativeHydrationObservers();
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();
        var inventoryHarmony=new Harmony("rebirth.camper.native.actor-payment");
        RebirthHarmonyBootstrap.PatchClassOnce(inventoryHarmony,typeof(RebirthCamperNativeActorMutationGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(inventoryHarmony,typeof(RebirthCamperNativeActorCursorGuard));
    }
    internal static void ObserveHireNativeDogRead(EntityRebirthDogCompanion dog,global::StreamModeRead mode)
    {
        RetireReplacedHireWorld();
        ObserveHireRemoteDogBirth(dog,mode);
        if(mode!=global::StreamModeRead.Persistency||dog==null||!dog.NativeIdentityReadable||dog.RebirthSavedStableIdForDebug.IsEmpty||(!dog.NativeStableIdentity.IsEmpty&&dog.RebirthSavedStableIdForDebug!=dog.NativeStableIdentity))return;
        try
        {
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());if(!Directory.Exists(Path.Combine(save,"RebirthData","DogHire"))||!HasColdHireHold(dog.RebirthSavedStableIdForDebug)&&!HasRemoteHireOriginalForDog(dog.RebirthSavedStableIdForDebug))return;
            lock(HireColdGate)
            {
                if(HireNativeDogs.Count>=256&&!HireNativeDogs.ContainsKey(dog))return;
                HireNativeDogs[dog]=new HireNativeDogLoad{Dog=dog,World=dog.world??GameManager.Instance?.World,Save=save,Stable=dog.RebirthSavedStableIdForDebug,Owner=dog.RebirthSavedOwnerIdForDebug};
            }
        }
        catch{}
    }
    private static bool HasPendingHireAccount(string root,string account,bool includeRemoteTerminal=true)
    {
        RequireHirePath(root);var entries=Directory.EnumerateDirectories(root).Take(257).ToArray();if(entries.Length>256)return false;
        foreach(string directory in entries)
        {
            try
            {
                var prepared=ReadHireXml(Path.Combine(directory,"prepared.xml"));string text=(string)prepared.Element("original");
                if(!HasHireStage(directory,"prepared",text))continue;
                System.Xml.Linq.XElement image;using(var reader=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))image=System.Xml.Linq.XElement.Load(reader);
                if((string)image.Attribute("account")!=account)continue;
                bool terminal=false;foreach(string name in new[]{"committed","refunded","never-debited"})if(File.Exists(Path.Combine(directory,name+".xml"))&&HasHireStage(directory,name,text))terminal=true;
                if(!terminal||includeRemoteTerminal&&File.Exists(Path.Combine(directory,"remote-request.xml")))return true;
            }
            catch{/* An unparseable original cannot issue financial reentry. */}
        }
        return false;
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.Load),new[]{typeof(string),typeof(string)})]
    private static class HireOwnerNativeLoad
    {
        private sealed class Purpose{internal HireNativeOwnerLoad Hire;internal object Consumption,Recycler,ActionPayment,RecipeOutput,LocalTrade;}
        private static void Prefix(PlayerDataFile __instance,string _dir,string _playerName,bool __runOriginal,out Purpose __state)
        {
            object actionPayment=null;try{actionPayment=RemoteResourceTransactions.EnterActionPaymentHostOwnerLoad(__instance,_dir,_playerName,__runOriginal);}catch{}
            var consumption=RebirthNpcMetabolismCustody.EnterNativeOwnerLoad(__instance,_dir,_playerName,__runOriginal);
            var recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerOwnerLoad(__instance,_dir,_playerName,__runOriginal);
            if(__runOriginal)try{RebirthNpcLifecycle.PrepareNativeEpochBoundary();}catch{}
            EnterHire(__instance,_dir,_playerName,__runOriginal,out var hire);
            object recipeOutput=null;try{recipeOutput=RebirthRecipeOutputIntegration.EnterRecipeOutputOwnerLoad(__instance,_dir,_playerName,__runOriginal);}catch{}
            object localTrade=null;try{localTrade=RebirthNpcTradeLocalOwnerLoad.EnterLoad(__instance,_dir,_playerName,__runOriginal);}catch{}
            __state=hire==null&&consumption==null&&recycler==null&&actionPayment==null&&recipeOutput==null&&localTrade==null?null:new Purpose{Hire=hire,Consumption=consumption,Recycler=recycler,ActionPayment=actionPayment,RecipeOutput=recipeOutput,LocalTrade=localTrade};
        }
        private static void Postfix(PlayerDataFile __instance,bool __runOriginal,Purpose __state)
        {
            if(__state==null)return;
            try{RebirthNpcTradeLocalOwnerLoad.ObserveLoad(__instance,__runOriginal,__state.LocalTrade);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerLoad(__instance,__runOriginal,__state.RecipeOutput);}catch{}
            try{RemoteResourceTransactions.ObserveActionPaymentHostOwnerLoad(__instance,__runOriginal,__state.ActionPayment);}catch{}
            try{ObserveHire(__instance,__runOriginal,__state.Hire);}
            finally{try{RebirthNpcMetabolismCustody.ObserveNativeOwnerLoad(__instance,__runOriginal,__state.Consumption);}
                finally{RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerLoad(__instance,__runOriginal,__state.Recycler);}}
        }
        private static Exception Finalizer(Exception __exception,Purpose __state)
        {
            try{RebirthNpcTradeLocalOwnerLoad.EndLoad(__state?.LocalTrade,__exception);}catch{}
            try{RebirthRecipeOutputIntegration.EndRecipeOutputOwnerLoad(__state?.RecipeOutput);}catch{}
            try{RemoteResourceTransactions.EndActionPaymentHostOwnerLoad(__state?.ActionPayment);}catch{}
            if(__state!=null){try{if(__exception!=null&&__state.Hire!=null)End(__state.Hire);}finally{try{RebirthNpcMetabolismCustody.EndNativeOwnerLoad(__state.Consumption);}
                finally{RebirthRecyclerPlacedSettlement.EndRecyclerOwnerLoad(__state.Recycler);}}}
            return __exception;
        }
        private static void EnterHire(PlayerDataFile __instance,string _dir,string _playerName,bool __runOriginal,out HireNativeOwnerLoad __state)
        {
            __state=null;if(!__runOriginal||!ThreadManager.IsMainThread()||EnteredHireNativeOwnerLoad!=null||GameManager.Instance?.World==null)return;
            RetireReplacedHireWorld();
            try
            {
                string save=Path.GetFullPath(GameIO.GetSaveGameDir()),root=Path.Combine(save,"RebirthData","DogHire");
                bool retainedOwner=RebirthNpcSaveQuarantineBundle.NeedsIncomingNativeOwnerReentry(GameManager.Instance.World,save,_playerName);
                if(!retainedOwner&&(!Directory.Exists(root)||!HasPendingHireAccount(root,_playerName)))return;
                string path=Path.GetFullPath(Path.Combine(_dir,_playerName+"."+PlayerDataFile.EXT));if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
                lock(HireColdGate)
                {
                    if(HireNativeOwners.Count>=8||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var budget))return;
                    __state=new HireNativeOwnerLoad{Data=__instance,World=GameManager.Instance.World,Save=save,Path=path,Owner=_playerName,Budget=budget,Spawn=EnteredHireNativeSpawn!=null&&EnteredHireNativeSpawn.Owner==_playerName?EnteredHireNativeSpawn:null};EnteredHireNativeOwnerLoad=__state;
                }
            }
            catch{}
        }
        private static void ObserveHire(PlayerDataFile __instance,bool __runOriginal,HireNativeOwnerLoad __state)
        {
            if(__state==null)return;
            try
            {
                if(__instance.buffData!=null&&__instance.buffData.Length<=1048576&&ReadHireStamp(__instance.buffData.ToArray(),out _,out _,out int phase)&&phase==6&&
                    !HasPendingHireAccount(Path.Combine(__state.Save,"RebirthData","DogHire"),__state.Owner,false))return;
                if(__runOriginal&&ReferenceEquals(EnteredHireNativeOwnerLoad,__state)&&__state.ReadReturned&&__instance.bLoaded&&__state.Native!=null&&
                    RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(__instance).SequenceEqual(__state.Native)&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(__state.Path,__state.Native))
                {lock(HireColdGate){__state.NativeBelt=__instance.inventoryData.ToArray();__state.NativeBag=__instance.bagData.ToArray();__state.NativeBuffs=__instance.buffData.ToArray();__state.Deaths=__instance.deaths;__state.Dead=__instance.bDead;__state.Loaded=true;HireNativeOwners[__instance]=__state;}}
            }
            catch{/* Observational native load failure issues no reconstruction permit. */}
            finally{End(__state);}
        }
        private static void End(HireNativeOwnerLoad state)
        {
            if(ReferenceEquals(EnteredHireNativeOwnerLoad,state))EnteredHireNativeOwnerLoad=null;
            state.SourceStream=null;if(!state.Loaded){state.Budget?.Dispose();state.Budget=null;state.Native=null;state.Data=null;}
        }
    }
    [HarmonyPatch(typeof(SdFile),nameof(SdFile.OpenRead),new[]{typeof(string)})]
    private static class HireOwnerNativeOpen
    {
        private static void Postfix(string path,Stream __result,bool __runOriginal)
        {
            try{RebirthNpcTradeLocalOwnerLoad.ObserveOpen(path,__result,__runOriginal);}catch{}
            try{RemoteResourceTransactions.ObserveActionPaymentHostOwnerOpen(path,__result,__runOriginal);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerOpen(path,__result,__runOriginal);}catch{}
            try{RebirthCookingMillingPublication.ObserveMobileNativeOpen(path,__result,__runOriginal);}catch{}
            RebirthNpcMetabolismCustody.ObserveNativeOwnerOpen(path,__result,__runOriginal);
            RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerOpen(path,__result,__runOriginal);
            var source=EnteredHireNativeOwnerLoad;if(source==null||!__runOriginal||__result==null||source.SourceStream!=null)return;
            try{if(SameHirePath(path,source.Path)&&__result.CanRead&&__result.CanSeek&&__result.Position==0)source.SourceStream=__result;}catch{}
        }
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.Read),new[]{typeof(PooledBinaryReader),typeof(uint),typeof(global::StreamModeRead)})]
    private static class HireOwnerNativeRead
    {
        private sealed class Purpose{internal HireNativeOwnerLoad Local;internal object Remote,Consumption,Recycler,ActionPayment,RecipeOutput,Trade,RemoteRecipeOutput;}
        private static void Prefix(PlayerDataFile __instance,PooledBinaryReader _br,uint _version,global::StreamModeRead _eStreamMode,bool __runOriginal,out Purpose __state)
        {
            var consumption=RebirthNpcMetabolismCustody.EnterNativeOwnerRead(__instance,_br,_version,_eStreamMode,__runOriginal);
            var recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerOwnerRead(__instance,_br,_version,_eStreamMode,__runOriginal);
            object actionPayment=null;try{actionPayment=RemoteResourceTransactions.EnterActionPaymentNativeOwnerRead(__instance,_br,_version,_eStreamMode,__runOriginal);}catch{}
            var remote=EnterHireRemoteRead(__instance,_br,_version,_eStreamMode,__runOriginal);var source=EnteredHireNativeOwnerLoad;
            object recipeOutput=null;try{recipeOutput=RebirthRecipeOutputIntegration.EnterRecipeOutputOwnerRead(__instance,_br,_version,_eStreamMode,__runOriginal);}catch{}
            object trade=null;try{trade=RebirthNpcTradeOwnerLoad.EnterRead(__instance,_br,_version,_eStreamMode,__runOriginal);}catch{}
            object remoteRecipeOutput=null;try{remoteRecipeOutput=RebirthRecipeOutputIntegration.EnterRemoteOutputOwnerRead(__instance,_br,_version,_eStreamMode,__runOriginal);}catch{}
            __state=remote==null&&source==null&&consumption==null&&recycler==null&&actionPayment==null&&recipeOutput==null&&trade==null&&remoteRecipeOutput==null?null:new Purpose{Remote=remote,Consumption=consumption,Recycler=recycler,ActionPayment=actionPayment,RecipeOutput=recipeOutput,Trade=trade,RemoteRecipeOutput=remoteRecipeOutput};
            if(!__runOriginal||source==null||source.ReadEntered||!ReferenceEquals(source.Data,__instance)||_version!=PlayerDataFile.cFileVersion||_eStreamMode!=global::StreamModeRead.Persistency||_br?.BaseStream==null)return;
            try
            {
                var file=_br.BaseStream;if(!ReferenceEquals(file,source.SourceStream)||!file.CanSeek||file.Position!=5||file.Length<6||file.Length>16*1024*1024)return;
                long position=file.Position;var native=new byte[checked((int)file.Length)];
                try{file.Position=0;int offset=0;while(offset<native.Length){int count=file.Read(native,offset,native.Length-offset);if(count<1)return;offset+=count;}}
                finally{file.Position=position;}
                source.Native=native;source.ReadEntered=true;__state.Local=source;
            }
            catch{}
        }
        private static void Postfix(PooledBinaryReader _br,bool __runOriginal,Purpose __state)
        {
            if(__state==null)return;try{RemoteResourceTransactions.ObserveActionPaymentNativeOwnerRead(__runOriginal,__state.ActionPayment);}catch{}
            try{RebirthNpcTradeOwnerLoad.ObserveRead(__runOriginal,__state.Trade);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRemoteOutputOwnerRead(__runOriginal,__state.RemoteRecipeOutput);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerRead(_br,__runOriginal,__state.RecipeOutput);}catch{}
            RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerRead(_br,__runOriginal,__state.Recycler);RebirthNpcMetabolismCustody.ObserveNativeOwnerRead(_br,__runOriginal,__state.Consumption);ObserveHireRemoteRead(__runOriginal,__state.Remote);var local=__state.Local;if(local==null)return;try{local.ReadReturned=__runOriginal&&ReferenceEquals(EnteredHireNativeOwnerLoad,local)&&_br.BaseStream.Position==_br.BaseStream.Length;}catch{}
        }
        private static Exception Finalizer(Exception __exception,Purpose __state){if(__exception!=null){try{RebirthRecipeOutputIntegration.ObserveRemoteOutputOwnerRead(false,__state?.RemoteRecipeOutput);}catch{}try{RebirthNpcTradeOwnerLoad.ObserveRead(false,__state?.Trade);}catch{}try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerRead(null,false,__state?.RecipeOutput);}catch{}try{RemoteResourceTransactions.ObserveActionPaymentNativeOwnerRead(false,__state?.ActionPayment);}catch{}RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerRead(null,false,__state?.Recycler);RebirthNpcMetabolismCustody.ObserveNativeOwnerRead(null,false,__state?.Consumption);ObserveHireRemoteRead(false,__state?.Remote);}return __exception;}
    }
    internal static void EnterHireOwnerToPlayer(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool run)
    {
        RebirthNpcMetabolismCustody.EnterNativeOwnerHydration(data,actor,mode,run);
        EnterHireRemoteHydration(data,actor,mode,run);
        if(!run||!ThreadManager.IsMainThread()||mode!=global::StreamModeRead.Persistency||actor==null)return;
        lock(HireColdGate)try
        {
            if(!HireNativeOwners.TryGetValue(data,out var loaded)||loaded.Hydrated||loaded.HydrationEntered||!loaded.Loaded||
                !ReferenceEquals(loaded.World,actor.world)||!ReferenceEquals(actor.world,GameManager.Instance?.World)||
                (!loaded.Native.SequenceEqual(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data))&&!SameHireNativeSpawnInput(loaded,data)))return;
            loaded.Actor=actor;loaded.HydrationEntered=true;
        }catch{}
    }
    internal static void EndHireOwnerToPlayer(PlayerDataFile data)
    {
        RebirthNpcMetabolismCustody.EndNativeOwnerHydration(data);
        lock(HireColdGate)if(HireNativeOwners.TryGetValue(data,out var loaded)&&!loaded.Hydrated){loaded.HydrationEntered=false;loaded.Actor=null;}
    }
    // Composed into the existing sole actual ToPlayer postfix, independently of C traffic.
    internal static void ObserveHireOwnerToPlayer(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        RebirthNpcMetabolismCustody.ObserveNativeOwnerHydration(data,actor,run);
        ObserveHireRemoteHydration(data,actor,run);
        if(!run||!ThreadManager.IsMainThread()||actor==null)return;
        lock(HireColdGate)try
        {
            if(!HireNativeOwners.TryGetValue(data,out var loaded)||!loaded.Loaded||!loaded.HydrationEntered||!ReferenceEquals(loaded.Actor,actor)||!ReferenceEquals(loaded.World,actor.world)||!ReferenceEquals(actor.world,GameManager.Instance?.World))return;
            loaded.Actor=actor;loaded.BeltGrid=actor.inventory?.ItemGrid.items;loaded.BagGrid=actor.bag?.ItemGrid.items;
            loaded.BeltRefs=loaded.BeltGrid==null?null:(ItemStack[])loaded.BeltGrid.Clone();loaded.BagRefs=loaded.BagGrid==null?null:(ItemStack[])loaded.BagGrid.Clone();loaded.Hydrated=true;
        }catch{}
    }
    private static void PumpColdHireOriginals()
    {
        if(!ThreadManager.IsMainThread())return;
        lock(HireColdGate)
        {
            foreach(var owner in HireNativeOwners.Values.Where(value=>value.Hydrated).Take(8).ToArray())
                foreach(var body in HireNativeDogs.Values.Where(value=>ReferenceEquals(value.World,owner.World)).Take(256).ToArray())
                    if(body.Dog.RebirthRuntimeState!=null)TryCompleteColdHireOriginal(owner.World,owner.Actor,body.Dog);
        }
    }
    private static void TryCompleteColdHireOriginal(World world,EntityPlayer player,EntityRebirthDogCompanion dog)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||HireSettlements.ContainsKey(dog.RebirthRuntimeState.StableId))return;
        lock(HireColdGate)try
        {
            if(!HireNativeDogs.TryGetValue(dog,out var body)||!ReferenceEquals(body.World,world)||body.Stable!=dog.RebirthRuntimeState.StableId||!SameHirePath(body.Save,GameIO.GetSaveGameDir()))return;
            var loaded=HireNativeOwners.Values.SingleOrDefault(value=>value.Hydrated&&ReferenceEquals(value.Actor,player)&&ReferenceEquals(value.World,world)&&SameHirePath(value.Save,body.Save));if(loaded==null||loaded.Spawn!=null&&!loaded.Spawn.Returned||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var account)||account.CanonicalId!=loaded.Owner)return;
            string root=Path.Combine(body.Save,"RebirthData","DogHire");RequireHirePath(root);
            var pending=Directory.EnumerateDirectories(root,body.Stable+".*").Take(65).ToArray();if(pending.Length>64)return;
            foreach(string directory in pending)
            {
                var prepared=ReadHireXml(Path.Combine(directory,"prepared.xml"));string text=(string)prepared.Element("original");
                if(!HasHireStage(directory,"prepared",text))return;
                System.Xml.Linq.XElement original;using(var input=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))original=System.Xml.Linq.XElement.Load(input);
                if(original.Name!="dogHireOriginal"||(int?)original.Attribute("version")!=1||(string)original.Attribute("dog")!=body.Stable.ToString()||
                    (string)original.Attribute("account")!=loaded.Owner||!SameHirePath((string)original.Attribute("save"),body.Save)||
                    !Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation)||Path.GetFileName(directory)!=body.Stable+"."+operation.ToString("N"))return;
                bool alreadyTerminal=false;foreach(string name in new[]{"committed","refunded","never-debited"})if(File.Exists(Path.Combine(directory,name+".xml"))){if(!HasHireStage(directory,name,text))return;alreadyTerminal=true;}
                if(alreadyTerminal)
                {
                    string requestPath=Path.Combine(directory,"remote-request.xml");if(!File.Exists(requestPath)||!ReadHireStamp(loaded.NativeBuffs,out var actualNonce,out _,out int actualPhase)||actualPhase==6)continue;
                    var request=ReadHireXml(requestPath);if((string)request.Element("original")!=text||!Guid.TryParseExact((string)request.Attribute("nonce"),"N",out var historicalNonce)||historicalNonce!=actualNonce)continue;
                }
                var controlOriginal=new HireSettlement{Directory=directory,Original=text};string control=ReadHireControl(controlOriginal,out var sequence);
                if(control=="AwaitingBaseline")
                {if(sequence!=1)return;WriteHireRecord(controlOriginal,"never-debited");AdvanceHireControl(controlOriginal,"NeverDebited");RegisterRemoteHireTerminal(text,directory,loaded,body,true,false);TransferHireNativeLoad(loaded,body,operation.ToString("N"));return;}
                if(control=="NeverDebited"&&HasHireStage(directory,"never-debited",text))
                {RegisterRemoteHireTerminal(text,directory,loaded,body,true,false);TransferHireNativeLoad(loaded,body,operation.ToString("N"));return;}
                if(control=="ReadyDebit"||control=="Debited"||control=="ReadyOwnership"||control=="ReadyRefund"||control=="OwnedSaved")
                {if(control!="OwnedSaved"||!File.Exists(Path.Combine(directory,"owner-settled-saved.xml"))){TryReconstructUnattemptedHire(original,text,directory,control,body,loaded);return;}}
                bool owned=File.Exists(Path.Combine(directory,"ownership-saved.xml")),reversed=File.Exists(Path.Combine(directory,"reversal-saved.xml"));
                if(owned==reversed)return;
                string phase=owned?"settled":"refund",terminal=owned?"committed":"refunded",leg=owned?"ownership":"reversal";
                bool positiveTerminal=File.Exists(Path.Combine(directory,terminal+".xml"));
                if(File.Exists(Path.Combine(directory,terminal+".xml"))&&!HasHireStage(directory,terminal,text))return;
                if(!HireColdOwnerIntegrity(directory,"before")||!HireColdOwnerIntegrity(directory,phase))return;
                if(!HasHireStage(directory,owned?"ownership-saved":"reversal-saved",text)||!HasHireStage(directory,"owner-before-saved",text)||
                    !HasHireStage(directory,"owner-"+phase+"-saved",text)||
                    (positiveTerminal?!HasHireHistoricalOwnershipSeal(directory,leg,text):!MatchesHireSavedOwnership(directory,leg,text,body.Save)||
                        (body.Owner!=string.Empty&&body.Owner!=(string)original.Attribute("owner"))||!File.ReadAllBytes(Path.Combine(directory,"owner-"+phase+".native")).SequenceEqual(loaded.Native)))return;
                if(!owned&&!HasHireStage(directory,"refund-returned",text))return;
                // Already-saved terminal metadata ONLY: no native slot, owner, order,
                // debit or refund replay is issued by this reconstructed proof.
                var receipt=new HireSettlement{Directory=directory,Original=text};WriteHireRecord(receipt,terminal);RegisterRemoteHireTerminal(text,directory,loaded,body,false,!owned);TransferHireNativeLoad(loaded,body,operation.ToString("N"));return;
            }
        }
        catch{/* Mixed/attempted financial originals retain their hold; no absent receipt replay. */}
    }
}
