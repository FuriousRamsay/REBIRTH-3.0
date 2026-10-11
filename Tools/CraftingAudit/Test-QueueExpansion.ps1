$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Crafting/UI/XUiC_RebirthStationWorkspace.cs') -Raw
$start=$source.IndexOf('    internal static void ExpandQueue(')
$end=$source.IndexOf('    private void RefreshToolPreviews()', $start)
if($start -lt 0 -or $end -lt $start){throw 'Actual queue expansion method not found'}
$method=$source.Substring($start,$end-$start).Replace('internal static void','public static void')
Add-Type -TypeDefinition ('using System; public class RecipeQueueItem { public string Marker; } public class XUiM_Workstation { public RecipeQueueItem[] Queue; public int Writes; public RecipeQueueItem[] GetRecipeQueueItems(){return Queue;} public void SetRecipeQueueItems(RecipeQueueItem[] q){Queue=q;Writes++;} } public static class Expansion {'+$method+'}')
foreach($size in @(0,1,4,15,16,20)){
 $data=[XUiM_Workstation]::new();$data.Queue=[RecipeQueueItem[]]::new($size)
 for($i=0;$i -lt $size;$i++){$data.Queue[$i]=[RecipeQueueItem]::new();$data.Queue[$i].Marker="job$i"}
 $original=$data.Queue
 [Expansion]::ExpandQueue($data)
 $expected=[Math]::Max(16,$size)
 if($data.Queue.Length -ne $expected){throw "Capacity failed $size"}
 for($i=0;$i -lt $size;$i++){if(-not [Object]::ReferenceEquals($data.Queue[$expected-$size+$i],$original[$i])){throw "Order/identity failed $size/$i"}}
 for($i=0;$i -lt $expected-$size;$i++){if($null -eq $data.Queue[$i]){throw "Uninitialized empty slot $i"}}
 [Expansion]::ExpandQueue($data)
 if($data.Writes -ne [int]($size -lt 16)){throw "Idempotence failed $size"}
 "PASS size=$size capacity=$expected preserves order/identity and idempotence"
}
[Expansion]::ExpandQueue($null)
'PASS null model'
