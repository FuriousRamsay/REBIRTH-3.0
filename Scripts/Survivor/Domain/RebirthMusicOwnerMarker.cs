using System;
using System.Xml.Linq;
// Canonical marker DATA only. No CVar calls or native owner/generation authority.
public sealed class RebirthMusicOwnerMarker
{
 public const string Prefix="_rbmusicowner_v1_";
 public const int MaximumKeyLength=4096;
 public enum Kind { OriginalIntent, AdoptedWitness }
 public Kind Role {get;}
 public Guid SavedWorldId {get;}
 public Guid TransactionId {get;}
 public Guid GenerationId {get;}
 public string CreationId {get;}
 public string OwnerStorageKeyDigest {get;}
 public string IntentDigest {get;}
 public string WitnessDigest {get;}
 private RebirthMusicOwnerMarker(Kind role,Guid world,Guid transaction,Guid generation,string creation,string ownerDigest,string intentDigest,string witnessDigest)
 {Role=role;SavedWorldId=world;TransactionId=transaction;GenerationId=generation;CreationId=creation;OwnerStorageKeyDigest=ownerDigest;IntentDigest=intentDigest;WitnessDigest=witnessDigest;}
 public static bool TryCreate(RebirthMusicOwnerIntent intent,RebirthMusicOwnerWitness witness,out RebirthMusicOwnerMarker marker)
 {
  marker=null;
  try
  {
   if(intent==null||!RebirthMusicOwnerIntent.TryRead(intent.Write(),out var original)||witness!=null&&(witness.Intent.Digest!=original.Digest||!RebirthMusicOwnerWitness.TryRead(witness.Write(),out var verified)))return false;
   var candidate=new RebirthMusicOwnerMarker(witness==null?Kind.OriginalIntent:Kind.AdoptedWitness,original.SavedWorldId,original.TransactionId,original.GenerationId,original.CreationId,RebirthMusicOwnerDataCodec.Hash(original.OwnerStorageKey),original.Digest,witness?.Digest??string.Empty);
   if(!candidate.TryEncodeKey(out var key))return false;marker=candidate;return true;
  }
  catch{return false;}
 }
 public XElement Write()=>new XElement("musicOwnerMarker",new XAttribute("version",1),new XAttribute("role",Role==Kind.OriginalIntent?"original":"witness"),new XAttribute("world",SavedWorldId.ToString("N")),new XAttribute("transaction",TransactionId.ToString("N")),new XAttribute("generation",GenerationId.ToString("N")),new XAttribute("creation",CreationId),new XAttribute("ownerDigest",OwnerStorageKeyDigest),new XAttribute("intentDigest",IntentDigest),new XAttribute("witnessDigest",WitnessDigest));
 public bool TryEncodeKey(out string key)
 {key=null;if(!RebirthMusicOwnerDataCodec.Encode(Write(),out var bytes)||bytes.Length>(MaximumKeyLength-Prefix.Length)/2)return false;key=Prefix+BitConverter.ToString(bytes).Replace("-",string.Empty).ToLowerInvariant();return true;}
 public static bool TryRead(string key,float value,out RebirthMusicOwnerMarker marker)
 {
  marker=null;
  if(value!=1f||key==null||key.Length>MaximumKeyLength||!key.StartsWith(Prefix,StringComparison.Ordinal)||key.Length==Prefix.Length||(key.Length-Prefix.Length)%2!=0)return false;
  try
  {
   var bytes=new byte[(key.Length-Prefix.Length)/2];
   for(int i=0;i<bytes.Length;i++){int hi=Hex(key[Prefix.Length+i*2]),lo=Hex(key[Prefix.Length+i*2+1]);if(hi<0||lo<0)return false;bytes[i]=(byte)(hi*16+lo);}
   if(!RebirthMusicOwnerDataCodec.Decode(bytes,out var node)||!RebirthMusicOwnerDataCodec.Shape(node,"musicOwnerMarker",new[]{"version","role","world","transaction","generation","creation","ownerDigest","intentDigest","witnessDigest"},new string[0])||(string)node.Attribute("version")!="1"||
      !RebirthMusicOwnerDataCodec.GuidField(node,"world",out var world)||!RebirthMusicOwnerDataCodec.GuidField(node,"transaction",out var transaction)||!RebirthMusicOwnerDataCodec.GuidField(node,"generation",out var generation)||!RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||
      !RebirthMusicOwnerDataCodec.IsDigest((string)node.Attribute("ownerDigest"))||!RebirthMusicOwnerDataCodec.IsDigest((string)node.Attribute("intentDigest")))return false;
   string role=(string)node.Attribute("role"),witness=(string)node.Attribute("witnessDigest");
   if(role!="original"&&role!="witness"||role=="original"&&witness!=string.Empty||role=="witness"&&!RebirthMusicOwnerDataCodec.IsDigest(witness))return false;
   var candidate=new RebirthMusicOwnerMarker(role=="original"?Kind.OriginalIntent:Kind.AdoptedWitness,world,transaction,generation,creation,(string)node.Attribute("ownerDigest"),(string)node.Attribute("intentDigest"),witness);
   if(!RebirthMusicOwnerDataCodec.Canonical(candidate.Write(),bytes)||!candidate.TryEncodeKey(out var canonical)||canonical!=key)return false;marker=candidate;return true;
  }
  catch{return false;}
 }
 public bool MatchesData(RebirthMusicOwnerIntent intent,RebirthMusicOwnerWitness witness)
 {return TryCreate(intent,witness,out var expected)&&TryEncodeKey(out var key)&&expected.TryEncodeKey(out var other)&&key==other;}
 private static int Hex(char c)=>c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:-1;
}