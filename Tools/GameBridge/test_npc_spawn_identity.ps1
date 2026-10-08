#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path @((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcSpawnIdentity.cs'),(Join-Path $PSScriptRoot 'test_npc_spawn_identity_fixture.cs'))
[SpawnIdentityFixture]::Run()