$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$a=$source.IndexOf('    internal bool TryAdmitPreparedAi(');$b=$source.IndexOf('    public override void OnUpdateEntity()',$a)
if($a -lt 0 -or $b -le $a){throw 'actual admission method absent'}
$body=$source.Substring($a,$b-$a).Replace('RebirthNpcPresenceState','PresenceState')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualAdmission.cs'),'using System;partial class AdmissionActor {'+$body+'}')
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'actual scheduler fixture failed'}
