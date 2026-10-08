$ErrorActionPreference='Stop'
$repoPath=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
function Extract-Member([string]$source,[string]$signature) {
 $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
 if($start -lt 0){throw "Missing exact member: $signature"}
 $open=$source.IndexOf('{',$start); $depth=1;$end=$open+1
 while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++}elseif($source[$end] -eq '}'){$depth--};$end++}
 if($depth -ne 0){throw "Unbalanced member: $signature"}
 return $source.Substring($start,$end-$start)
}
$teSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\ThirdTeam\ClosedQueue\native\TileEntityWorkstation.cs'))
$teMembers=@('public TileEntityWorkstation(Chunk _chunk)','public override void OnSetLocalChunkPosition()','public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)','public override void write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)','public void readItemStackArray(PooledBinaryReader _br, ref ItemStack[] stack)','public void writeItemStackArray(PooledBinaryWriter bw, ItemStack[] stack)')
$generatedText="namespace NativeLifecycle {`npublic partial class TileEntityWorkstation {`n"+ (($teMembers | ForEach-Object {Extract-Member $teSource $_}) -join "`n")+"`n}`n"
$gridSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationIntegrationAudit\native\XUiC_ItemStackGrid.cs'))
$generatedText+="public partial class XUiC_ItemStackGrid {`n"+(Extract-Member $gridSource 'public virtual void SetStacks(ItemStack[] stackList)')+"`n"+(Extract-Member $gridSource 'public virtual ItemStack[] GetSlots()')+"`n"+(Extract-Member $gridSource 'public virtual ItemStack[] getUISlots()')+"`n}`n"
$slotsSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationIntegrationAudit\native\XUiC_WorkstationGrid.cs'))
$generatedText+="public partial class XUiC_WorkstationGrid {`n"+(Extract-Member $slotsSource 'public virtual void SetSlots(ItemStack[] stacks)')+"`n}`n"
$modelSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationIntegrationAudit\native\XUiM_Workstation.cs'))
$generatedText+="public partial class XUiM_Workstation {`n"+(Extract-Member $modelSource 'public ItemStack[] GetInputStacks()')+"`n"+(Extract-Member $modelSource 'public void SetInputStacks(ItemStack[] _itemStacks)')+"`n}`n}`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeLifecycle.cs'),$generatedText,[Text.UTF8Encoding]::new($false))
Write-Output 'PREPARED actual installed lifecycle/UI members only; no compiler invoked.'
$candidateSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationNativeMigrationReview\NativeInputMigrationCandidate.cs'))
$candidateSource=$candidateSource.Replace('using System;','').Replace('using System.Collections.Generic;','')
$candidateRead=Extract-Member $teSource 'public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)'
$storedLoopPattern='int (num6|num4|num) = _br.ReadByte\(\);\s*for \(int (l|k|i) = 0; \2 < \1; \2\+\+\)\s*\{\s*currentMeltTimesLeft\[\2\] = _br.ReadSingle\(\);\s*\}'
$matchCount=[regex]::Matches($candidateRead,$storedLoopPattern).Count
if($matchCount -ne 3){throw "Expected three exact stored loops, got $matchCount"}
$candidateRead=[regex]::Replace($candidateRead,$storedLoopPattern,'currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);')
$candidateRead=$candidateRead.Replace('base.read(_br, _eStreamMode);','ReadNativeBase(_br, _eStreamMode); // Exact base member alias; avoid invoking baseline TE.read twice.')
$candidateGenerated="using System;`nusing System.Collections.Generic;`nnamespace NativeLifecycle {`n"+$candidateSource+"`npublic partial class CandidateTileEntityWorkstation:TileEntityWorkstation {public CandidateTileEntityWorkstation(Chunk c):base(c){}`n"+$candidateRead+"`n}`n}`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualCandidateLifecycle.cs'),$candidateGenerated,[Text.UTF8Encoding]::new($false))
Write-Output 'PREPARED exact candidate and native three-loop replacement; discard branches retained.'
$cloneSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationPaidCompletionIndependentFixture\native\ItemValue.cs'))
$stackSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationPaidCompletionIndependentFixture\native\ItemStack.cs'))
$cloneAdapters=@"
using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace NativeCloneBoundary {
public class TypedMetadataValue{public enum TypeTag {None,Float,Integer,String}public readonly TypeTag typeTag;public object value;public TypedMetadataValue(object val,TypeTag tag){typeTag=tag;value=val;}public object GetValue()=>value;public TypedMetadataValue Clone(){_ = typeTag;return new TypedMetadataValue(value,typeTag);}}
public struct TextureFullArray {public long Bits;}
public static class Extensions {public static ItemValue[] CloneItemValueArray(this ItemValue[] values)=>values?.Select(v=>v.Clone()).ToArray();}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){} }
public partial class ItemValue {public struct Stat {public int type;public bool isBoosted;public short value;}public static int NonemptyConstructorCalls;public int type,Meta;public float UseTimes;public ushort Quality,Seed;public byte SelectedAmmoTypeIndex,Flags;public Stat[] Stats;public ItemValue[] modifications,cosmeticMods;public Dictionary<string,TypedMetadataValue> Metadata;public TextureFullArray TextureFullArray;public ItemValue(){}public ItemValue(int t){type=t;if(t!=0)NonemptyConstructorCalls++;}public static ItemValue None=>new ItemValue(0);
"@
$cloneAdapters+=(Extract-Member $cloneSource 'public ItemValue Clone()')+"`n}`npublic partial class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public static ItemStack[] CreateArray(int n)=>Enumerable.Range(0,n).Select(_=>new ItemStack(new ItemValue(0),0)).ToArray();`n"+(Extract-Member $stackSource 'public ItemStack Clone()')+"`n}`n"+$candidateSource+@"

public static class Probe {public static bool Run(){var input=ItemStack.CreateArray(3);var previous=ItemStack.CreateArray(3);input[0].itemValue.Seed=123;previous[1].itemValue.Seed=456;bool accepted=RebirthPlainStationNativeMigrationCandidate.TryExpandProvenPlainInput(true,3,Array.Empty<string>(),Array.Empty<string>(),input,previous,new float[3],out _,out _,out _);var control=new ItemValue(0){Seed=789};control.Clone();return accepted&&input[0].itemValue.Seed==123&&previous[1].itemValue.Seed==456&&control.Seed==0;}}
}
"@
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeCloneBoundary.cs'),$cloneAdapters,[Text.UTF8Encoding]::new($false))
$cloneAdapters=$cloneAdapters.Replace('public ItemValue(){}',(Extract-Member $cloneSource 'public ItemValue()'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeCloneBoundary.cs'),$cloneAdapters,[Text.UTF8Encoding]::new($false))
$baseSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationIntegrationAudit\native\TileEntity.cs'))
$baseRead=Extract-Member $baseSource 'public virtual void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)'
$baseWrite=Extract-Member $baseSource 'public virtual void write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)'
$baseHandle=Extract-Member $baseSource 'public virtual void SetHandle(byte _handle)'
$streamSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationIntegrationAudit\native\StreamUtils.cs'))
$baseGenerated="using System.IO;namespace NativeLifecycle {public partial class TileBase {`n"+$baseRead+"`n"+$baseRead.Replace('public virtual void read(','public void ReadNativeBase(')+"`n"+$baseWrite+"`n"+$baseHandle+"`n}public static class StreamUtils {`n"+(Extract-Member $streamSource 'public static Vector3i ReadVector3i(BinaryReader _br)')+"`n"+(Extract-Member $streamSource 'public static void Write(BinaryWriter _bw, Vector3i _v)')+"`n} }"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeBase.cs'),$baseGenerated,[Text.UTF8Encoding]::new($false))
$remoteSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationNativeMigrationReview\RemoteFrameGuardCandidate.cs'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualRemoteGuard.cs'),"using System;using System.IO;namespace NativeLifecycle {"+$remoteSource.Replace('using System;','').Replace('using System.IO;','')+"}",[Text.UTF8Encoding]::new($false))
$transportSource=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationIntegrationAudit\native\NetPackageTileEntity.cs'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeTransport.cs'),"using TileEntity=NativeLifecycle.TileBase;namespace NativeLifecycle {public partial class NetPackageTileEntity {"+(Extract-Member $transportSource 'public override void ProcessPackage(World _world, GameManager _callbacks)')+"}}",[Text.UTF8Encoding]::new($false))
$nativeReplacement=Extract-Member $teSource 'public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)'
$baseReplacement=Extract-Member $baseSource 'public virtual void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)'
$reviewReplacement=[IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationNativeMigrationReview\TileEntityWorkstation.ReplacedBy.review.cs'))
$reviewReplacement=Extract-Member $reviewReplacement 'public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)'
$reviewReplacement=$reviewReplacement.Replace('base.ReplacedBy(_bvOld, _bvNew, _teNew);','ReplacedByNativeBase(_bvOld, _bvNew, _teNew);')
$replacementGenerated="using System.Collections.Generic;using TileEntity=NativeLifecycle.TileBase;namespace NativeLifecycle {public partial class TileBase {`n"+$baseReplacement+"`n"+$baseReplacement.Replace('public virtual void ReplacedBy(','public void ReplacedByNativeBase(')+"`n}public partial class TileEntityWorkstation {`n"+$nativeReplacement+"`n}public partial class CandidateTileEntityWorkstation {public bool CurrentLayoutWitness;private bool OriginalOwnerHasAuthenticatedCurrentNinePlainLayout(TileEntityWorkstation tile)=>CurrentLayoutWitness&&object.ReferenceEquals(tile,this);`n"+$reviewReplacement+"`n}}"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeReplacement.cs'),$replacementGenerated,[Text.UTF8Encoding]::new($false))
$positionProperty=Extract-Member $baseSource 'public Vector3i localChunkPos'
$processingNative=Extract-Member $teSource 'public void HandleMaterialInput(float timePassed)'
$setupReview=Extract-Member ([IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationNativeMigrationReview\TileEntityWorkstation.OnSetLocalChunkPosition.review.cs'))) 'public override void OnSetLocalChunkPosition()'
$processingReview=Extract-Member ([IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationNativeMigrationReview\TileEntityWorkstation.HandleMaterialInput.review.cs'))) 'public void HandleMaterialInput(float timePassed)'
$mappedReplacement=Extract-Member ([IO.File]::ReadAllText((Join-Path $repoPath 'Tools\StationNativeMigrationReview\TileEntityWorkstation.ReplacedByMapped.review.cs'))) 'public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)'
$mappedReplacement=$mappedReplacement.Replace('base.ReplacedBy(_bvOld, _bvNew, _teNew);','ReplacedByNativeBase(_bvOld, _bvNew, _teNew);')
$pairedGenerated="using System.Collections.Generic;using System.Runtime.CompilerServices;using TileEntity=NativeLifecycle.TileBase;namespace NativeLifecycle {public partial class TileBase {`n"+$positionProperty+"`n}public partial class TileEntityWorkstation {`n"+$processingNative+"`n}public partial class MappedReviewTile {`n"+$setupReview+"`n"+$processingReview.Replace('public void HandleMaterialInput','public new void HandleMaterialInput')+"`n"+$mappedReplacement+"`n}}"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualMappedNativeBodies.cs'),$pairedGenerated,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualMappedNativeRead.cs'),"namespace NativeLifecycle {public partial class MappedReviewTile {`n"+$candidateRead+"`n}}",[Text.UTF8Encoding]::new($false))