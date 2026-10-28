// AUTHORED_NOT_RUN. Shared native-member adapters; actual UIO codec, real
// native load/transition validation and final removal/PDS issuer are excluded.
internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
 static string uiRoot,uiPath;static System.Xml.Linq.XElement uiTerminal,uiSeal;
 static void SeedUi()
 {
  Seed();RebirthProductiveEarnedUiCoordinator.AllowWrite=true;
  var loaded=aggregate.LoadedOwnerSource;loaded.Save=uiRoot;var operation=System.Guid.NewGuid();var nonce=System.Guid.NewGuid();
  var frame=new RebirthProductiveEarnedUiOriginal.UiFrame{Operation=operation,Birth=loaded.Birth,Owner=new string('d',64)};
  var image=System.Text.Encoding.UTF8.GetBytes("independently-saved-uio-"+operation.ToString("N"));RebirthProductiveEarnedUiOriginal.Frame=frame;RebirthProductiveEarnedUiOriginal.Image=image;
  string hash=HydrationHash(image);uiPath=System.IO.Path.Combine(uiRoot,"RebirthProductiveEarnedUi","remote",frame.Owner,operation.ToString("N"),"original.uio");System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(uiPath));System.IO.File.WriteAllBytes(uiPath,image);
  member=new System.Xml.Linq.XElement("member",new System.Xml.Linq.XAttribute("kind","uio"),new System.Xml.Linq.XAttribute("operation",operation.ToString("N")),new System.Xml.Linq.XAttribute("source","purpose"),new System.Xml.Linq.XAttribute("uio",hash),new System.Xml.Linq.XAttribute("nonce",nonce.ToString("N")),new System.Xml.Linq.XAttribute("contribution",1),new System.Xml.Linq.XElement("nativeAddArgument","AQ=="),new System.Xml.Linq.XElement("beforeComplete","Ag=="),new System.Xml.Linq.XElement("afterComplete","Aw=="));
  uiTerminal=new System.Xml.Linq.XElement("uiTerminalOriginal",new System.Xml.Linq.XAttribute("operation",operation.ToString("N")),new System.Xml.Linq.XAttribute("uio",hash),new System.Xml.Linq.XElement("afterNative","AQ=="),new System.Xml.Linq.XElement("deferredContribution",new System.Xml.Linq.XElement("members",new System.Xml.Linq.XElement(member))));
  uiSeal=new System.Xml.Linq.XElement("uiNativeStationViewerSaved",new System.Xml.Linq.XAttribute("operation",operation.ToString("N")),new System.Xml.Linq.XAttribute("uio",hash),new System.Xml.Linq.XAttribute("nonce",nonce.ToString("N")),new System.Xml.Linq.XAttribute("birth",frame.Birth.ToString("N")),new System.Xml.Linq.XAttribute("native",HydrationHash(new byte[]{1})),new System.Xml.Linq.XAttribute("viewerNative",new string('a',64)),new System.Xml.Linq.XAttribute("nativePath","native-region"),new System.Xml.Linq.XAttribute("payload",new string('b',64)),new System.Xml.Linq.XAttribute("offset",8),new System.Xml.Linq.XAttribute("length",100),new System.Xml.Linq.XAttribute("tile",new string('c',64)));
  WriteUi();loaded.DeferredMembership=new System.Xml.Linq.XElement("contribution",new System.Xml.Linq.XElement("members",new System.Xml.Linq.XElement(member)));
 }
 static void WriteUi(){System.IO.File.WriteAllText(uiPath+".TerminalOriginal",uiTerminal.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));System.IO.File.WriteAllText(uiPath+".StationViewerSaved",uiSeal.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));}
 static bool CaptureUi(out LoadedUiMember value)=>LoadedUiMember.TryCapture(aggregate,member,creator,out value);
 static void RunUi()
 {
  uiRoot=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rebirth-loaded-ui-member-"+System.Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(uiRoot);
  try
  {
   SeedUi();Need(CaptureUi(out var value)&&value.Current());Need(value.Matches(member));var lease=value.Retain();value.Dispose();Need(value.Current());lease.Dispose();Need(RebirthProductiveEarnedObservationBudget.Held==0);
   SeedUi();System.IO.File.Delete(uiPath+".StationViewerSaved");Need(!CaptureUi(out _));
   SeedUi();uiSeal.SetAttributeValue("nonce",System.Guid.NewGuid().ToString("N"));WriteUi();Need(!CaptureUi(out _));
   SeedUi();aggregate.LoadedOwnerSource.Native.Live=false;Need(!CaptureUi(out _));
   SeedUi();System.IO.File.WriteAllText(uiPath,"changed binary original");Need(!CaptureUi(out _));
   SeedUi();uiTerminal.Element("afterNative").Value="Ag==";WriteUi();Need(!CaptureUi(out _));
   SeedUi();uiTerminal.Element("deferredContribution").Element("members").Element("member").SetAttributeValue("contribution",2);WriteUi();Need(!CaptureUi(out _));
   SeedUi();Delta=false;Need(!CaptureUi(out _));
   SeedUi();NativeArgument=false;Need(!CaptureUi(out _));
   SeedUi();creator.Live=false;Need(!CaptureUi(out _));
   SeedUi();aggregate.LoadedOwnerSource.Native.Identity=false;Need(!CaptureUi(out _));
   SeedUi();System.IO.File.WriteAllText(uiPath+".tmp","unresolved");Need(!CaptureUi(out _));
   SeedUi();string duplicate=uiPath.Replace(new string('d',64),new string('e',64));System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(duplicate));System.IO.File.Copy(uiPath,duplicate);Need(!CaptureUi(out _));
   SeedUi();Need(CaptureUi(out value));var proof=new HostTerminalPublication{Ui=value};Need(value.SaveTerminal(proof)&&value.TerminalSaved);int writes=RebirthWorkstationCompletionPublication.Writes;Need(value.SaveTerminal(proof)&&RebirthWorkstationCompletionPublication.Writes==writes);System.IO.File.WriteAllText(uiPath+".TerminalSaved","changed receipt");Need(!value.HistoricalTerminalSaved);value.Dispose();
   SeedUi();RebirthProductiveEarnedObservationBudget.Available=false;Need(!CaptureUi(out _));
   SeedUi();Need(CaptureUi(out value));proof=new HostTerminalPublication{Ui=value};RebirthProductiveEarnedUiCoordinator.AllowWrite=false;Need(!value.SaveTerminal(proof)&&!value.TerminalSaved);RebirthProductiveEarnedUiCoordinator.AllowWrite=true;Need(value.SaveTerminal(proof)&&value.HistoricalTerminalSaved);value.Dispose();
   Need(RebirthProductiveEarnedObservationBudget.Held==0);System.Console.WriteLine("PASS "+checks+" loaded UIO member assertions; native/codec/publication adapters excluded");
  }
  finally
  {
   if(System.IO.Path.GetDirectoryName(uiRoot)!=System.IO.Path.GetTempPath().TrimEnd(System.IO.Path.DirectorySeparatorChar)||!System.IO.Path.GetFileName(uiRoot).StartsWith("rebirth-loaded-ui-member-"))throw new System.IO.InvalidDataException();
   System.IO.Directory.Delete(uiRoot,true);
  }
 }
}
