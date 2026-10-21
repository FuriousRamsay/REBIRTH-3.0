using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed class ClientTerminalPublication
    {
        internal readonly ClientHydration Source;
        private readonly string path;private readonly XElement image;
        private ClientTerminalPublication(ClientHydration source,string file,XElement receipt){Source=source;path=file;image=receipt;}
        internal World World=>Source.World;
        internal TileEntityWorkstation Station=>Source.Station;
        internal EntityPlayerLocal Actor=>Source.Actor;
        internal INetConnection Connection=>Source.Connection;
        internal XUiC_WorkstationWindowGroup Window=>Source.Window;
        internal long WindowSession=>Source.WindowVersion;
        internal Guid Operation=>Source.Group;
        internal byte[] Body=>Source.NativeAfter;
        internal int Offset=>Source.NativeAfterOffset;
        internal string Path=>path;
        internal XElement Image=>new XElement(image);
        internal long MeasureImage()=>MeasurePublicationElement(image);
        internal bool CurrentSavedSource()
        {
            try
            {
                var receipt=image.Element("before")?.Element("recipientNativeTerminal");
                return Source.CurrentSource()&&Source.Consumed&&!Source.Unknown&&Source.OwnerStatus==5&&receipt!=null&&
                    Source.Members.All(value=>value.Witness?.ObservedSavedEvent()==true&&value.Witness.Final!=null&&
                        value.Witness.Final.LongLength==(long?)receipt.Attribute("ownerNativeLength")&&
                        HydrationHash(value.Witness.Final)==(string)receipt.Attribute("ownerNative"))&&
                    XNode.DeepEquals(image,RebirthWorkstationCompletionPublication.ReadPrimary(path));
            }
            catch{return false;}
        }
        internal bool Current()
        {
            try
            {
                if(!Source.CurrentSource()||!Source.Consumed||Source.Unknown||Source.OwnerStatus!=5||
                    Source.NativeAfter==null||Source.Members.Any(value=>value.Witness?.Saved()!=true)||
                    !XNode.DeepEquals(image,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Body,Offset);
            }
            catch{return false;}
        }
        internal static bool TryIssue(ClientHydration original,out ClientTerminalPublication token)
        {
            token=null;
            if(original?.OwnerStatus!=5||!original.Consumed||original.Unknown||!original.CurrentSource()||
                original.Members.Any(value=>value.Witness?.Saved()!=true))return false;
            var ownerNative=original.Members.FirstOrDefault()?.Witness?.Final;
            if(ownerNative==null||original.Members.Any(value=>!value.Witness.Final.SequenceEqual(ownerNative)))return false;
            var image=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),
                new XElement("before",new XElement("recipientNativeTerminal",new XAttribute("version",1),
                    new XAttribute("beforeDigest",original.BeforeDigest),new XAttribute("afterDigest",original.AfterDigest),
                    new XAttribute("ownerNative",HydrationHash(ownerNative)),new XAttribute("ownerNativeLength",ownerNative.LongLength),
                    new XAttribute("nativeAfter",HydrationHash(original.NativeAfter)),new XAttribute("offset",original.NativeAfterOffset),
                    new XElement("membership",original.Header.Elements("aggregate").Select(value=>new XElement(value))))));
            string path=original.Path+".terminal.xml";
            if(!RebirthWorkstationCompletionPublication.Write(path,image))return false;
            var result=new ClientTerminalPublication(original,path,image);if(!result.Current())return false;token=result;return true;
        }
    }
}
