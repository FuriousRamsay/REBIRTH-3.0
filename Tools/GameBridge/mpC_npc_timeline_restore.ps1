# Source-only runner; NOT_RUN. Explicit invocation builds/runs an isolated offline fixture.
# No game DLL, game process, install output, or real save is used.
#requires -Version 7.0
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-mp045-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work)|Out-Null
$source=[IO.File]::ReadAllText((Join-Path $repo 'Scripts/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs'))
function Slice([string]$first,[string]$next){$a=$source.IndexOf($first,[StringComparison]::Ordinal);$b=$source.IndexOf($next,$a,[StringComparison]::Ordinal);if ($a -lt 0 -or $b -le $a){throw "Actual member extraction failed: $first"};$source.Substring($a,$b-$a)}
$methods=(Slice '    private static void RestoreCheckpoint(' '    private static bool ValidateCheckpoint(')+(Slice '    private static string HashFile(' '    private static void Prune(')
$journal=[IO.File]::ReadAllText((Join-Path $repo 'Scripts/NPC/Persistence/RebirthNpcTimelineRestoreJournal.cs'))
$durable=[IO.File]::ReadAllText((Join-Path $repo 'Scripts/Persistence/RebirthDurableFileCommit.cs'))
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'mpC_npc_timeline_restore_fixture.cs'))
# Using directives from whole actual helper sources are lifted to the common file header.
$usings=[regex]::Matches(($journal+"`n"+$durable),'(?m)^using [^\r\n]+;')|ForEach-Object Value|Select-Object -Unique
$journal=[regex]::Replace($journal,'(?m)^using [^\r\n]+;\r?\n','')
$durable=[regex]::Replace($durable,'(?m)^using [^\r\n]+;\r?\n','')
$combined=($usings-join "`n")+"`n"+$fixture.Replace('// ACTUAL_RESTORE_METHODS',$methods)+"`n"+$journal+"`n"+$durable
[IO.File]::WriteAllText((Join-Path $work 'Program.cs'),$combined)
[IO.File]::WriteAllText((Join-Path $work 'Fixture.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><DefineConstants>DEBUG;REBIRTH_DEBUG</DefineConstants><EnableDefaultCompileItems>true</EnableDefaultCompileItems><Nullable>disable</Nullable></PropertyGroup></Project>')
Write-Host "Contained offline fixture retained at $work"
dotnet build (Join-Path $work 'Fixture.csproj') -o (Join-Path $work 'bin') --nologo
if ($LASTEXITCODE -ne 0){throw 'Offline fixture compilation failed'}
dotnet (Join-Path $work 'bin/Fixture.dll') suite (Join-Path $work 'cases')
if ($LASTEXITCODE -ne 0){throw 'Offline fixture failed; contained evidence retained'}
