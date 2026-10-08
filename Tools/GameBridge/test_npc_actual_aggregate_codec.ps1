#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$managed=(Resolve-Path (Join-Path $root '../../7DaysToDie_Data/Managed')).Path
$taskTemp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-actual-codec-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($taskTemp) | Out-Null
$codecExe=Join-Path $taskTemp 'codec.exe'
$config=Join-Path $taskTemp 'codec.runtimeconfig.json'
try{
 dotnet 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' /nologo /target:exe "/out:$codecExe" '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll' '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll' '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xml.dll' '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xml.Linq.dll' (Join-Path $PSScriptRoot 'test_npc_actual_aggregate_codec.cs')
 if($LASTEXITCODE -ne 0){throw 'Codec fixture compile failed'}
 [IO.File]::WriteAllText($config,'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}')
 dotnet $codecExe $root $managed
 if($LASTEXITCODE -ne 0){throw 'Actual codec fixture failed'}
}finally{
 foreach($file in @($codecExe,$config)){if([IO.File]::Exists($file)){[IO.File]::Delete($file)}}
 [IO.Directory]::Delete($taskTemp,$false)
}