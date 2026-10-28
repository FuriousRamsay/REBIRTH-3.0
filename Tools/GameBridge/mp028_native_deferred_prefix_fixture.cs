// AUTHORED_NOT_RUN. Entire actual host deferred-prefix qualifier.
// Native original/current, actual accepted Add membership, argument decode,
// rich grid and rich stack equality are explicit adapters.
using System;using System.Linq;using System.Collections.Generic;using System.Xml.Linq;
class ItemStack{internal string Image="primary";}
class Recipe{}
class CraftCompleteData{internal ItemStack CraftedItemStack=new ItemStack();}
static class RebirthWorkstationCompletionPipeline{
 internal class Owner{internal int StartingEntityId=7;}
 internal class NativeHostOriginal{internal bool Live=true,Admitted=true;internal Guid Operation=Guid.NewGuid();internal Recipe Recipe=new Recipe();internal Owner EarlierOwner=new Owner();internal bool CurrentViewer()=>Live;internal bool AdmissionCurrent()=>Admitted;}}
static class RebirthRemoteResourcePlacedStationNativeReader{internal static bool Decode=true;internal static bool TryReadCompletionArgument(byte[] bytes,out CraftCompleteData argument){argument=new CraftCompleteData();return Decode;}}
static class RebirthProductiveEarnedUiRemote{internal static ItemStack[] ReadGrid(XElement prefix,string name)=>new[]{new ItemStack{Image=prefix.Element(name).Value}};}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.Image==b.Image;}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 class HostContribution{internal RebirthWorkstationCompletionPipeline.NativeHostOriginal NativeOriginal;internal int NativePhase=1;internal bool Live=true;internal XElement NativeMembership;internal bool SourceCurrentWithoutViewer()=>Live;}
 static readonly Dictionary<Guid,HostContribution> host=new Dictionary<Guid,HostContribution>();
 static byte[] ReadBytes(XElement node,string name)=>Convert.FromBase64String(node.Element(name).Value);
 static bool Argument=true;static bool NativeArgumentMatches(CraftCompleteData a,Recipe recipe,int owner)=>Argument;
 // ACTUAL_QUALIFIER
 static int checks;static RebirthWorkstationCompletionPipeline.NativeHostOriginal original;static XElement contribution,prefix;static HostContribution entry;
 static void Fresh(){host.Clear();Argument=RebirthRemoteResourcePlacedStationNativeReader.Decode=true;original=new RebirthWorkstationCompletionPipeline.NativeHostOriginal();contribution=new XElement("deferredNativeContribution",new XAttribute("version",1),new XElement("nativeAddArgument","AQ=="));prefix=new XElement("nativeOutputPrefix",new XElement("primaryNative","primary"));entry=new HostContribution{NativeOriginal=original,NativeMembership=new XElement(contribution)};host.Add(original.Operation,entry);}
 static bool Valid()=>HasActualDeferredOutputPrefix(original,contribution,prefix);static void Need(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
 public static void Main(){Fresh();Need(Valid(),"actual accepted native Add refused");Fresh();host.Clear();Need(!Valid(),"parsed contribution created Add authority");Fresh();entry.NativeOriginal=new RebirthWorkstationCompletionPipeline.NativeHostOriginal();Need(!Valid(),"different original adopted");Fresh();entry.NativePhase=0;Need(!Valid(),"unentered native Add admitted");Fresh();entry.NativePhase=2;Need(!Valid(),"later native body relabeled as receipt phase");Fresh();entry.Live=false;Need(!Valid(),"stale accepted source admitted");Fresh();contribution.SetAttributeValue("foreign",true);Need(!Valid(),"foreign membership admitted");Fresh();contribution.SetAttributeValue("phase",1);Need(!Valid(),"physical envelope used as original membership");Fresh();prefix.Element("primaryNative").Value="foreign";Need(!Valid(),"different generated primary adopted");Fresh();Argument=false;Need(!Valid(),"different native argument admitted");Fresh();original.Admitted=false;Need(!Valid(),"missing original admission accepted");Fresh();RebirthRemoteResourcePlacedStationNativeReader.Decode=false;Need(!Valid(),"malformed native Add argument accepted");Console.WriteLine("PASS "+checks+" actual deferred native prefix assertions; explicit native adapters excluded");}
}
