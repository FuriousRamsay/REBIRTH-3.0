#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content (Join-Path $root 'Scripts/Survivor/Persistence/RebirthAtomicXmlFile.cs') -Raw
$part=Get-Content (Join-Path $root 'Scripts/NPC/Persistence/RebirthNpcPersistenceFile.cs') -Raw
$source+=[regex]::Replace($part,'(?m)^using [^\r\n]+;\r?\n','')
$source+=Get-Content (Join-Path $PSScriptRoot 'test_npc_persistence_publication_fixture.cs') -Raw
Add-Type -TypeDefinition $source
[NpcPublicationFixture]::Run()