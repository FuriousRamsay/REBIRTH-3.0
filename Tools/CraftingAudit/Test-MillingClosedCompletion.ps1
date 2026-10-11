$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Crafting/Cooking/RebirthCookingHeat.cs') -Raw
$start=$source.IndexOf('    public static bool TickTile(')
$end=$source.IndexOf('    // Prepare the whole result', $start)
if($start -lt 0 -or $end -lt $start){throw 'Actual TickTile method not found'}
$method=$source.Substring($start,$end-$start)
$stub=@"
using System; using System.Linq;
public class Recipe {public string craftingArea="WorkbenchMortarPestle001_FR";}
public class Entry {public Recipe Recipe=new Recipe();public short Multiplier=3;public int StartingEntityId=1;public bool IsCrafting=true;public float CraftingTimeLeft;}
public class TileEntityWorkstation {
 public Entry[] Queue={new Entry()}; public bool bUserAccessing,IsBurning;public ulong lastTickTime;public int Output,Reports,Cycles;
 public void setModified(){}
 public void cycleRecipeQueue(){if(Queue[0].Multiplier>0)return;Cycles++;Queue[0]=new Entry{Recipe=null,Multiplier=0};}
}
public class GameTimer {public static GameTimer Instance=new GameTimer();public ulong ticks=20;}
public static class RebirthCookingBatch {public static bool NeedsHeat(Recipe r){return false;}}
public static class HeatFixture {
 public static bool OutputFits=true;
 static bool Managed(Recipe r){return r!=null;} static void Advance(Recipe r,float d,bool h){}
 static float Remaining(Recipe r){return 0;} static bool Ready(Recipe r){return true;}
 static bool Burnt(Recipe r){return false;}
 static void ReportReady(TileEntityWorkstation t,Recipe r,int p,int o){t.Reports++;}
 static bool TryMillingOutput(int current,Recipe r,int portions,out int output){output=current+portions;return OutputFits;}
$method
}
"@
Add-Type -TypeDefinition $stub
$t=[TileEntityWorkstation]::new()
if([HeatFixture]::TickTile($t,1)){throw 'Managed tick delegated to native'}
if($t.Output -ne 3 -or $t.Cycles -ne 1 -or $null -ne $t.Queue[0].Recipe){throw 'Completed batch did not clear/advance'}
if(-not [HeatFixture]::TickTile($t,1) -or $t.Output -ne 3){throw 'Completed job emitted twice'}
'PASS completed closed-window batch emits once and advances'
[HeatFixture]::OutputFits=$false
$t=[TileEntityWorkstation]::new();[void][HeatFixture]::TickTile($t,1)
if($t.Output -ne 0 -or $t.Cycles -ne 0 -or $t.Queue[0].Multiplier -ne 3 -or $null -eq $t.Queue[0].Recipe){throw 'Full output changed queued job'}
'PASS full output preserves job and quantity'
$t=[TileEntityWorkstation]::new();$t.bUserAccessing=$true;[void][HeatFixture]::TickTile($t,1)
if($t.Output -ne 0 -or $t.Cycles -ne 0 -or $t.Reports -ne 0){throw 'Open window processed twice'}
'PASS open window leaves completion to UI owner'
[HeatFixture]::OutputFits=$true
$t=[TileEntityWorkstation]::new();$t.Queue[0].Recipe.craftingArea='campfire';[void][HeatFixture]::TickTile($t,1)
if($t.Output -ne 0 -or $t.Cycles -ne 0 -or $null -eq $t.Queue[0].Recipe){throw 'Cooking lost manual collection'}
'PASS cooking batch remains for manual collection'
