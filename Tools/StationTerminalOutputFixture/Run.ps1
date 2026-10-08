$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
& (Join-Path $root 'Tools/SecondTeam/Medicine/ItemConformance/StackArrayIntegration/Run.ps1') -StageOnly

function Member([string]$s,[string]$sig){$a=$s.IndexOf($sig);if($a -lt 0){throw "Missing $sig"};$b=$s.IndexOf('{',$a);$i=$b+1;$d=1;while($d){if($s[$i] -eq '{'){$d++};if($s[$i] -eq '}'){$d--};$i++};$s.Substring($a,$i-$a)}
$base=[IO.File]::ReadAllText((Join-Path $root 'Tools/SecondTeam/Medicine/ItemConformance/StackArrayIntegration/out/Check.cs'))
$native=[IO.File]::ReadAllText((Join-Path $root 'Tools/ThirdTeam/Native/CraftCompleteData.cs'))
$base += 'public class CraftCompleteData{public int CrafterEntityID;public ItemStack CraftedItemStack;public string RecipeName="",ItemScrapped="";public int CraftExpGain;public ushort RecipeUsedCount;public CraftCompleteData(){}'+(Member $native 'public CraftCompleteData(int crafterEntityID')+(Member $native 'public void Read(PooledBinaryReader')+(Member $native 'public void Write(PooledBinaryWriter')+'}'
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Persistence/RebirthStationTerminalContents.cs'))
$base += 'internal static class RebirthStationTerminalContents{'+(Member $source 'internal static bool TryReadOutput(')+'}'
$base += [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixture.cs'))
. (Join-Path $root 'Tools/StationTerminalCompletionFixture/NativeQueueSupport.ps1')
$base=Add-NativeQueueFixture $base $root
$out=Join-Path $PSScriptRoot out;[IO.Directory]::CreateDirectory($out)|Out-Null
[IO.File]::WriteAllText((Join-Path $out 'Check.cs'),$base)
$refs=@('mscorlib','System','System.Core')|ForEach-Object {'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+$_+'.dll'}
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' /nologo /unsafe /target:exe /main:TerminalOutputCheck ('/out:'+(Join-Path $out 'Check.exe')) @refs (Join-Path $out 'Check.cs')
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& (Join-Path $out 'Check.exe')|Tee-Object (Join-Path $out 'RESULT.txt');if($LASTEXITCODE -ne 0){throw 'Assertions failed'}