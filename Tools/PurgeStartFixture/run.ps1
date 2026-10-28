$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$compiler=Get-ChildItem 'C:/Program Files/dotnet/sdk/*/Roslyn/bincore/csc.dll' | Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName
$reference='C:/Users/Etienne/.nuget/packages/microsoft.netframework.referenceassemblies.net481/1.0.3/build/.NETFramework/v4.8.1'
$output=Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $output | Out-Null
& dotnet $compiler /nologo /target:exe /out:"$output/PurgeStartFixture.exe" /r:"$reference/mscorlib.dll" /r:"$reference/System.dll" "$PSScriptRoot/Program.cs" "$projectRoot/Scripts/Purge/Runtime/RebirthPurgeStartFootprint.cs"
if($LASTEXITCODE -ne 0){throw 'Footprint fixture compilation failed'}
& "$output/PurgeStartFixture.exe"
if($LASTEXITCODE -ne 0){throw 'Footprint fixture failed'}
