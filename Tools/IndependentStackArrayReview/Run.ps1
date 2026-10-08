$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function Member($s,$sig){$a=$s.IndexOf($sig);if($a-lt0){throw "missing $sig"};$b=$s.IndexOf('{',$a);$d=1;$i=$b+1;while($d){if($s[$i]-eq'{'){$d++};if($s[$i]-eq'}'){$d--};$i++};$s.Substring($a,$i-$a)}
$src=[IO.File]::ReadAllText("$root/Tools/SecondTeam/Medicine/ItemConformance/StackArrayIntegration/out/Check.cs")
$native=[IO.File]::ReadAllText("$root/Tools/SecondTeam/Medicine/NativeValue/installed/ItemValue.cs")
$nativeGetter=(Member $native 'public bool TryGetMetadata(string key, out string value)')+(Member $native 'public bool TryGetMetadata(string key, out object value,')
$src=$src.Replace((Member $src 'public bool TryGetMetadata(string k,out string v)'),$nativeGetter)
$extra=@"
public static class IndependentStackAudit {
static int n;static void A(bool b,string name){if(!b)throw new Exception(name);n++;Console.WriteLine(name);}
public static void Main(){
ItemClass.list[1]=new ItemClass{Name="book"};ItemClass.list[3]=new ItemClass{Name="backpack"};
foreach(bool library in new[]{true,false})foreach(bool integer in new[]{true,false}){
string key=library?RebirthBackpackLibraryContents.MetadataKey:RebirthBackpackSellStashContents.MetadataKey;
var pack=new ItemValue(3);pack.SetMetadata(key,new TypedMetadataValue(integer?(object)7:(object)7f,integer?TypedMetadataValue.TypeTag.Integer:TypedMetadataValue.TypeTag.Float));
ItemStack[] read;bool ok=library?RebirthBackpackLibraryContents.TryRead(pack,out read):RebirthBackpackSellStashContents.TryRead(pack,out read);
A(ok&&read.All(s=>s.count==0),"REPRO existing wrong-type "+key+" reported empty");
var rows=ItemStack.CreateArray(3);rows[0].count=1;rows[0].itemValue=new ItemValue(1);ItemValue candidate;
bool write=library?RebirthBackpackLibraryContents.TryWrite(pack,rows,out candidate):RebirthBackpackSellStashContents.TryWrite(pack,rows,out candidate);
A(write&&candidate!=null,"REPRO TryWrite success despite wrong-type reserved key");
ok=library?RebirthBackpackLibraryContents.TryRead(candidate,out read):RebirthBackpackSellStashContents.TryRead(candidate,out read);
A(ok&&read.All(s=>s.count==0),"REPRO candidate lacks requested positive-count contents");
A(pack.Metadata[key].GetTypeTag()==(integer?TypedMetadataValue.TypeTag.Integer:TypedMetadataValue.TypeTag.Float)&&rows[0].count==1,"source unchanged, ambiguity not actual transfer loss proof");
}
var zero=ItemStack.CreateArray(3);zero[0].itemValue=null;string encoded;
A(RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(zero,49152,65536,out encoded),"zero-count null sentinel safely native encoded");
ItemStack[] decoded;A(RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(encoded,3,49152,65536,out decoded)&&decoded.All(s=>s.IsEmpty()),"zero-count native rows canonical empty; custody count stays zero");
Console.WriteLine(n+" independent observations/checks; reserved-key bug reproduced, not fixed; engine/pool/policy adapters same as461 fixture");
}}
"@
[IO.File]::WriteAllText("$PSScriptRoot/Check.cs",$src+$extra)
$refs=@('mscorlib','System','System.Core')|ForEach-Object {'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+$_+'.dll'}
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' /nologo /unsafe /target:exe /main:IndependentStackAudit "/out:$PSScriptRoot/Check.exe" @refs "$PSScriptRoot/Check.cs"
if($LASTEXITCODE-ne0){throw 'compile failed'}
& "$PSScriptRoot/Check.exe"
if($LASTEXITCODE-ne0){throw 'independent fixture failed'}

