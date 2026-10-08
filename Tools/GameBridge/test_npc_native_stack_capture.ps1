#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content (Join-Path $root 'Scripts/NPC/Inventory/RebirthNpcNativeStackRecord.cs') -Raw
$foundation=Get-Content (Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs') -Raw
$start=$foundation.IndexOf('public readonly struct RebirthNpcStableId')
$end=$foundation.IndexOf('public sealed class RebirthNpcProfile',$start)
if($start -lt 0 -or $end -le $start){throw 'Stable ID extraction failed'}
$fixture=Get-Content (Join-Path $PSScriptRoot 'test_npc_native_stack_capture_fixture.cs') -Raw
$setSource=Get-Content (Join-Path $root 'Scripts/NPC/Inventory/RebirthNpcNativeStackSet.cs') -Raw
$setSource=[regex]::Replace($setSource,'(?m)^using [^\r\n]+;\r?\n','')
Add-Type -TypeDefinition ('using System.Collections.Generic;'+$source+$setSource+$foundation.Substring($start,$end-$start)+$fixture)
[NativeStackFixture]::Run()