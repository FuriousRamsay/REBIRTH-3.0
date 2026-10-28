using System;using System.IO;using System.Linq;using System.Text;
// Complete actual compact closure codec; Vector3i and bounded native string
// reader are explicit adapters. No native final evidence is synthesized.
struct Vector3i{internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>owner?.Length==64&&creation=="creation";}
static class RebirthSurvivorNetworkCodec{internal static string ReadBoundedString(BinaryReader reader,int maximum){string text=reader.ReadString();if(text.Length>maximum)throw new InvalidDataException();return text;}}
// ACTUAL_CLOSED_CODEC
static class Program
{
 static int count;static void Check(bool value){count++;if(!value)throw new Exception("Actual closed metadata assertion "+count);}
 static RebirthProductiveEarnedRemoteClosedRecord.Description Make()=>new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=Guid.NewGuid(),Birth=Guid.NewGuid(),Nonce=Guid.NewGuid(),World="world",Owner=new string('a',64),Creation="creation",Position=new Vector3i(1,2,3),Kind=1,OriginalDigest=new string('b',64),HeaderDigest=new string('c',64),HoldDigest=new string('d',64),TerminalDigest=new string('e',64)};
 static void Main(){var value=Make();var bytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(value);Check(RebirthProductiveEarnedRemoteClosedRecord.TryRead(bytes,out var read)&&read.Operation==value.Operation&&read.Position.z==3&&read.Kind==1);Check(bytes.SequenceEqual(RebirthProductiveEarnedRemoteClosedRecord.Encode(read)));value.Kind=2;Check(RebirthProductiveEarnedRemoteClosedRecord.TryRead(RebirthProductiveEarnedRemoteClosedRecord.Encode(value),out read)&&read.Kind==2);
 var changed=(byte[])bytes.Clone();changed[30]^=1;Check(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(changed,out _));Check(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(bytes.Take(bytes.Length-1).ToArray(),out _));Check(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(bytes.Concat(new byte[]{0}).ToArray(),out _));value=Make();value.Kind=0;Check(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value));value=Make();value.Nonce=Guid.Empty;Check(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value));value=Make();value.Deaths=-1;Check(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value));value=Make();value.TerminalDigest=new string('E',64);Check(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value));value=Make();value.Creation="foreign";Check(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value));value=Make();value.World=new string('w',45);Check(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value));Console.WriteLine(count+" actual closed metadata assertions passed");}
}
