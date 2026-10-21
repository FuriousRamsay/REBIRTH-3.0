$ErrorActionPreference='Stop'
if (Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue) { throw 'Close the game before offline validation' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ref = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
$csc = 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll'
$json = [IO.Path]::GetFullPath((Join-Path $root '../../7DaysToDie_Data/Managed/Newtonsoft.Json.dll'))
$out = Join-Path $PSScriptRoot 'out/batch-lease-fixture'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$argsList = @($csc,'/nologo','/noconfig','/nostdlib','/langversion:latest','/target:exe',('/out:' + (Join-Path $out 'lease.exe')),('/r:' + (Join-Path $ref 'mscorlib.dll')),('/r:' + (Join-Path $ref 'System.dll')),('/r:' + (Join-Path $ref 'System.Core.dll')),('/r:' + (Join-Path $ref 'Facades/netstandard.dll')),(Join-Path $PSScriptRoot 'batch-lease-fixture/Program.cs'),(Join-Path $root 'Scripts/GameBridge/RebirthGameBridgeBatch.cs'))
& dotnet @argsList
if ($LASTEXITCODE) { throw 'Actual lease compilation failed' }

& (Join-Path $out 'lease.exe')
if ($LASTEXITCODE) { throw 'Actual lease tests failed' }
