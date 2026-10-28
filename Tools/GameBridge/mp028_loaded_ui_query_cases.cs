// AUTHORED_NOT_RUN. Entire production historical reader, after the production
// member publishes and releases its source. Authentication/codec/PDS issuer
// remain explicit shared adapters, not remote topology evidence.
internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
 static EntityPlayer queryActor;static ClientInfo queryPeer;static TileEntityWorkstation queryStation;
 static System.Guid queryOperation,queryNonce;static string queryHash,queryProofPath,queryTerminalPath;
 static System.Xml.Linq.XElement QuerySaved()=>RebirthWorkstationCompletionPublication.Files[queryProofPath].Element("before").Element("loadedUiOwnerTerminal");
 static void SeedUiQuery()
 {
  SeedUi();ThreadManager.Main=true;RebirthProductiveEarnedArtifact.Authority=true;GameIO.Save=uiRoot;
  queryActor=new EntityPlayer{world=aggregate.LoadedOwnerSource.World};queryPeer=new ClientInfo();queryStation=new TileEntityWorkstation();
  queryOperation=ReadGuid(member,"operation");queryNonce=ReadGuid(member,"nonce");queryHash=(string)member.Attribute("uio");
  RebirthLogisticsEndpointIdentity.Birth=aggregate.LoadedOwnerSource.Birth.ToString("N");
  queryProofPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",queryOperation.ToString("N")+".loaded-uio-owner.xml");
  queryTerminalPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",aggregate.Operation.ToString("N")+".terminal.xml");
  var membership=new System.Xml.Linq.XElement("membership",new System.Xml.Linq.XElement("deferredNativeContribution",new System.Xml.Linq.XElement("members",new System.Xml.Linq.XElement(member))));
  var terminal=new System.Xml.Linq.XElement("uiDeferredNativeOwnerSaved",new System.Xml.Linq.XAttribute("owner","recipient"),new System.Xml.Linq.XAttribute("creation","recipient-creation"),new System.Xml.Linq.XAttribute("beforePlayerFile","before-file"),new System.Xml.Linq.XAttribute("beforePlayerDigest","before-digest"),new System.Xml.Linq.XAttribute("afterPlayerFile","after-file"),new System.Xml.Linq.XAttribute("afterPlayerDigest","after-digest"),new System.Xml.Linq.XElement(membership));
  RebirthWorkstationCompletionPublication.Files[queryTerminalPath]=Envelope(aggregate.Operation,terminal);
  var evidence=new System.Xml.Linq.XElement("nativeOwnerGroup",terminal.Attributes().Select(attribute=>new System.Xml.Linq.XAttribute(attribute)),new System.Xml.Linq.XAttribute("group",System.Guid.NewGuid().ToString("N")),new System.Xml.Linq.XAttribute("terminalPath",queryTerminalPath),new System.Xml.Linq.XAttribute("terminalDigest",HydrationHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(queryTerminalPath))),new System.Xml.Linq.XAttribute("nativeAfter",new string('a',64)),new System.Xml.Linq.XElement(membership));
  evidence.SetAttributeValue("recipient",(string)evidence.Attribute("owner"));evidence.Attribute("owner").Remove();
  if(!CaptureUi(out var source))throw new System.Exception("Historical query setup cannot capture original source");
  try{if(!source.SaveTerminal(new HostTerminalPublication{Ui=source,Evidence=evidence}))throw new System.Exception("Historical query setup cannot publish terminal");}finally{source.Dispose();}
  aggregate.LoadedOwnerSource.Live=false;
 }
 static bool QueryUi(out RebirthProductiveEarnedWire.Reply reply)=>RebirthProductiveEarnedUiRemote.QueryFixture(queryActor,queryPeer,queryStation,"creation",queryOperation,queryNonce,queryHash,out reply);
 static void RunUiQuery()
 {
  uiRoot=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rebirth-loaded-ui-query-"+System.Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(uiRoot);
  try
  {
   SeedUiQuery();Need(QueryUi(out var reply));Need(reply.State==RebirthProductiveEarnedWire.Status.UiTerminalSaved&&reply.Operation==queryOperation&&reply.Nonce==queryNonce&&reply.Digest==queryHash&&reply.QueueOwner=="purpose");Need(!aggregate.LoadedOwnerSource.Live&&RebirthProductiveEarnedObservationBudget.Held==0);
   int writes=RebirthWorkstationCompletionPublication.Writes;Need(QueryUi(out _)&&RebirthWorkstationCompletionPublication.Writes==writes);
   SeedUiQuery();queryActor.Died++;Need(!QueryUi(out _));
   SeedUiQuery();queryPeer.Owner=new string('e',64);Need(!QueryUi(out _));
   SeedUiQuery();queryNonce=System.Guid.NewGuid();Need(!QueryUi(out _));
   SeedUiQuery();queryHash=new string('e',64);Need(!QueryUi(out _));
   SeedUiQuery();RebirthProductiveEarnedArtifact.Authority=false;Need(!QueryUi(out _));
   SeedUiQuery();RebirthLogisticsEndpointIdentity.Birth=System.Guid.NewGuid().ToString("N");Need(!QueryUi(out _));
   SeedUiQuery();QuerySaved().Element("member").SetAttributeValue("nonce",System.Guid.NewGuid().ToString("N"));Need(!QueryUi(out _));
   SeedUiQuery();QuerySaved().Element("nativeOwnerGroup").SetAttributeValue("terminalDigest",new string('0',64));Need(!QueryUi(out _));
   SeedUiQuery();RebirthWorkstationCompletionPublication.Files[queryTerminalPath].SetAttributeValue("changed",true);Need(!QueryUi(out _));
   SeedUiQuery();var receipt=System.Xml.Linq.XElement.Load(uiPath+".TerminalSaved");receipt.SetAttributeValue("unknown",true);System.IO.File.WriteAllText(uiPath+".TerminalSaved",receipt.ToString());Need(!QueryUi(out _));
   SeedUiQuery();System.IO.File.WriteAllText(uiPath+".tmp","unresolved");Need(!QueryUi(out _));
   SeedUiQuery();RebirthProductiveEarnedObservationBudget.Available=false;Need(!QueryUi(out _));
   SeedUiQuery();ThreadManager.Main=false;Need(!QueryUi(out _));
   SeedUiQuery();QuerySaved().Element("nativeOwnerGroup").SetAttributeValue("recipient","other-recipient");Need(!QueryUi(out _));
   SeedUiQuery();QuerySaved().Element("nativeOwnerGroup").Element("membership").Elements().Single().Element("members").RemoveNodes();Need(!QueryUi(out _));
   Need(RebirthProductiveEarnedObservationBudget.Held==0);System.Console.WriteLine("PASS "+checks+" production loaded UIO historical query assertions; native/authentication adapters excluded");
  }
  finally
  {
   if(System.IO.Path.GetDirectoryName(uiRoot)!=System.IO.Path.GetTempPath().TrimEnd(System.IO.Path.DirectorySeparatorChar)||!System.IO.Path.GetFileName(uiRoot).StartsWith("rebirth-loaded-ui-query-"))throw new System.IO.InvalidDataException();
   System.IO.Directory.Delete(uiRoot,true);
  }
 }
}
