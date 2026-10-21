using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;

// Native owner publication observation. No parsed receipt can instantiate a
// Witness, execute progression, or replace the actual native owner callbacks.
internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    private const int Maximum=16*1024*1024;
    internal sealed class Witness:IDisposable
    {
        internal readonly RebirthProductiveEarnedUiOriginal.BindingToken Binding;
        internal readonly string PlayerRoot,PlayerName,RecipeName;
        internal byte[] Before,After,Final;internal string FinalPath;
        internal bool Completed,Closed;internal IDisposable Allocation;
        internal SharedCapture NativeBudget;
        internal Witness(RebirthProductiveEarnedUiOriginal.BindingToken binding,byte[] before,IDisposable allocation,string root,string name)
        {Binding=binding;Before=before;NativeBudget=new SharedCapture(allocation,1);Allocation=NativeBudget.Owner();PlayerRoot=root;PlayerName=name;RecipeName=binding.Recipe.GetName();}
        internal bool Current()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,Binding.World)&&
            ReferenceEquals(Binding.Actor.world,Binding.World)&&ReferenceEquals(Binding.World.GetPrimaryPlayer(),Binding.Actor)&&
            ReferenceEquals(Binding.World.GetEntity(Binding.Actor.entityId),Binding.Actor)&&!Binding.Actor.IsDead()&&Binding.Actor.Died==Binding.Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Binding.Actor)==Binding.Creation&&
            PlayerRoot==Path.GetFullPath(GameIO.GetPlayerDataDir())&&
            Binding.SaveDirectory==Path.GetFullPath(GameIO.GetSaveGameDir())&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Binding.Owner&&identity.CanonicalId==PlayerName;
        internal bool ObserveDelivery(RebirthRecipeOutputIntegration.OwningDelivery delivery)
        {
            if(!Current()||After!=null||delivery==null||!delivery.IsCurrent()||delivery.Operation!=Binding.Operation||
                !ReferenceEquals(delivery.Viewer,Binding.Actor)||!ReferenceEquals(delivery.Station,Binding.Station)||
                delivery.StartingOwnerEntityId!=Binding.StartingOwnerEntityId)return false;
            After=Live(Binding.Actor,Binding.Recipe.GetName());return After!=null;
        }
        internal bool Saved()
        {
            lock(gate)
            {
                return Current()&&Completed&&After!=null&&Final!=null&&FinalPath!=null&&
                    ReadFinal(FinalPath,Final);
            }
        }
        public void Dispose(){lock(gate){if(Closed)return;Closed=true;Before=After=Final=null;Allocation?.Dispose();Allocation=null;NativeBudget=null;active.Remove(this);}}
    }
    internal sealed class RemoteWitness:IDisposable
    {
        internal RebirthProductiveEarnedUiRemote.Registration Original;internal byte[] Expected,Final;
        internal string PlayerRoot,PlayerName,Path,RecipeName;internal bool Accepted,Completed,Closed;internal IDisposable Allocation;
        internal SharedCapture NativeBudget;
        internal bool Current()=>!Closed&&ThreadManager.IsMainThread()&&PlayerRoot==System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir())&&Original.Current();
        internal bool Saved()=>Current()&&Accepted&&Completed&&Final!=null&&ReadFinal(Path,Final);
        public void Dispose(){lock(gate){if(Closed)return;Closed=true;Expected=Final=null;Original=null;Allocation?.Dispose();Allocation=null;NativeBudget=null;remote.Remove(this);}}
    }
    private sealed class NativeFile{internal Witness Witness;internal RemoteWitness Remote;internal DeferredWitness Deferred;internal DeferredWitness[] DeferredGroup;internal LocalOwnerScope LocalBefore,LocalAfter;internal int NativeLength;internal byte[] Image;internal string Path,NativeHash;internal IDisposable DeferredWorkerRetention;}
    private static readonly object gate=new object();
    private static readonly List<Witness> active=new List<Witness>();
    private static readonly List<RemoteWitness> remote=new List<RemoteWitness>();
    private static readonly ConditionalWeakTable<PlayerDataFile,NativeFile> files=new ConditionalWeakTable<PlayerDataFile,NativeFile>();
    [ThreadStatic]private static NetPackagePlayerData readingPlayer;
    private static bool installed;
    internal static bool MatchesRemoteBefore(EntityPlayer actor,ClientInfo sender,
        RebirthProductiveEarnedUiOriginal.UiFrame frame,Recipe recipe)
    {
        try
        {
            var data=sender?.latestPlayerData;
            if(!ThreadManager.IsMainThread()||actor==null||data==null||data.id!=actor.entityId||data.deaths!=frame.Deaths||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||owner.StorageKey!=frame.Owner||
                !Projection(data,recipe.GetName()).SequenceEqual(frame.OwnerBefore)||
                !RebirthProductiveEarnedArtifact.SameTools(RebirthGearPlayerDataInventory.ReadSlots(data,true),frame.Bag)||
                !RebirthProductiveEarnedArtifact.SameTools(RebirthGearPlayerDataInventory.ReadSlots(data,false),frame.Belt))return false;
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            string path=Path.GetFullPath(Path.Combine(root,owner.CanonicalId+"."+PlayerDataFile.EXT));
            return Path.GetDirectoryName(path)==root&&ReadFinal(path,NativeImage(data));
        }
        catch{return false;}
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.productive.ui.owner.file");
        foreach(var type in new[]{typeof(FromPlayer),typeof(Save),typeof(PlayerUpload),typeof(AcceptedPlayer)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        InstallDeferred(harmony);
        installed=true;
    }
    internal static RemoteWitness BeginRemote(RebirthProductiveEarnedUiRemote.Registration original)
    {
        if(original?.Current()!=true||original.OwnerAfter==null||!RebirthStablePlayerIdentity.TryFromClientInfo(original.Sender,out var owner))return null;
        lock(gate)
        {
            if(remote.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(4L*Maximum,out var allocation))return null;
            var result=new RemoteWitness{Original=original,Expected=(byte[])original.OwnerAfter.Clone(),PlayerRoot=Path.GetFullPath(GameIO.GetPlayerDataDir()),PlayerName=owner.CanonicalId,
                RecipeName=original.Station.Queue[original.Frame.Index].Recipe.GetName(),NativeBudget=new SharedCapture(allocation,1)};
            result.Allocation=result.NativeBudget.Owner();
            remote.Add(result);return result;
        }
    }
    internal static Witness Begin(RebirthProductiveEarnedUiOriginal.BindingToken binding)
    {
        if(!ThreadManager.IsMainThread()||binding?.Current()!=true)return null;
        lock(gate)
        {
            if(active.Count>=4||active.Any(value=>ReferenceEquals(value.Binding.Actor,binding.Actor)))return null;
            // Full saved file and native capture graphs are reserved before any
            // detached native snapshots are retained, including worker Save.
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(4L*Maximum,out var allocation))return null;
            try{if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidDataException();
                var before=Live(binding.Actor,binding.Recipe.GetName());var result=new Witness(binding,before,allocation,Path.GetFullPath(GameIO.GetPlayerDataDir()),owner.CanonicalId);active.Add(result);return result;}
            catch{allocation.Dispose();return null;}
        }
    }
    private static byte[] Live(EntityPlayer player,string recipe)
    {
        using(var stream=new Limited())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);player.Progression.Write(writer);
            player.QuestJournal.Write(writer);player.challengeJournal.Write(writer);writer.Write(player.totalItemsCrafted);
            writer.Write(player.Buffs.GetCustomVar("_craftCount_"+recipe));
            writer.Flush();if(stream.Length>Maximum)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static byte[] Projection(PlayerDataFile data,string recipe)
    {
        using(var stream=new Limited())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);writer.Write(data.progressionData.ToArray());
            data.questJournal.Write(writer);data.challengeJournal.Write(writer);writer.Write(data.totalItemsCrafted);
            writer.Write(CraftCount(data.buffData,recipe));
            writer.Flush();if(stream.Length>Maximum)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static float CraftCount(MemoryStream bytes,string recipe)
    {
        if(bytes==null||bytes.Length>Maximum)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)throw new InvalidDataException();
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();bool found=false;float result=0;
            for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key!="_craftCount_"+recipe)continue;if(found||float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException();found=true;result=value;}
            if(stream.Position!=stream.Length)throw new InvalidDataException();return result;
        }
    }
    private static byte[] NativeImage(PlayerDataFile data,int maximum=Maximum)
    {
        using(var stream=new Limited(maximum))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);
            data.Write(writer,StreamModeWrite.Persistency);writer.Flush();if(stream.Length>Maximum)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static bool ReadFinal(string path,byte[] expected)
    {
        try
        {
            if(!Safe(path)||File.Exists(path+".tmp")||!SdFile.Exists(path))return false;
            using(var stream=SdFile.OpenRead(path))
            {if(stream.Length!=expected.Length)return false;var buffer=new byte[8192];int offset=0,read;
             while((read=stream.Read(buffer,0,buffer.Length))>0){for(int i=0;i<read;i++)if(buffer[i]!=expected[offset+i])return false;offset+=read;}return offset==expected.Length&&Safe(path);}
        }
        catch{return false;}
    }
    private static bool Safe(string path)
    {
        try
        {
            string current=Path.GetFullPath(path);
            while(current!=null)
            {
                try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;}
                catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
                current=Path.GetDirectoryName(current);
            }
            return true;
        }
        catch{return false;}
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.FromPlayer))]
    private static class FromPlayer
    {
        private static void Postfix(PlayerDataFile __instance,EntityPlayer _player,bool __runOriginal)
        {
            // Independent purpose observer must see the actual native capture
            // even when another owner-file cohort claims this same event.
            try{RebirthRemoteResourcePlacedStationOriginal.ObserveCancellationOwnerFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthWorkstationCompletionPipeline.ObserveNativeViewerFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthWorkstationCompletionPipeline.ObserveCachedOwnerEffectFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthDogLifecycleService.ObserveHireOwnerFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthNpcMetabolismCustody.ObserveOwnerFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthConsumptionDurableDelivery.ObserveInventoryOwnerFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RemoteResourceTransactions.ObserveActionPaymentFromPlayer(__instance,_player,__runOriginal);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerFromPlayer(__instance,_player,__runOriginal);}catch{}
            if(!__runOriginal||!ThreadManager.IsMainThread())return;
            if(ObserveLocalBeforePlayer(__instance,_player))return;
            ObserveDeferredPlayer(__instance,_player);
            try
            {
                lock(gate)
                {
                    var witness=active.SingleOrDefault(value=>value.After!=null&&value.Current()&&ReferenceEquals(value.Binding.Actor,_player));
                    if(witness==null||__instance.id!=_player.entityId||__instance.deaths!=witness.Binding.Deaths||
                        !Projection(__instance,witness.Binding.Recipe.GetName()).SequenceEqual(witness.After))return;
                    files.Remove(__instance);files.Add(__instance,new NativeFile{Witness=witness,NativeHash=Hash(NativeImage(__instance))});
                }
            }
            catch{/* Unknown native capture never becomes owner-file authority. */}
        }
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.Save))]
    private static class Save
    {
        private sealed class Observation {internal NativeFile Native;internal object HostPds,CancellationOwner,NativeViewer,CachedOwnerEffect,HireOwner,MetabolismOwner,ConsumptionOwner,RecyclerOwner,ActionPaymentOwner,RecipeOutputOwner,NativeToolOwner;internal bool Finished;}
        private static void Prefix(PlayerDataFile __instance,string _dir,string _playerId,bool __runOriginal,out Observation __state)
        {
            __state=new Observation();
            try{RebirthProductiveEarnedRemoteHost.EnterNativeToolOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.NativeToolOwner);}catch{}
            try{RebirthRecipeOutputIntegration.EnterRecipeOutputOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.RecipeOutputOwner);}catch{}
            try{RebirthConsumptionDurableDelivery.EnterInventoryOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.ConsumptionOwner);}catch{}
            try{RebirthRemoteResourcePlacedStationOriginal.EnterCancellationOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.CancellationOwner);}catch{}
            try{RebirthDogLifecycleService.EnterHireOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.HireOwner);}catch{}
            try{RebirthNpcMetabolismCustody.EnterOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.MetabolismOwner);}catch{}
            try{RebirthRecyclerPlacedSettlement.EnterRecyclerOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.RecyclerOwner);}catch{}
            try{RemoteResourceTransactions.EnterActionPaymentOwnerSave(__instance,_dir,_playerId,__runOriginal,out __state.ActionPaymentOwner);}catch{}
            // These are independent evidence consumers, not native Save
            // admission. A failed observer must not suppress the save or keep
            // later purposes from acquiring their own actual-call scope.
            try{RebirthProductiveEarnedUiDeferredTransport.OwnerPds.BeforeSave(__instance,_dir,_playerId,__runOriginal,out __state.HostPds);}catch{}
            try{RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.BeforeSave(__instance,_dir,_playerId,__runOriginal,out __state.NativeViewer);}catch{}
            try{RebirthWorkstationCompletionPipeline.BeforeCachedOwnerEffectSave(__instance,_dir,_playerId,__runOriginal,out __state.CachedOwnerEffect);}catch{}
            try{PrefixLegacy(__instance,_dir,_playerId,__runOriginal,out __state.Native);}catch{}
        }
        private static void Finish(Observation state,bool ran)
        {
            if(state==null||state.Finished)return;state.Finished=true;
            try{RebirthProductiveEarnedRemoteHost.ObserveNativeToolOwnerSave(ran,state.NativeToolOwner);}catch{}
            try{RebirthConsumptionDurableDelivery.ObserveInventoryOwnerSave(ran,state.ConsumptionOwner);}catch{}
            state.ConsumptionOwner=null;
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerSave(ran,state.RecipeOutputOwner);}catch{}
            state.RecipeOutputOwner=null;
            try{RebirthProductiveEarnedUiDeferredTransport.OwnerPds.AfterSave(ran,state.HostPds);}catch{}
            finally
            {
                try{RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.AfterSave(ran,state.NativeViewer);}catch{}
                try{RebirthWorkstationCompletionPipeline.AfterCachedOwnerEffectSave(ran,state.CachedOwnerEffect);}catch{}
                try{RebirthRemoteResourcePlacedStationOriginal.ObserveCancellationOwnerSave(ran,state.CancellationOwner);}catch{}
                finally
                {
                    try{RebirthDogLifecycleService.ObserveHireOwnerSave(ran,state.HireOwner);}catch{}
                    finally
                    {
                        try{RebirthNpcMetabolismCustody.ObserveOwnerSave(ran,state.MetabolismOwner);}catch{}
                        finally
                        {
                            try{RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerSave(ran,state.RecyclerOwner);}catch{}
                            finally
                            {
                                try{RebirthRecyclerPlacedSettlement.EndRecyclerOwnerSave(state.RecyclerOwner);}catch{}
                                finally
                                {
                                    try{RemoteResourceTransactions.ObserveActionPaymentOwnerSave(ran,state.ActionPaymentOwner);}catch{}
                                    finally{ReleaseDeferredSaveState(state.Native);state.Native=null;state.HostPds=null;state.CancellationOwner=null;state.NativeViewer=null;state.CachedOwnerEffect=null;state.HireOwner=null;state.MetabolismOwner=null;state.RecyclerOwner=null;state.ActionPaymentOwner=null;}
                                }
                            }
                        }
                    }
                }
            }
        }
        private static void Postfix(bool __runOriginal,Observation __state)
        {try{PostfixLegacy(__runOriginal,__state?.Native);}finally{Finish(__state,__runOriginal);}}
        private static Exception Finalizer(Exception __exception,Observation __state)
        {Finish(__state,false);return __exception;}
        private static void PrefixLegacy(PlayerDataFile __instance,string _dir,string _playerId,bool __runOriginal,out NativeFile __state)
        {
            __state=null;
            if(TryLocalBeforeSave(__instance,_dir,_playerId,__runOriginal,out __state))return;
            if(TryDeferredSave(__instance,_dir,_playerId,__runOriginal,out __state))return;
            try
            {
                lock(gate)
                {
                    if(!__runOriginal||!files.TryGetValue(__instance,out var native))return;
                    var expected=native.Remote==null?native.Witness?.After:native.Remote.Expected;
                    if(expected==null||(native.Remote==null?native.Witness.Closed:native.Remote.Closed))return;
                    string root=Path.GetFullPath(_dir),path=Path.GetFullPath(Path.Combine(root,_playerId+"."+PlayerDataFile.EXT));
                    if(_playerId!=(native.Remote==null?native.Witness.PlayerName:native.Remote.PlayerName)||
                        root!=(native.Remote==null?native.Witness.PlayerRoot:native.Remote.PlayerRoot)||Path.GetDirectoryName(path)!=root||!Safe(path))return;
                    var lease=(native.Remote==null?native.Witness.NativeBudget:native.Remote.NativeBudget)?.Retain();if(lease==null)return;
                    try{var image=NativeImage(__instance);if(Hash(image)!=native.NativeHash)return;
                        __state=new NativeFile{Witness=native.Witness,Remote=native.Remote,Image=image,Path=path,DeferredWorkerRetention=lease};lease=null;}
                    finally{lease?.Dispose();}
                }
            }
            catch{__state=null;}
        }
        private static void PostfixLegacy(bool __runOriginal,NativeFile __state)
        {
            if(!__runOriginal||__state==null)return;
            if(__state.LocalBefore!=null){ObserveLocalBeforeSave(__state);return;}
            if(__state.Deferred!=null){ObserveDeferredSave(__state);return;}
            lock(gate)
            {
                if(!ReadFinal(__state.Path,__state.Image))return;
                if(__state.Remote!=null){var owner=__state.Remote;if(owner.Closed)return;owner.Final=__state.Image;owner.Path=__state.Path;owner.Completed=true;}
                else{var witness=__state.Witness;if(witness.Closed)return;witness.Final=__state.Image;witness.FinalPath=__state.Path;witness.Completed=true;}
            }
        }
    }
    [HarmonyPatch(typeof(NetPackagePlayerData),nameof(NetPackagePlayerData.ProcessPackage))]
    private static class PlayerUpload
    {
        private sealed class Observation {internal NetPackagePlayerData Previous;internal object Recycler;}
        private static bool Prefix(NetPackagePlayerData __instance,World __0,bool __runOriginal,out Observation __state)
        {
            __state=new Observation{Previous=readingPlayer};
            if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(__instance,__runOriginal))return false;
            try
            {
                if(!RebirthRecyclerPlacedSettlement.CanProcessRecyclerOwnerUpload(__instance,__0,__runOriginal))return false;
                RebirthRecyclerPlacedSettlement.EnterRecyclerOwnerUpload(__instance,__0,__runOriginal,out __state.Recycler);
            }
            catch{return false;} // An incomplete purpose admission never runs the native upload.
            if(__runOriginal&&readingPlayer==null&&__instance.Sender!=null&&__instance.playerDataFile!=null&&
                __instance.ValidEntityIdForSender(__instance.playerDataFile.id))readingPlayer=__instance;
            return true;
        }
        private static void Postfix(NetPackagePlayerData __instance,World __0,bool __runOriginal,Observation __state)
        {
            try{RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerUpload(__instance,__0,__runOriginal,__state?.Recycler);}catch{}
        }
        private static Exception Finalizer(Exception __exception,Observation __state)
        {
            try{RebirthRecyclerPlacedSettlement.EndRecyclerOwnerUpload(__state?.Recycler);}catch{}
            finally{if(__state!=null){readingPlayer=__state.Previous;__state.Previous=null;__state.Recycler=null;}}
            return __exception;
        }
    }
    [HarmonyPatch(typeof(GameManager),nameof(GameManager.SavePlayerData))]
    private static class AcceptedPlayer
    {
        private sealed class Observation {internal RemoteWitness Native;internal object HostPds,NativeViewer,HireOwner,MetabolismOwner,ConsumptionOwner,ActionPaymentOwner,NativeToolOwner;}
        private static void Prefix(ClientInfo _cInfo,PlayerDataFile _playerDataFile,bool __runOriginal,out Observation __state)
        {
            __state=new Observation();
            if(__runOriginal&&ThreadManager.IsMainThread()&&readingPlayer!=null&&ReferenceEquals(readingPlayer.Sender,_cInfo)&&
                ReferenceEquals(readingPlayer.playerDataFile,_playerDataFile))
            {
                try{RebirthProductiveEarnedRemoteHost.EnterNativeToolOwner(_cInfo,_playerDataFile,__runOriginal,out __state.NativeToolOwner);}catch{}
                try{RebirthDogLifecycleService.EnterHireAcceptedOwner(_cInfo,_playerDataFile,__runOriginal,out __state.HireOwner);}catch{}
                try{RebirthNpcMetabolismCustody.EnterAcceptedOwner(_cInfo,_playerDataFile,__runOriginal,out __state.MetabolismOwner);}catch{}
                try{RebirthConsumptionDurableDelivery.EnterInventoryAcceptedOwner(_cInfo,_playerDataFile,__runOriginal,out __state.ConsumptionOwner);}catch{}
                try{RemoteResourceTransactions.EnterAcceptedActionPaymentOwner(_cInfo,_playerDataFile,__runOriginal,out __state.ActionPaymentOwner);}catch{}
                try{RebirthProductiveEarnedUiDeferredTransport.OwnerPds.ObserveAcceptedPrefix(_cInfo,_playerDataFile,__runOriginal,out __state.HostPds);}catch{}
                try{RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.AcceptedPrefix(_cInfo,_playerDataFile,__runOriginal,out __state.NativeViewer);}catch{}
            }
            try{PrefixLegacy(_cInfo,_playerDataFile,__runOriginal,out __state.Native);}catch{}
        }
        private static void Postfix(ClientInfo _cInfo,PlayerDataFile _playerDataFile,bool __runOriginal,Observation __state)
        {
            try
            {
            try{PostfixLegacy(_cInfo,_playerDataFile,__runOriginal,__state?.Native);}catch{}
            try{RebirthProductiveEarnedRemoteHost.ObserveNativeToolOwner(_cInfo,_playerDataFile,__runOriginal,__state?.NativeToolOwner);}catch{}
            try{RebirthConsumptionDurableDelivery.ObserveInventoryAcceptedOwner(_cInfo,_playerDataFile,__runOriginal,__state?.ConsumptionOwner);}catch{}
            try{RebirthProductiveEarnedUiDeferredTransport.OwnerPds.ObserveAcceptedPostfix(_cInfo,_playerDataFile,__runOriginal,__state?.HostPds);}catch{}
            try{RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.AcceptedPostfix(_cInfo,_playerDataFile,__runOriginal,__state?.NativeViewer);}catch{}
            }
            finally
            {
                try{RebirthDogLifecycleService.ObserveHireAcceptedOwner(_cInfo,_playerDataFile,__runOriginal,__state?.HireOwner);}catch{}
                finally
                {
                    try{RebirthNpcMetabolismCustody.ObserveAcceptedOwner(_cInfo,_playerDataFile,__runOriginal,__state?.MetabolismOwner);}catch{}
                    finally{try{RemoteResourceTransactions.ObserveAcceptedActionPaymentOwner(_cInfo,_playerDataFile,__runOriginal,__state?.ActionPaymentOwner);}catch{}}
                }
            }
        }
        private static void PrefixLegacy(ClientInfo _cInfo,PlayerDataFile _playerDataFile,bool __runOriginal,out RemoteWitness __state)
        {
            __state=null;if(!__runOriginal||!ThreadManager.IsMainThread()||readingPlayer==null||!ReferenceEquals(readingPlayer.Sender,_cInfo)||
                !ReferenceEquals(readingPlayer.playerDataFile,_playerDataFile))return;
            try
            {
                lock(gate)
                {
                    var owner=remote.SingleOrDefault(value=>value.Current()&&ReferenceEquals(value.Original.Sender,_cInfo)&&
                        value.Original.Actor.entityId==_playerDataFile.id&&_playerDataFile.deaths==value.Original.Frame.Deaths&&Projection(_playerDataFile,value.RecipeName).SequenceEqual(value.Expected)&&
                        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(_playerDataFile,true)))==(string)value.Original.Terminal.Delivery.Element("bagAfter")&&
                        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(RebirthGearPlayerDataInventory.ReadSlots(_playerDataFile,false)))==(string)value.Original.Terminal.Delivery.Element("beltAfter"));
                    if(owner==null)return;files.Remove(_playerDataFile);files.Add(_playerDataFile,new NativeFile{Remote=owner,NativeHash=Hash(NativeImage(_playerDataFile))});__state=owner;
                }
            }
            catch{__state=null;}
        }
        private static void PostfixLegacy(ClientInfo _cInfo,PlayerDataFile _playerDataFile,bool __runOriginal,RemoteWitness __state)
        {
            if(__runOriginal&&__state?.Current()==true&&ReferenceEquals(_cInfo.latestPlayerData,_playerDataFile)&&
                ReferenceEquals(__state.Original.Actor.QuestJournal,_playerDataFile.questJournal))__state.Accepted=true;
        }
    }
    private static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
    private sealed class Limited:MemoryStream
    {
        private readonly int maximum;
        internal Limited(int maximum=Maximum){if(maximum<1||maximum>Maximum)throw new InvalidDataException();this.maximum=maximum;}
        public override void Write(byte[] buffer,int offset,int count){if(count<0||Position>maximum-count)throw new InvalidDataException();base.Write(buffer,offset,count);}
        public override void WriteByte(byte value){if(Position>=maximum)throw new InvalidDataException();base.WriteByte(value);}
    }
}


