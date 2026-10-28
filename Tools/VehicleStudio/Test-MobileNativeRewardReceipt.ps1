#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationNativeRewardReceipt.cs'))
$camperStart=$camperSource.IndexOf('    internal static string Key(')
$camperEnd=$camperSource.IndexOf('    internal static bool HasSaved(',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Native receipt methods missing.'}
$camperFixture=@'
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
public class RebirthStationGridAdmission
{
 public bool IsPublicationAttempted=true,Mobile=true;public string Creation="a";
 public bool TryGetMobileAddress(out object value){value=null;return Mobile;}
 public XElement Write()=>new XElement("admission",new XAttribute("creation",Creation));
}
public static class EntityBuffs { public const int Version=2; }
public class BuffValue { public void Read(Reader r,int version){r.ReadByte();} }
public sealed class Reader:IDisposable
{
 private BinaryReader reader;public void SetBaseStream(Stream s){reader=new BinaryReader(s,Encoding.UTF8,true);}
 public byte ReadByte()=>reader.ReadByte();public ushort ReadUInt16()=>reader.ReadUInt16();public string ReadString()=>reader.ReadString();public float ReadSingle()=>reader.ReadSingle();
 public void Dispose(){reader?.Dispose();}
}
public class Pool { public Reader AllocSync(bool b)=>new Reader(); }
public static class MemoryPools { public static Pool poolBinaryReader=new Pool(); }
public static class ReceiptFixture
{
METHOD_BODY
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 private static byte[] Blob(string key,float value,bool duplicate=false,bool tail=false)
 {
  using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
   writer.Write((byte)2);writer.Write((ushort)1);writer.Write((byte)0);writer.Write((ushort)(duplicate?3:2));
   writer.Write("unrelated");writer.Write(3f);writer.Write(key);writer.Write(value);if(duplicate){writer.Write(key);writer.Write(value);}if(tail)writer.Write((byte)0);
  }return stream.ToArray();}
 }
 public static void Run()
 {
  var admission=new RebirthStationGridAdmission();var key=Key(admission);
  Check(key.Length==78&&key==Key(admission),"stable bound key");admission.Creation="b";Check(Key(admission)!=key,"creation changes key");
  Check(Contains(Blob(key,1),key,true)&&!Contains(Blob(key,1),key,false),"applied stamp only");
  Check(Contains(Blob(key,2),key,false)&&!Contains(Blob(key,2),key,true),"in-flight stamp distinct");
  Check(TryRead(Blob(key,0),key,out var phase)&&phase==0,"explicit never-applied state");
  Check(TryRead(Blob(Key(admission),1),key,out phase)&&phase==0,"valid absent key differs from unreadable blob");
  Check(!TryRead(null,key,out phase)&&float.IsNaN(phase),"unreadable evidence remains unknown");
  Check(!TryRead(Blob(key,3),key,out phase)&&float.IsNaN(phase),"unrecognized phase remains unknown");
  Check(!TryRead(Blob(key,1),"rbCamperCraft_"+new string('Z',64),out phase),"malformed key refused");
  Check(!Contains(Blob(key,1,true),key,true)&&!Contains(Blob(key,1,false,true),key,true),"duplicate and trailing bytes refused");
  Check(!Contains(Blob(key,float.NaN),key,true)&&!Contains(Blob(key,float.PositiveInfinity),key,true),"nonfinite stamps refused");
  var truncated=Blob(key,1);Array.Resize(ref truncated,truncated.Length-1);Check(!Contains(truncated,key,true),"truncated payload refused");
  var version=Blob(key,1);version[0]=1;Check(!Contains(version,key,true),"wrong version refused");
  admission.Mobile=false;try{Key(admission);throw new Exception("terrain key accepted");}catch(ArgumentException){}
  Console.WriteLine("PASS actual native reward receipt key/parser: scoped identity, applied/in-flight distinction, duplicates, tails, nonfinite stamps, truncation/version and mobile admission. Binary reader/buff adapters doubled; no native file proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD_BODY',$camperSource.Substring($camperStart,$camperEnd-$camperStart))
[ReceiptFixture]::Run()
