using System;using System.Linq;
public static partial class Program{
static int wireChecks;
static void WireTests(RebirthGearPreparationRefusal original){
void W(bool b,string n){Check(b,n);wireChecks++;}
void Bad(byte[] b,string n){W(!RebirthGearPreparationRefusalWireCodec.TryDecode(b,out var v)&&v==null,n);}
W(RebirthGearPreparationRefusalWireCodec.TryEncode(original,out var bytes),"wire encode");
W(RebirthGearPreparationRefusalWireCodec.TryDecode(bytes,out var decoded)&&decoded.OriginalMarker==original.OriginalMarker&&decoded.RequestDigest==original.RequestDigest&&decoded.ObservedRevision==original.ObservedRevision,"wire exact roundtrip");
W(!RebirthGearPreparationRefusalWireCodec.TryEncode(null,out var absent)&&absent==null,"wire null encode");Bad(null,"wire null");Bad(Array.Empty<byte>(),"wire empty");
for(int i=0;i<bytes.Length;i++)Bad(bytes.Take(i).ToArray(),"wire truncate "+i);
foreach(var field in new[]{0,1}){var b=(byte[])bytes.Clone();b[field]=2;Bad(b,"wire schema reason");}
foreach(var length in new[]{0,1,4097,65535}){var b=(byte[])bytes.Clone();b[10]=(byte)length;b[11]=(byte)(length>>8);Bad(b,"wire bad length");}
Bad(bytes.Concat(new byte[]{0}).ToArray(),"wire trailing");
var high=(byte[])bytes.Clone();high[12]=128;Bad(high,"wire high ASCII");
foreach(var revision in new[]{-1L,0L,original.ExpectedRevision}){var b=(byte[])bytes.Clone();BitConverter.GetBytes(revision).CopyTo(b,2);Bad(b,"wire invalid observed");}
var maximum=(byte[])bytes.Clone();BitConverter.GetBytes(long.MaxValue).CopyTo(maximum,2);
W(RebirthGearPreparationRefusalWireCodec.TryDecode(maximum,out decoded)&&decoded.ObservedRevision==long.MaxValue,"wire maxlong supported observed revision");
Bad(new byte[RebirthGearPreparationRefusalWireCodec.MaximumBytes+1],"wire bound");
}
}
