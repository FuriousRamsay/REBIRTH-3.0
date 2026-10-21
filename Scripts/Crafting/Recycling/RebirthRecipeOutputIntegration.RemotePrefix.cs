using System;
using System.Linq;
using System.IO;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static bool MatchesRemotePrefixFile(string path,byte[] bytes)
    {
        try
        {
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            if(bytes==null||bytes.Length<1||bytes.Length>UiOriginalMaximum||File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=bytes.Length)return false;var buffer=new byte[8192];int offset=0,read;
                while((read=stream.Read(buffer,0,buffer.Length))>0){for(int index=0;index<read;index++)if(buffer[index]!=bytes[offset+index])return false;offset+=read;}
                if(offset!=bytes.Length)return false;
            }
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);return true;
        }
        catch{return false;}
    }
    // Issued only by the actual accepted insertion observer. Serialized prefix
    // data does not construct this retained native caller capability.
    internal sealed class AdditionalOutputPrefix
    {
        private readonly UiOutputOriginal original;private readonly byte[] accepted;private readonly XElement image;
        internal readonly int Count;internal readonly string OriginalHash,AcceptedHash;
        internal Guid Operation=>original.Operation;
        private AdditionalOutputPrefix(UiOutputOriginal source,int count,byte[] receipt,XElement value)
        {original=source;Count=count;accepted=receipt;image=value;OriginalHash=UiOriginalHash(source.Original);AcceptedHash=UiOriginalHash(receipt);}
        internal bool Current()=>ThreadManager.IsMainThread()&&original.World.IsRemote()&&
            ReferenceEquals(GameManager.Instance?.World,original.World)&&original.UiContextCurrent()&&
            ReferenceEquals(original.Actor.world,original.World)&&ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor)&&
            ReferenceEquals(original.World.GetEntity(original.Actor.entityId),original.Actor)&&!original.Actor.IsDead()&&original.Actor.Died==original.Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(original.Actor)==original.Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==original.Owner&&
            MatchesRemotePrefixFile(original.Path,original.Original)&&
            MatchesRemotePrefixFile(original.Path+".output-"+(Count-1)+".accepted",accepted);
        internal bool Matches(OwningObservation observed)=>Current()&&observed!=null&&ReferenceEquals(observed.Card,original.Scope.Card)&&
            ReferenceEquals(observed.Viewer,original.Actor)&&ReferenceEquals(observed.Recipe,original.Recipe)&&observed.BeforeCount==original.Count&&
            observed.ViewerOwnerKey==original.Owner&&observed.ViewerCreation==original.Creation&&observed.ViewerDeaths==original.Deaths&&MatchesObservedOrigin(observed);
        private bool MatchesObservedOrigin(OwningObservation observed)
        {
            // Only the actual retained native full-boundary predecessor chain
            // can carry this original into a later UI generation. Equal XML,
            // card shape or a copied window version does not issue the link.
            for(int depth=0;observed!=null&&depth<=64;depth++,observed=observed.CachedPrevious)
                if(ReferenceEquals(observed.Card,original.Scope.Card)&&ReferenceEquals(observed.Viewer,original.Actor)&&
                    observed.NativeVersionBefore==original.NativeVersionBefore&&observed.BeforeCount==original.Count&&
                    observed.BagBefore.SequenceEqual(original.OwnerBeforeBag)&&observed.BeltBefore.SequenceEqual(original.OwnerBeforeBelt)&&
                    observed.EquipmentBefore.SequenceEqual(original.OwnerBeforeEquipment))return true;
            return false;
        }
        internal XElement CopyEvidence()=>Current()?new XElement(image):null;
        internal bool OwnerCurrent()=>Current()&&image.Element("bagAfter").Value==Convert.ToBase64String(OwningGrid(original.Actor.bag.ItemGrid.items))&&
            image.Element("beltAfter").Value==Convert.ToBase64String(OwningGrid(original.Actor.inventory.ItemGrid.items))&&
            image.Element("equipmentAfter").Value==Convert.ToBase64String(OwningEquipmentNative(original.Actor));
        internal static AdditionalOutputPrefix Capture(object nativeSource,int index,byte[] receipt,XElement acceptedImage)
        {
            if(nativeSource is not UiOutputOriginal source)return null;
            if(source?.Current()!=true||!source.World.IsRemote()||source.Scope.Grid!=null||index<0||index>source.Scope.Outputs.Count||
                source.OutputPrefix!=index+1||source.OutputEntered||!source.AcceptedOwnerCurrent())return null;
            var evidence=new XElement("nativeOutputPrefix",new XAttribute("version",1),new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("original",UiOriginalHash(source.Original)),new XAttribute("accepted",UiOriginalHash(receipt)),new XAttribute("count",index+1),
                new XAttribute("extrasDefinition",RebirthRecipeOutputs.DefinitionIdentity(source.Recipe)),
                new XElement("primaryNative",Convert.ToBase64String(OwningGrid(new[]{new ItemStack(source.Primary,source.Recipe.count)}))),
                new XElement("extrasNative",Convert.ToBase64String(OwningGrid(source.Scope.Outputs.ToArray()))),
                new XElement("bagAfter",Convert.ToBase64String(source.AcceptedBag)),new XElement("beltAfter",Convert.ToBase64String(source.AcceptedBelt)),
                new XElement("equipmentAfter",Convert.ToBase64String(source.AcceptedEquipment)),new XElement("acceptedInsertion",new XElement(acceptedImage)));
            return new AdditionalOutputPrefix(source,index+1,receipt,evidence);
        }
        internal static AdditionalOutputPrefix FromRemoteCold(object nativeOriginal,RemoteOutputColdClient authority,byte[] accepted,XElement image)
        {
            if(nativeOriginal is not UiOutputOriginal source||authority?.BindingCurrent()!=true||!ReferenceEquals(source.RemoteCold,authority)||
                source.Operation!=authority.LocalOperation||source.OutputPrefix!=authority.Prefix||source.OutputEntered||!source.UiContextCurrent()||
                (string)image.Attribute("original")!=UiOriginalHash(source.Original)||(int?)image.Attribute("count")!=source.OutputPrefix)return null;
            var result=new AdditionalOutputPrefix(source,source.OutputPrefix,accepted,new XElement(image));return result.Current()?result:null;
        }
        internal void ObserveSaved(RebirthWorkstationCompletionPipeline.AdditionalOutputPrefixSaved saved)
        {
            if(saved?.Current()!=true||!ReferenceEquals(saved.Prefix,this)||!Matches(saved.Observation))return;
            var request=saved.Request;var ack=saved.Acknowledgment;var frame=request.Element("nativeOwnerFrame");
            if(frame==null||frame.Element("bag")?.Value!=image.Element("bagAfter")?.Value||frame.Element("belt")?.Value!=image.Element("beltAfter")?.Value||
                frame.Element("equipment")?.Value!=image.Element("equipmentAfter")?.Value||!uiOwnerPrefixes.TryGetValue(original.Actor,out var retained))return;
            lock(uiOwnerGate)foreach(var prefix in retained.Values.ToArray())
            {
                if(prefix.Saved||!prefix.Remote||prefix.Operation!=original.Operation||prefix.Index!=Count-1||prefix.OriginalHash!=OriginalHash||prefix.AcceptedHash!=AcceptedHash||
                    !prefix.Bag.SequenceEqual(Convert.FromBase64String(frame.Element("bag").Value))||
                    !prefix.Belt.SequenceEqual(Convert.FromBase64String(frame.Element("belt").Value))||
                    !prefix.Equipment.SequenceEqual(Convert.FromBase64String(frame.Element("equipment").Value)))continue;
                var proof=new XElement("recipeOutputOwnerPrefixSaved",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("index",Count-1),
                    new XAttribute("original",OriginalHash),new XAttribute("accepted",AcceptedHash),new XAttribute("owner",original.Owner),new XAttribute("creation",original.Creation),
                    new XAttribute("deaths",original.Deaths),new XAttribute("entity",original.Actor.entityId),new XAttribute("remote",true),
                    new XAttribute("nativeOperation",saved.Observation.Operation.ToString("N")),new XAttribute("nativeGeneration",(string)request.Attribute("generation")),
                    new XAttribute("nativeNonce",(string)ack.Attribute("nativeNonce")),new XAttribute("nativeSource",(string)ack.Attribute("nativeSource")),
                    new XAttribute("nativeDigest",(string)request.Attribute("pdsDigest")),new XAttribute("nativeLength",(int)request.Attribute("pdsLength")),
                    new XAttribute("requestDigest",(string)ack.Attribute("digest")));
                if(saved.Current()&&Current())prefix.Saved=PublishUiOriginal(prefix.SourcePath+".output-"+prefix.Index+".owner-saved",UiOriginalBytes(proof));
                if(prefix.Saved)
                {
                    prefix.Dispose();
                    // The saved complete ordered prefix supersedes only our
                    // older volatile registrations. It does not manufacture
                    // separate earlier native player-file save receipts.
                    foreach(var prior in retained.Values.Where(value=>!value.Saved&&value.Remote&&value.Operation==original.Operation&&
                        value.SourcePath==original.Path&&value.OriginalHash==OriginalHash&&value.Index<Count-1&&
                        ReferenceEquals(value.Actor,original.Actor)&&ReferenceEquals(value.World,original.World)&&
                        value.Owner==original.Owner&&value.Creation==original.Creation&&value.Deaths==original.Deaths).ToArray())
                    {retained.Values.Remove(prior);prior.Dispose();}
                }
            }
        }
    }
}
