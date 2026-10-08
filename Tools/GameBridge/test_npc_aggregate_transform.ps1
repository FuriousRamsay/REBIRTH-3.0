#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content (Join-Path $root 'Scripts/NPC/Persistence/RebirthNpcAggregateTransformCapture.cs') -Raw
$source=$source.Replace('using System;','').Replace('using UnityEngine;','')
$fixture=Get-Content (Join-Path $PSScriptRoot 'test_npc_aggregate_transform_fixture.cs') -Raw
Add-Type -TypeDefinition ($fixture+$source)
[TransformFixture]::Run()