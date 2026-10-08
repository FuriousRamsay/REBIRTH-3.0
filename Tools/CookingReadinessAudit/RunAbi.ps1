$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path;$native='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/7DaysToDie_Data/Managed'
[xml]$project=Get-Content (Join-Path $root 'RebirthUtils.csproj') -Raw
$refs=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($reference in $project.Project.ItemGroup.Reference){if($reference.HintPath){$path=([string]$reference.HintPath).Replace('$(RebirthManagedRoot)',$native);if(Test-Path $path){[void]$refs.Add((Resolve-Path $path).Path)}}}
foreach($name in @('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','System.Runtime.Serialization','System.Data','System.Numerics')){[void]$refs.Add((Join-Path $native ($name+'.dll')))}
[void]$refs.Add((Join-Path $root 'RebirthUtils.dll'))
$out=Join-Path $PSScriptRoot out;[IO.Directory]::CreateDirectory($out)|Out-Null
$compilerArgs=@('/nologo','/noconfig','/nostdlib+','/target:library','/langversion:latest',('/out:'+(Join-Path $out 'CookingPreflightAbi.dll')))
$compilerArgs+=@($refs|Sort-Object|ForEach-Object{'/r:'+$_});$compilerArgs+=Join-Path $PSScriptRoot 'CandidateAbi.cs'
$result=& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' @compilerArgs 2>&1;$exit=$LASTEXITCODE;$result|Tee-Object (Join-Path $out 'COMPILE.txt');if($exit-ne0){throw 'Candidate ABI compile failed'}
'PASS candidate shared preflight actual native/current mod ABI; private Matches/BatchRecipe host adapters compile only.'