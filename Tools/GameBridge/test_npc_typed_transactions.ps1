#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$foundation=Get-Content (Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs') -Raw
$start=$foundation.IndexOf('public readonly struct RebirthNpcStableId');$end=$foundation.IndexOf('public sealed class RebirthNpcProfile',$start)
if($start -lt 0 -or $end -le $start){throw 'Stable ID extraction failed'}
$source='using System;using System.Collections.Generic;using System.Text;using System.Threading;using System.Globalization;using System.Linq;using System.Xml.Linq;'+$foundation.Substring($start,$end-$start)
foreach($file in @('RebirthNpcNativeStackRecord.cs','RebirthNpcNativeStackSet.cs','RebirthNpcInventoryTransactions.cs')){
 $part=Get-Content (Join-Path $root ('Scripts/NPC/Inventory/'+$file)) -Raw
 $source+=[regex]::Replace($part,'(?m)^using [^\r\n]+;\r?\n','')
}
$source+=Get-Content (Join-Path $PSScriptRoot 'test_npc_native_stack_capture_fixture.cs') -Raw
$source+=Get-Content (Join-Path $PSScriptRoot 'test_npc_typed_transactions_fixture.cs') -Raw
Add-Type -TypeDefinition $source
[TypedTransactionFixture]::Run()