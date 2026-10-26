using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
namespace RebirthEntityStudio {
 // Offline-prepared linear cage cache. No Blender solver code is embedded.
 public sealed class RebirthCageBindingRange {
  internal Vector3[] baseline,rest;
  internal int[] offsets,indices;
  internal float[] weights;
  static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
  public Vector3[] Evaluate(Vector3[] cage) {
   if(cage==null||cage.Length!=rest.Length)throw new InvalidDataException("Cage control count differs from binding");
   var delta=new Vector3[cage.Length];
   for(int i=0;i<cage.Length;i++){if(!Finite(cage[i]))throw new InvalidDataException("Non-finite cage control");delta[i]=cage[i]-rest[i];}
   var result=new Vector3[baseline.Length];
   for(int i=0;i<result.Length;i++){
    Vector3 displacement=Vector3.zero;
    for(int k=offsets[i];k<offsets[i+1];k++)displacement+=delta[indices[k]]*weights[k];
    result[i]=baseline[i]+displacement;
   }
   return result;
  }
 }
 public sealed class RebirthCageBindingFile:IDisposable {
  readonly FileStream stream;
  readonly BinaryReader reader;
  readonly int vertices,controls,influences;
  readonly long baselineStart,restStart,offsetStart,indexStart,weightStart;
  public int VertexCount=>vertices;
  static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
  static string Hash(Stream s){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  static bool Identity(string h)=>h!=null&&h.Length==64&&System.Text.RegularExpressions.Regex.IsMatch(h,"^[0-9a-f]{64}$");
  public RebirthCageBindingFile(string path,string expectedBinaryHash,string templateHash,string foundationHash) {
   if(!Identity(expectedBinaryHash)||!Identity(templateHash)||!Identity(foundationHash))throw new InvalidDataException("Binding identities are required");
   stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read);
   try {
    if(Hash(stream)!=expectedBinaryHash)throw new InvalidDataException("Cage binding source changed");stream.Position=0;
    reader=new BinaryReader(stream,Encoding.ASCII,true);
    if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="RBTCAGE1")throw new InvalidDataException("Unsupported cage binding format");
    vertices=reader.ReadInt32();controls=reader.ReadInt32();influences=reader.ReadInt32();
    if(vertices<1||vertices>1000000||controls<4||controls>65535||influences<vertices||influences>100000000)throw new InvalidDataException("Invalid cage dimensions");
    if(Encoding.ASCII.GetString(reader.ReadBytes(64))!=templateHash||Encoding.ASCII.GetString(reader.ReadBytes(64))!=foundationHash)throw new InvalidDataException("Binding requires its reviewed template and foundation");
    if(!Identity(Encoding.ASCII.GetString(reader.ReadBytes(64))))throw new InvalidDataException("Invalid measured-binding identity");
    baselineStart=stream.Position;restStart=baselineStart+12L*vertices;offsetStart=restStart+12L*controls;indexStart=offsetStart+4L*(vertices+1);weightStart=indexStart+2L*influences;
    if(stream.Length!=weightStart+2L*influences)throw new InvalidDataException("Binding byte count differs from header");
   } catch {stream.Dispose();throw;}
  }
  Vector3 ReadVector(){float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle();if(!Finite(x)||!Finite(y)||!Finite(z))throw new InvalidDataException("Non-finite binding coordinates");return new Vector3(x,y,z);}
  public Vector3[] ReadRestCage(){stream.Position=restStart;var result=new Vector3[controls];for(int i=0;i<controls;i++)result[i]=ReadVector();return result;}
  // Load only the anatomical component required by a visible wound. Hash once
  // on opening the library; retain loaded ranges across subsequent slider edits.
  public RebirthCageBindingRange LoadRange(int first,int count) {
   if(first<0||count<1||first>vertices-count)throw new ArgumentOutOfRangeException("Invalid anatomy vertex range");
   var result=new RebirthCageBindingRange{baseline=new Vector3[count],rest=new Vector3[controls],offsets=new int[count+1]};
   stream.Position=baselineStart+12L*first;for(int i=0;i<count;i++)result.baseline[i]=ReadVector();
   stream.Position=restStart;for(int i=0;i<controls;i++)result.rest[i]=ReadVector();
   stream.Position=offsetStart+4L*first;int start=reader.ReadInt32();result.offsets[0]=0;
   if(start<0||start>influences)throw new InvalidDataException("Invalid cage row offset");
   for(int i=1;i<=count;i++){int value=reader.ReadInt32();if(value<=start+result.offsets[i-1]||value>influences)throw new InvalidDataException("Invalid cage row ordering");result.offsets[i]=value-start;}
   int total=result.offsets[count];result.indices=new int[total];result.weights=new float[total];
   stream.Position=indexStart+2L*start;for(int i=0;i<total;i++){int index=reader.ReadUInt16();if(index>=controls)throw new InvalidDataException("Unknown cage control");result.indices[i]=index;}
   stream.Position=weightStart+2L*start;for(int i=0;i<total;i++){float weight=Mathf.HalfToFloat(reader.ReadUInt16());if(!Finite(weight)||weight<=0||weight>1)throw new InvalidDataException("Invalid cage weight");result.weights[i]=weight;}
   for(int i=0;i<count;i++){float sum=0;for(int k=result.offsets[i];k<result.offsets[i+1];k++)sum+=result.weights[k];if(Mathf.Abs(sum-1)>.01f)throw new InvalidDataException("Cage weights do not sum to one");for(int k=result.offsets[i];k<result.offsets[i+1];k++)result.weights[k]/=sum;}
   return result;
  }
  public void Dispose(){if(reader!=null)reader.Dispose();stream.Dispose();}
 }
}
