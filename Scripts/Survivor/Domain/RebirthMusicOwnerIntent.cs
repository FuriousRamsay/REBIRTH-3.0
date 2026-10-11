using System;
using System.Xml.Linq;
// Immutable data only: no reservation, persistence, native item decoding or authority.
public sealed class RebirthMusicOwnerIntent
{
 public const int Version=1;
 public string OwnerStorageKey {get;}
 public Guid SavedWorldId {get;}
 public Guid TransactionId {get;}
 public Guid GenerationId {get;}
 public string CreationId {get;}
 public int Operation {get;}
 public bool IsAudiobook {get;}
 public int LibraryIndex {get;}
 public long ExpectedRevision {get;}
 public string ItemId {get;}
 public string ItemData {get;}
 public bool SourceIsBag {get;}
 public int SourceIndex {get;}
 public string Digest {get;}
 private readonly RebirthGearInventorySnapshot original;
 public RebirthGearInventorySnapshot CopyOriginalInventory()=>RebirthMusicOwnerDataCodec.Copy(original);
 private RebirthMusicOwnerIntent(string owner,Guid world,Guid transaction,Guid generation,string creation,int operation,bool audio,int index,long revision,string itemId,string itemData,bool bag,int source,RebirthGearInventorySnapshot inventory)
 {
  OwnerStorageKey=owner;SavedWorldId=world;TransactionId=transaction;GenerationId=generation;CreationId=creation;Operation=operation;IsAudiobook=audio;LibraryIndex=index;ExpectedRevision=revision;ItemId=itemId;ItemData=itemData;SourceIsBag=bag;SourceIndex=source;original=inventory;Digest=RebirthMusicOwnerDataCodec.Hash(Core());
 }
 public static bool TryCreate(string owner,Guid world,Guid transaction,Guid generation,string creation,int operation,bool audiobook,int libraryIndex,long revision,string itemId,string itemData,bool sourceIsBag,int sourceIndex,RebirthGearInventorySnapshot before,out RebirthMusicOwnerIntent intent)
 {
  intent=null;
  try
  {
   if(string.IsNullOrWhiteSpace(owner)||owner.Length>256||world==Guid.Empty||transaction==Guid.Empty||generation==Guid.Empty||
      !RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||(operation!=1&&operation!=2)||libraryIndex<0||libraryIndex>=(audiobook?RebirthAudiobookLibraryPersistence.Capacity:RebirthMusicLibraryService.Capacity)||revision<0||string.IsNullOrWhiteSpace(itemId)||itemId.Length>256||
      !RebirthMusicOwnerDataCodec.Data(itemData,operation==2)||!RebirthMusicOwnerDataCodec.Copy(before,out var copy))return false;
   if(operation==1)
   {if(!copy.IsUsableSource(sourceIsBag,sourceIndex))return false;var cell=(sourceIsBag?copy.Bag:copy.Belt)[sourceIndex];if(cell.Count<1||cell.ItemData!=itemData)return false;}
   else if(!sourceIsBag||sourceIndex!=0)return false; // no physical source for a return
   var candidate=new RebirthMusicOwnerIntent(owner,world,transaction,generation,normalized,operation,audiobook,libraryIndex,revision,itemId,itemData,sourceIsBag,sourceIndex,copy);
   if(!candidate.TryEncode(out var payload))return false;intent=candidate;return true;
  }
  catch{return false;}
 }
 private XElement Core()=>new XElement("musicOwnerIntent",new XAttribute("version",Version),new XAttribute("owner",OwnerStorageKey),new XAttribute("world",SavedWorldId.ToString("N")),new XAttribute("transaction",TransactionId.ToString("N")),new XAttribute("generation",GenerationId.ToString("N")),new XAttribute("creation",CreationId),new XAttribute("operation",Operation),new XAttribute("isAudiobook",IsAudiobook),new XAttribute("libraryIndex",LibraryIndex),new XAttribute("revision",ExpectedRevision),new XAttribute("itemId",ItemId),new XAttribute("itemData",ItemData),new XAttribute("sourceIsBag",SourceIsBag),new XAttribute("sourceIndex",SourceIndex),RebirthMusicOwnerDataCodec.Image("before",original));
 public XElement Write(){var node=Core();node.Add(new XAttribute("digest",Digest));return node;}
 public bool TryEncode(out byte[] payload)=>RebirthMusicOwnerDataCodec.Encode(Write(),out payload);
 public static bool TryRead(XElement node,out RebirthMusicOwnerIntent intent)
 {
  intent=null;
  try
  {
   if(!RebirthMusicOwnerDataCodec.Encode(node,out var bytes)||!RebirthMusicOwnerDataCodec.Shape(node,"musicOwnerIntent",new[]{"version","owner","world","transaction","generation","creation","operation","isAudiobook","libraryIndex","revision","itemId","itemData","sourceIsBag","sourceIndex","digest"},new[]{"before"})||
      (string)node.Attribute("version")!="1"||!RebirthMusicOwnerDataCodec.GuidField(node,"world",out var world)||!RebirthMusicOwnerDataCodec.GuidField(node,"transaction",out var transaction)||!RebirthMusicOwnerDataCodec.GuidField(node,"generation",out var generation)||
      !RebirthMusicOwnerDataCodec.Integer(node,"operation",out var operation)||!bool.TryParse((string)node.Attribute("isAudiobook"),out var audio)||!RebirthMusicOwnerDataCodec.Integer(node,"libraryIndex",out var index)||!RebirthMusicOwnerDataCodec.Long(node,"revision",out var revision)||!bool.TryParse((string)node.Attribute("sourceIsBag"),out var bag)||!RebirthMusicOwnerDataCodec.Integer(node,"sourceIndex",out var source)||!RebirthMusicOwnerDataCodec.ReadImage(node.Element("before"),"before",out var before))return false;
   if(!TryCreate((string)node.Attribute("owner"),world,transaction,generation,(string)node.Attribute("creation"),operation,audio,index,revision,(string)node.Attribute("itemId"),(string)node.Attribute("itemData"),bag,source,before,out var candidate)||candidate.Digest!=(string)node.Attribute("digest")||!RebirthMusicOwnerDataCodec.Canonical(candidate.Write(),node))return false;
   intent=candidate;return true;
  }
  catch{return false;}
 }
 public static bool TryRead(byte[] payload,out RebirthMusicOwnerIntent intent)
 {intent=null;if(!RebirthMusicOwnerDataCodec.Decode(payload,out var node)||!TryRead(node,out var candidate)||!RebirthMusicOwnerDataCodec.Canonical(candidate.Write(),payload))return false;intent=candidate;return true;}
}