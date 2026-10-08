$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/UI/RebirthToolbeltCapacity.cs')
$start=$source.IndexOf('    public static int GetOwnedSlotCount(')
$end=$source.IndexOf('    public static int GetSlotsForPlayer(', $start)
if($start -lt 0 -or $end -le $start){throw 'Production slot-range helper missing'}
$method=$source.Substring($start,$end-$start)
Add-Type -TypeDefinition ('using System; public class EntityPlayer {public int Capacity;} public static class Checks {' +
 'static int GetSlotsForPlayer(EntityPlayer player){return player.Capacity;}' + $method + @'
 public static void Run(){
  var p=new EntityPlayer{Capacity=4};
  if(GetOwnedSlotCount(p,20)!=4)throw new Exception("Starting belt exposed locked slots");
  p.Capacity=18;if(GetOwnedSlotCount(p,20)!=18)throw new Exception("Expanded belt range wrong");
  if(GetOwnedSlotCount(p,11)!=11)throw new Exception("Last real grid slot was dropped");
  if(GetOwnedSlotCount(p,1)!=1||GetOwnedSlotCount(p,0)!=0)throw new Exception("Zero/one-slot grid wrong");
  p.Capacity=10;if(GetOwnedSlotCount(p,10)!=10)throw new Exception("Vanilla belt changed");
 }
}
'@)
[Checks]::Run()
Write-Output 'PASS: production range uses every real ItemGrid slot and excludes locked slots for starting, expanded, short, empty and native inventories. Capacity provider stubbed.'
