using System;
using System.Xml.Linq;
// Complete detached one-cassette data. Declared native compatibility/limit are
// DATA ONLY: original runtime must independently decode/qualify before authority.
public sealed class RebirthMusicOwnerWitness
{
 public const int Version=1;
 public RebirthMusicOwnerIntent Intent {get;}
 public bool DestinationIsBag {get;}
 public int DestinationIndex {get;}
 public bool DeclaredDestinationCompatible {get;}
 public int DeclaredStackLimit {get;}
 public string Digest {get;}
 private readonly string offerXml;
 private readonly RebirthGearInventorySnapshot after;
 public RebirthMusicTransferState CopyOffer(){RebirthMusicTransferState.TryRead(XElement.Parse(offerXml),out var value);return value;}
 public RebirthGearInventorySnapshot CopyBeforeInventory()=>Intent.CopyOriginalInventory();
 public RebirthGearInventorySnapshot CopyAfterInventory()=>RebirthMusicOwnerDataCodec.Copy(after);
 private RebirthMusicOwnerWitness(RebirthMusicOwnerIntent intent,RebirthMusicTransferState offer,bool bag,int index,bool compatible,int limit,RebirthGearInventorySnapshot applied)
 {Intent=intent;offerXml=offer.ToXml().ToString(SaveOptions.DisableFormatting);DestinationIsBag=bag;DestinationIndex=index;DeclaredDestinationCompatible=compatible;DeclaredStackLimit=limit;after=applied;Digest=RebirthMusicOwnerDataCodec.Hash(Core());}
 public static bool TryCreate(RebirthMusicOwnerIntent intent,RebirthMusicTransferState offer,bool destinationIsBag,int destinationIndex,bool declaredCompatible,int declaredStackLimit,out RebirthMusicOwnerWitness witness)
 {
  witness=null;
  try
  {
   if(intent==null||offer==null||!RebirthMusicOwnerIntent.TryRead(intent.Write(),out var bound)||!RebirthMusicTransferState.TryRead(offer.ToXml(),out var validated)||validated==null||
      validated.TransactionId!=bound.TransactionId.ToString("N")||validated.CreationId!=bound.CreationId||validated.Operation!=bound.Operation||validated.IsAudiobook!=bound.IsAudiobook||validated.LibraryIndex!=bound.LibraryIndex||validated.ExpectedRevision!=bound.ExpectedRevision||validated.ItemId!=bound.ItemId||
      !RebirthMusicOwnerDataCodec.Data(validated.ItemData,false)||bound.ItemData.Length>0&&bound.ItemData!=validated.ItemData)return false;
   var applied=bound.CopyOriginalInventory();
   if(validated.Operation==1)
   {
    if(destinationIsBag!=bound.SourceIsBag||destinationIndex!=bound.SourceIndex||validated.SourceIsBag!=bound.SourceIsBag||validated.SourceIndex!=bound.SourceIndex||declaredCompatible||declaredStackLimit!=0||!applied.IsUsableSource(destinationIsBag,destinationIndex))return false;
    var cell=(destinationIsBag?applied.Bag:applied.Belt)[destinationIndex];if(cell.Count<1||cell.ItemData!=validated.ItemData)return false;cell.Count--;if(cell.Count==0)cell.ItemData=string.Empty;
   }
   else
   {
    if(!destinationIsBag||destinationIndex<0||destinationIndex>=applied.Bag.Length||!declaredCompatible||declaredStackLimit<1)return false;
    var cell=applied.Bag[destinationIndex];if(cell.Count==int.MaxValue||cell.Count>=declaredStackLimit||cell.Count>0&&cell.ItemData!=validated.ItemData)return false;cell.ItemData=validated.ItemData;cell.Count++;
   }
   var candidate=new RebirthMusicOwnerWitness(bound,validated,destinationIsBag,destinationIndex,declaredCompatible,declaredStackLimit,applied);
   if(!candidate.TryEncode(out var payload))return false;witness=candidate;return true;
  }
  catch{return false;}
 }
 private XElement Core()=>new XElement("musicOwnerWitness",new XAttribute("version",Version),new XAttribute("destinationIsBag",DestinationIsBag),new XAttribute("destinationIndex",DestinationIndex),new XAttribute("declaredCompatible",DeclaredDestinationCompatible),new XAttribute("declaredStackLimit",DeclaredStackLimit),Intent.Write(),XElement.Parse(offerXml),RebirthMusicOwnerDataCodec.Image("after",after));
 public XElement Write(){var node=Core();node.Add(new XAttribute("digest",Digest));return node;}
 public bool TryEncode(out byte[] payload)=>RebirthMusicOwnerDataCodec.Encode(Write(),out payload);
 public static bool TryRead(XElement node,out RebirthMusicOwnerWitness witness)
 {
  witness=null;
  try
  {
   if(!RebirthMusicOwnerDataCodec.Encode(node,out var bytes)||!RebirthMusicOwnerDataCodec.Shape(node,"musicOwnerWitness",new[]{"version","destinationIsBag","destinationIndex","declaredCompatible","declaredStackLimit","digest"},new[]{"musicOwnerIntent","pendingTransfer","after"})||(string)node.Attribute("version")!="1"||
      !bool.TryParse((string)node.Attribute("destinationIsBag"),out var bag)||!RebirthMusicOwnerDataCodec.Integer(node,"destinationIndex",out var index)||!bool.TryParse((string)node.Attribute("declaredCompatible"),out var compatible)||!RebirthMusicOwnerDataCodec.Integer(node,"declaredStackLimit",out var limit)||!RebirthMusicOwnerIntent.TryRead(node.Element("musicOwnerIntent"),out var intent)||!RebirthMusicTransferState.TryRead(node.Element("pendingTransfer"),out var offer)||offer==null||!RebirthMusicOwnerDataCodec.Canonical(offer.ToXml(),node.Element("pendingTransfer"))||!RebirthMusicOwnerDataCodec.ReadImage(node.Element("after"),"after",out var applied))return false;
   if(!TryCreate(intent,offer,bag,index,compatible,limit,out var candidate)||!RebirthMusicOwnerDataCodec.Equal(candidate.after,applied)||candidate.Digest!=(string)node.Attribute("digest")||!RebirthMusicOwnerDataCodec.Canonical(candidate.Write(),node))return false;
   witness=candidate;return true;
  }
  catch{return false;}
 }
 public static bool TryRead(byte[] payload,out RebirthMusicOwnerWitness witness)
 {witness=null;if(!RebirthMusicOwnerDataCodec.Decode(payload,out var node)||!TryRead(node,out var candidate)||!RebirthMusicOwnerDataCodec.Canonical(candidate.Write(),payload))return false;witness=candidate;return true;}
}