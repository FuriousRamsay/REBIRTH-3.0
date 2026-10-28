// AUTHORED_NOT_RUN. Production authority-local factory and terminal role.
// Actual local native/account Before, Add/sync/save producers, full physical
// validators and publication safety are shared explicit adapters.
internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
 static string authorityOriginalPath,authorityPublicationPath,authorityBeforePath,authorityRemotePath;
 static System.Xml.Linq.XElement AuthorityRecord()=>RebirthWorkstationCompletionPublication.Files[authorityOriginalPath];
 static System.Xml.Linq.XElement AuthorityLedger()=>RebirthWorkstationCompletionPublication.Files[authorityPublicationPath].Element("before").Element("authorityNativeContribution");
 static System.Xml.Linq.XElement AuthoritySaved()=>RebirthWorkstationCompletionPublication.Files[authorityPublicationPath+".saved"].Element("authorityNativePhysicalSaved");
 static void SeedAuthority()
 {
  Seed();PhysicalSame=RebirthProductiveEarnedUiRemote.Extras=RebirthWorkstationCompletionWitnessValidator.Valid=true;
  authorityRemotePath=path;RebirthWorkstationCompletionPublication.Files.Clear();var operation=ReadGuid(member,"operation");creator.Birth=aggregate.LoadedOwnerSource.Birth;
  var before=new System.Xml.Linq.XElement(member.Element("nativeOriginal").Elements().Single());before.SetAttributeValue("viewer","viewer");before.SetAttributeValue("creation","creation");
  member.Element("nativeOriginal").ReplaceNodes(new System.Xml.Linq.XElement(before));
  authorityOriginalPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"intents","nativeOwners",operation.ToString("N")+".xml");
  authorityPublicationPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"authorityNative",operation.ToString("N")+".xml");
  authorityBeforePath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"intents",operation.ToString("N")+".local-before");
  var seal=Envelope(operation,new System.Xml.Linq.XElement(before));seal.Add(new System.Xml.Linq.XElement("localNativeBeforeSaved",new System.Xml.Linq.XAttribute("session",1),new System.Xml.Linq.XAttribute("path","native-region"),new System.Xml.Linq.XAttribute("payload",new string('b',64)),new System.Xml.Linq.XAttribute("offset",8),new System.Xml.Linq.XAttribute("length",100),new System.Xml.Linq.XAttribute("tile",new string('c',64))));RebirthWorkstationCompletionPublication.Files[authorityBeforePath]=seal;
  var original=Envelope(operation,new System.Xml.Linq.XElement(before));original.Add(new System.Xml.Linq.XAttribute("nativeNonce",""),new System.Xml.Linq.XAttribute("nativeSource",""),new System.Xml.Linq.XElement("originalQueueOwner",new System.Xml.Linq.XAttribute("operation",creator.Operation.ToString("N")),new System.Xml.Linq.XAttribute("birth",creator.Birth.ToString("N")),new System.Xml.Linq.XAttribute("owner",creator.OriginalOwnerKey),new System.Xml.Linq.XAttribute("creation",creator.Creation),new System.Xml.Linq.XAttribute("entity",creator.StartingEntityId),new System.Xml.Linq.XElement("recipeBinding",creator.RecipeBinding)),new System.Xml.Linq.XElement("authoritySavedBefore",new System.Xml.Linq.XAttribute("path",authorityBeforePath),new System.Xml.Linq.XAttribute("digest",RebirthWorkstationCompletionPipeline.CachedFileHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(authorityBeforePath)))));RebirthWorkstationCompletionPublication.Files[authorityOriginalPath]=original;
  string hash=RebirthWorkstationCompletionOriginal.Hash(original.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
  var ledger=new System.Xml.Linq.XElement("authorityNativeContribution",new System.Xml.Linq.XAttribute("version",1),new System.Xml.Linq.XAttribute("operation",operation.ToString("N")),new System.Xml.Linq.XAttribute("nonce",System.Guid.Empty.ToString("N")),new System.Xml.Linq.XAttribute("birth",creator.Birth.ToString("N")),new System.Xml.Linq.XAttribute("source","purpose"),new System.Xml.Linq.XAttribute("original",hash),new System.Xml.Linq.XElement("members",new System.Xml.Linq.XElement(member)),new System.Xml.Linq.XElement("nativeAddArgument","AQ=="),new System.Xml.Linq.XElement("beforeComplete","Ag=="),new System.Xml.Linq.XElement("afterComplete","Aw=="));
  RebirthWorkstationCompletionPublication.Files[authorityPublicationPath]=Envelope(operation,new System.Xml.Linq.XElement(ledger));
  var physical=Envelope(operation,new System.Xml.Linq.XElement(ledger));physical.Add(new System.Xml.Linq.XElement("authorityNativePhysicalSaved",new System.Xml.Linq.XAttribute("source",hash),new System.Xml.Linq.XAttribute("custodyBirth",creator.Birth.ToString("N")),new System.Xml.Linq.XAttribute("native",new string('a',64)),new System.Xml.Linq.XAttribute("nativePath","native-region"),new System.Xml.Linq.XAttribute("payload",new string('b',64)),new System.Xml.Linq.XAttribute("offset",8),new System.Xml.Linq.XAttribute("length",100),new System.Xml.Linq.XAttribute("tile",new string('c',64)),new System.Xml.Linq.XElement("physicalDelivery",new System.Xml.Linq.XElement("nativeOwningDelivery"))));RebirthWorkstationCompletionPublication.Files[authorityPublicationPath+".saved"]=physical;
  aggregate.LoadedOwnerSource.DeferredMembership=new System.Xml.Linq.XElement("contribution",new System.Xml.Linq.XElement("members",new System.Xml.Linq.XElement(member)));
 }
 static void RunAuthority()
 {
  SeedAuthority();Need(Capture(out var value)&&value.Current());var proof=new HostTerminalPublication{Member=value};Need(value.SaveTerminal(proof)&&value.TerminalSaved);Need(RebirthWorkstationCompletionPublication.Files.ContainsKey(authorityOriginalPath+".remote-terminal.xml")&&RebirthWorkstationCompletionPublication.Files.ContainsKey(authorityPublicationPath+".loaded-owner-terminal"));int writes=RebirthWorkstationCompletionPublication.Writes;Need(value.SaveTerminal(proof)&&RebirthWorkstationCompletionPublication.Writes==writes);Need(!RebirthWorkstationCompletionPublication.Files.ContainsKey(authorityOriginalPath+".owner-terminal"));value.Dispose();
  SeedAuthority();RebirthWorkstationCompletionPublication.Files[authorityBeforePath].SetAttributeValue("changed",true);Need(!Capture(out _));
  SeedAuthority();RebirthWorkstationCompletionPublication.Files.Remove(authorityPublicationPath+".saved");Need(!Capture(out _));
  SeedAuthority();AuthoritySaved().Element("physicalDelivery").Remove();Need(!Capture(out _));
  SeedAuthority();AuthorityRecord().SetAttributeValue("nativeNonce",System.Guid.NewGuid().ToString("N"));Need(!Capture(out _));
  SeedAuthority();AuthorityLedger().SetAttributeValue("source","other");Need(!Capture(out _));
  SeedAuthority();AuthoritySaved().SetAttributeValue("source","other");Need(!Capture(out _));
  SeedAuthority();AuthorityRecord().Element("authoritySavedBefore").SetAttributeValue("digest","other");Need(!Capture(out _));
  SeedAuthority();RebirthProductiveEarnedObservationBudget.Available=false;Need(!Capture(out _));
  SeedAuthority();aggregate.LoadedOwnerSource.Native.Live=false;Need(!Capture(out _));
  SeedAuthority();aggregate.LoadedOwnerSource.Native.Identity=false;Need(!Capture(out _));
  SeedAuthority();RebirthWorkstationCompletionWitnessValidator.Valid=false;Need(!Capture(out _));
  SeedAuthority();Delta=false;Need(!Capture(out _));
  SeedAuthority();NativeArgument=false;Need(!Capture(out _));
  SeedAuthority();PhysicalSame=false;Need(!Capture(out _));
  SeedAuthority();RebirthProductiveEarnedUiRemote.Extras=false;Need(!Capture(out _));
  SeedAuthority();RebirthProductiveEarnedNativeBody.Valid=false;Need(!Capture(out _));
  SeedAuthority();creator.OriginalOwnerKey="other";Need(!Capture(out _));
  SeedAuthority();RebirthWorkstationCompletionPublication.Files[authorityRemotePath]=new System.Xml.Linq.XElement("ambiguousRemoteOriginal");Need(!Capture(out _));
  SeedAuthority();Need(Capture(out value));AuthoritySaved().SetAttributeValue("changed",true);Need(!value.Current());value.Dispose();
  SeedAuthority();Need(Capture(out value));proof=new HostTerminalPublication{Member=value};RebirthWorkstationCompletionPublication.RefusedSuffix=".loaded-owner-terminal";Need(!value.SaveTerminal(proof)&&!value.TerminalSaved);RebirthWorkstationCompletionPublication.RefusedSuffix=null;Need(value.SaveTerminal(proof)&&value.HistoricalTerminalSaved);value.Dispose();
  Need(RebirthProductiveEarnedObservationBudget.Held==0);System.Console.WriteLine("PASS "+checks+" production loaded authority-native member assertions; native/account/publication adapters excluded");
 }
}
