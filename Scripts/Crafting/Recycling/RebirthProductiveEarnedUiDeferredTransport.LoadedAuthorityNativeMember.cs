using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class LoadedNativeMember
    {
        private bool authority;private string authorityPath;private XElement physical;
        private string OwnerTerminalPath=>authority?Path+".remote-terminal.xml":Path+".owner-terminal";
        private string QueryTerminalPath=>authority?authorityPath+".loaded-owner-terminal":Path+".query-terminal";
        private static string AuthorityOriginalPath(Guid operation)=>System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"intents","nativeOwners",operation.ToString("N")+".xml");
        private bool SaveAuthorityTerminal(HostTerminalPublication proof)
        {
            if(TerminalSaved)return true;
            long bound=checked(proof.GroupEvidenceBound+MeasurePublicationElement(Before)+MeasurePublicationElement(physical)+65536);
            using(var workspace=proof.EnterGroupPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement(Before)),new XElement("nativeOwnerRemoteTerminal",proof.CopyGroupEvidence(),new XElement("delivery",new XElement(physical))));
                if(!RebirthWorkstationCompletionPublication.Write(OwnerTerminalPath,record)||!Current()||!proof.Current())return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(OwnerTerminalPath);if(bytes==null)return false;
                var history=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement(Before)),new XElement("loadedAuthorityNativeOwnerTerminal",new XAttribute("original",Hash),
                        new XAttribute("physical",RebirthWorkstationCompletionPipeline.CachedFileHash(inputs[authorityPath+".saved"])),
                        new XAttribute("terminal",RebirthWorkstationCompletionPipeline.CachedFileHash(bytes)),proof.CopyGroupEvidence()));
                if(!RebirthWorkstationCompletionPublication.Write(QueryTerminalPath,history)||!Current()||!proof.Current())return false;
                var historical=RebirthWorkstationCompletionPublication.CopyPublishedBytes(QueryTerminalPath);if(historical==null)return false;
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(inputs.Sum(pair=>16L*pair.Value.Length)+8L*(bytes.Length+historical.Length)+
                    8L*(MeasurePublicationElement(Member)+MeasurePublicationElement(Before)+MeasurePublicationElement(physical))+65536)))return false;
                terminal=bytes;query=historical;return TerminalSaved;
            }
        }
        private static bool TryCaptureAuthority(HostContribution aggregate,XElement member,RebirthProductiveEarnedUiQueueOwner.Witness creator,out LoadedNativeMember result)
        {
            result=null;LoadedNativeMember value=null;IDisposable memory=null;
            try
            {
                var loaded=aggregate.LoadedMemberSource;Guid operation=ReadGuid(member,"operation");
                string originalPath=AuthorityOriginalPath(operation);
                string publicationPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"authorityNative",operation.ToString("N")+".xml");
                var files=new[]{originalPath,publicationPath,publicationPath+".saved"};
                var lengths=files.ToDictionary(path=>path,path=>RebirthWorkstationCompletionPublication.PublishedLength(path),StringComparer.Ordinal);
                if(lengths.Values.Any(length=>length<1||length>RebirthWorkstationCompletionPublication.MaximumBytes)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*lengths.Values.Sum()+32*MeasurePublicationElement(member)+65536),out memory))return false;
                value=new LoadedNativeMember{aggregate=aggregate,creator=creator,Operation=operation,Path=originalPath,authorityPath=publicationPath,
                    authority=true,Member=new XElement(member),memory=memory};memory=null;
                value.creatorRetention=creator.RetainPublishedSource();if(value.creatorRetention==null)return false;
                foreach(var pair in lengths){var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(pair.Key);if(bytes?.LongLength!=pair.Value)return false;value.inputs.Add(pair.Key,bytes);}
                var original=RebirthWorkstationCompletionPublication.ReadPrimary(originalPath);var before=original?.Element("before")?.Element("nativeOwningBefore");
                var publication=RebirthWorkstationCompletionPublication.ReadPrimary(publicationPath);var ledger=publication?.Element("before")?.Element("authorityNativeContribution");
                var saved=RebirthWorkstationCompletionPublication.ReadPrimary(publicationPath+".saved");var sealedPhysical=saved?.Element("authorityNativePhysicalSaved");
                value.WorldGuid=RebirthWorkstationCompletionOutbox.CurrentGuid(loaded.World);
                foreach(var record in new[]{original,publication,saved})
                    if((string)record?.Attribute("operation")!=operation.ToString("N")||(string)record.Attribute("world")!=value.WorldGuid)return false;
                value.Hash=RebirthWorkstationCompletionOriginal.Hash(original.ToString(SaveOptions.DisableFormatting));
                var insertion=original.Element("originalQueueOwner");
                if(before==null||(string)original.Attribute("nativeNonce")!=""||(string)original.Attribute("nativeSource")!=""||
                    (string)insertion?.Attribute("operation")!=creator.Operation.ToString("N")||(string)insertion.Attribute("birth")!=creator.Birth.ToString("N")||
                    (string)insertion.Attribute("owner")!=creator.OriginalOwnerKey||(string)insertion.Attribute("creation")!=creator.Creation||
                    (int?)insertion.Attribute("entity")!=creator.StartingEntityId||(string)insertion.Element("recipeBinding")!=creator.RecipeBinding||
                    !XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),before)||
                    !XNode.DeepEquals(saved.Element("before"),publication.Element("before"))||
                    (int?)ledger?.Attribute("version")!=1||(string)ledger.Attribute("operation")!=operation.ToString("N")||
                    (string)ledger.Attribute("nonce")!=Guid.Empty.ToString("N")||(string)ledger.Attribute("birth")!=loaded.Birth.ToString("N")||
                    (string)ledger.Attribute("source")!=(string)member.Attribute("source")||(string)ledger.Attribute("original")!=value.Hash||
                    ledger.Element("members")?.Elements("member").Count(node=>XNode.DeepEquals(node,member))!=1||
                    (string)sealedPhysical?.Attribute("source")!=value.Hash||(string)sealedPhysical.Attribute("custodyBirth")!=loaded.Birth.ToString("N")||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)sealedPhysical.Attribute("native"))||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)sealedPhysical.Attribute("payload"))||!RebirthWorkstationCompletionOriginal.ValidKey((string)sealedPhysical.Attribute("tile"))||
                    ((long?)sealedPhysical.Attribute("offset")??-1)<8||((int?)sealedPhysical.Attribute("length")??0)<1)return false;
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile((string)sealedPhysical.Attribute("nativePath"));
                var beforeReference=original.Element("authoritySavedBefore");string beforePath=(string)beforeReference?.Attribute("path");
                long beforeLength=RebirthWorkstationCompletionPublication.PublishedLength(beforePath);
                if(beforeLength<1||beforeLength>RebirthWorkstationCompletionPublication.MaximumBytes||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(value.memory,checked(32*(lengths.Values.Sum()+beforeLength)+32*MeasurePublicationElement(member)+65536)))return false;
                var beforeImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(beforePath);
                if(beforeImage?.LongLength!=beforeLength||RebirthWorkstationCompletionPipeline.CachedFileHash(beforeImage)!=(string)beforeReference.Attribute("digest"))return false;
                value.inputs.Add(beforePath,beforeImage);var beforeSeal=RebirthWorkstationCompletionPublication.ReadPrimary(beforePath);
                var savedBefore=beforeSeal.Element("localNativeBeforeSaved");
                if((string)beforeSeal.Attribute("operation")!=operation.ToString("N")||(string)beforeSeal.Attribute("world")!=value.WorldGuid||
                    !XNode.DeepEquals(beforeSeal.Element("before"),original.Element("before"))||savedBefore==null||
                    ((long?)savedBefore.Attribute("session")??0)<1||((long?)savedBefore.Attribute("offset")??-1)<8||((int?)savedBefore.Attribute("length")??0)<1||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)savedBefore.Attribute("payload"))||!RebirthWorkstationCompletionOriginal.ValidKey((string)savedBefore.Attribute("tile"))||
                    !RebirthProductiveEarnedNativeBody.TryRead(Convert.FromBase64String((string)before.Element("nativeBefore")),(int)before.Attribute("nativeElapsedOffset"),out var body,out _))return false;
                int index=(int)before.Attribute("slot");var delivery=sealedPhysical.Element("physicalDelivery")?.Element("nativeOwningDelivery");
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile((string)savedBefore.Attribute("path"));
                if(index<0||index>=body.Queue.Length||body.Queue[index]?.Recipe==null||body.Queue[index].StartingEntityId!=creator.StartingEntityId||
                    body.Queue[index].Multiplier!=(int?)before.Attribute("beforeCount")||(int?)before.Attribute("startingOwner")!=creator.StartingEntityId||
                    (string)before.Attribute("world")!=value.WorldGuid||(int?)before.Attribute("x")!=loaded.Position.x||(int?)before.Attribute("y")!=loaded.Position.y||(int?)before.Attribute("z")!=loaded.Position.z||
                    (string)before.Attribute("startingOwnerKey")!=creator.OriginalOwnerKey||(string)before.Attribute("startingOwnerCreation")!=creator.Creation||
                    (int?)before.Attribute("startingOwnerDeaths")!=creator.Deaths||delivery==null||
                    !RebirthWorkstationCompletionWitnessValidator.Matches(before,delivery,body.Queue[index].Recipe.count)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var argument)||
                    !NativeArgumentMatches(argument,body.Queue[index].Recipe,creator.StartingEntityId)||
                    !MemberDelta(member,body.Completions,body.Queue[index].Recipe,creator.StartingEntityId,argument.CraftedItemStack)||
                    !NativePhysicalSame(RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"primaryNative"),new[]{argument.CraftedItemStack})||
                    !RebirthProductiveEarnedUiRemote.ValidateExtraOutputs(body.Queue[index].Recipe,delivery,RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative"))||
                    !TryDescribeRemoteNativeAddBody(body.Queue[index].Recipe,creator.StartingEntityId,Convert.FromBase64String((string)before.Element("nativeBefore")),
                        (int)before.Attribute("nativeElapsedOffset"),ledger,out _,out _)||
                    !ReadBytes(member,"nativeAddArgument").SequenceEqual(ReadBytes(ledger,"nativeAddArgument"))||
                    !ReadBytes(member,"beforeComplete").SequenceEqual(ReadBytes(ledger,"beforeComplete"))||!ReadBytes(member,"afterComplete").SequenceEqual(ReadBytes(ledger,"afterComplete")))return false;
                value.Before=new XElement(before);value.physical=new XElement(delivery);value.Viewer=(string)before.Attribute("viewer");value.Creation=(string)before.Attribute("creation");
                if(!RebirthOriginalActionScope.Validate(value.Viewer,value.Creation)||!value.Current())return false;result=value;value=null;return true;
            }
            catch{return false;}
            finally{value?.Dispose();memory?.Dispose();}
        }
    }
}
