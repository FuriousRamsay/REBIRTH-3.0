using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class NativeHostOriginal
    {
        private RemoteOutputHostSource remoteOutputRestart;
        internal bool HasRemoteOutputFinalQueuePrefix=>remoteOutputRestart?.FinalCallbackSaved==true&&remoteOutputRestart.Current()&&remoteOutputRestart.Native?.IdentityCurrent()==true;
        internal byte[] RemoteOutputFinalQueuePrior=>HasRemoteOutputFinalQueuePrefix?remoteOutputRestart.FinalPriorBody:null;
        internal int RemoteOutputFinalQueuePriorOffset=>remoteOutputRestart?.Offset??BeforeOffset;
        internal bool IsRemoteOutputRestart(RemoteOutputHostSource source)=>ReferenceEquals(remoteOutputRestart,source);
        private bool RestartNativeIdentityCurrent()=>remoteOutputRestart==null||remoteOutputRestart.Current()&&remoteOutputRestart.Native?.IdentityCurrent()==true;
        private void ReleaseRemoteOutputRestart(){var source=remoteOutputRestart;remoteOutputRestart=null;if(source!=null){
            if(RemoteOwnerTerminalHistoricalSaved)remoteOutputHostHolds.RemoveAll(value=>ReferenceEquals(value.World,World)&&value.Operation==Operation);
            remoteOutputHostSources.Remove(source);source.Dispose();}}
        internal static NativeHostOriginal FromRemoteOutputRestart(RemoteOutputHostSource source,EntityPlayer viewer,ClientInfo peer)
        {
            RebirthProductiveEarnedObservationBudget memory=null;IDisposable ownerRetention=null,bodyRetention=null;NativeHostOriginal value=null;
            try
            {
                var station=source?.Native?.Tile;
                if(source?.Current()!=true||source.Admitted||station==null||source.Native?.Current()!=true||viewer==null||peer==null||
                    !ReferenceEquals(viewer.world,source.World)||viewer.IsDead()||viewer.Died!=source.Deaths||
                    RebirthCamperCharacterAccess.CurrentCreation(viewer)!=source.Creation||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.StorageKey!=source.Viewer||
                    !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)||
                    !RebirthProductiveEarnedArtifact.RemoteAuthority(viewer,peer,station,source.Creation)||
                    peer.latestPlayerData==null||!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(peer.latestPlayerData,source.Request.Element("nativeOwnerFrame")))return null;
                int index=(int)source.Before.Attribute("slot");
                RebirthProductiveEarnedUiQueueOwner.Witness owner;
                if(station.Queue==null||index<0||index>=station.Queue.Length||
                    !(source.DeferredAddBody!=null?RebirthProductiveEarnedUiQueueOwner.TryUseRemoteOutputDeferred(source,out owner):
                        source.FinalCallbackSaved?RebirthProductiveEarnedUiQueueOwner.TryUseRemoteOutputFinal(source,out owner):
                        RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,index,station.Queue[index],out owner))||
                    !owner.SourcePublished()||owner.Birth!=source.Birth||
                    owner.Deaths!=(source.DeferredAddBody!=null?(int)source.RootBefore.Attribute("startingOwnerDeaths"):source.Deaths)||
                    owner.OriginalOwnerKey!=(source.DeferredAddBody!=null?(string)source.RootBefore.Attribute("startingOwnerKey"):source.Viewer)||
                    owner.Creation!=(source.DeferredAddBody!=null?(string)source.RootBefore.Attribute("startingOwnerCreation"):source.Creation)||
                    owner.StartingEntityId!=(source.FinalCallbackSaved||source.DeferredAddBody!=null?(int)source.RootBefore.Attribute("startingOwner"):viewer.entityId)||
                    RebirthProductiveEarnedUiQueueOwner.Export(owner)!=(string)source.Original.Element("before").Element("remoteNativeOriginal").Attribute("ownerPurpose"))return null;
                var saved=RemoteOutputXml(source.Path+".saved");var beforeSaved=saved?.Element("before")?.Element("remoteNativeBeforeSaved");
                if((string)saved?.Attribute("operation")!=source.Operation.ToString("N")||(string)beforeSaved?.Attribute("source")!=source.Hash||
                    (string)beforeSaved.Attribute("nonce")!=source.Nonce.ToString("N"))return null;
                memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(source.World,owner.CurrentCustodyBirth,source.Operation,source.Image);if(memory==null)return null;
                var immutableBefore=source.RootBefore??source.Before;
                byte[] body=Convert.FromBase64String(immutableBefore.Element("nativeBefore").Value);int immutableOffset=(int)immutableBefore.Attribute("nativeElapsedOffset");
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*body.Length+65536),out bodyRetention)||
                    (ownerRetention=owner.RetainPublishedSource())==null||!source.Native.SealIdentity())return null;
                value=new NativeHostOriginal(viewer,peer,station,owner,source.Operation,source.Nonce,source.Viewer,source.Creation,new XElement(immutableBefore),source.Image,source.Path,
                    body,immutableOffset,memory,ownerRetention,bodyRetention){remoteOutputRestart=source};
                memory=null;ownerRetention=bodyRetention=null;
                value.savedBeforeImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.Path+".saved");
                if(value.savedBeforeImage==null||!value.CurrentViewer()){value.remoteOutputRestart=null;value.Dispose();return null;}
                value.BeforeSaved=true;
                if(source.FinalContinuationPath!=null)
                    value.nativeContinuations.Add(new NativeContinuation{Source=value,Before=new XElement(source.Before),Native=(byte[])source.FinalPriorBody.Clone(),Offset=source.Offset,
                        Path=source.FinalContinuationPath,Record=new XElement(source.FinalContinuationRecord),SavedRecord=new XElement(source.FinalContinuationSavedRecord),
                        Image=source.FinalContinuationImage,SavedImage=source.FinalContinuationSavedImage,Saved=true});
                if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.RestoreRemoteOutputPrefix(value,source))
                {value.remoteOutputRestart=null;value.Dispose();return null;}
                source.Admitted=true;nativeHostOriginals.Add(source.Operation,value);return value;
            }
            catch{if(value!=null){value.remoteOutputRestart=null;value.Dispose();}return null;}
            finally{memory?.Dispose();ownerRetention?.Dispose();bodyRetention?.Dispose();}
        }
    }
}
