$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$s=Get-Content (Join-Path $root 'native/XUiC_ItemStack.cs') -Raw
$a=$s.IndexOf('if (xui.PlayerInventory.AddItem(ItemStack))',$s.IndexOf('public void HandleMoveToPreferredLocation'))
$b=$s.IndexOf('break;', $a)
if($a -lt 0 -or $b -lt $a){throw 'Actual native pickup branch unavailable'}
Set-Content (Join-Path $PSScriptRoot 'NativePickup.cs') ('partial class TraceSlot { public void Pickup(){ int count=ItemStack.count; '+$s.Substring($a,$b-$a)+' }}')
dotnet run --project (Join-Path $PSScriptRoot 'Trace.csproj')
if($LASTEXITCODE -ne 0){throw 'Pickup trace failed'}
