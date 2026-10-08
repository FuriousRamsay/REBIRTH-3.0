$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$managed=(Resolve-Path (Join-Path $root '../../7DaysToDie_Data/Managed')).Path
$temp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-inflate-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
try {
 $exe=Join-Path $temp 'check.exe'
 $compression=Join-Path $temp 'Noemax.GZip.dll'
 [IO.File]::Copy((Join-Path $managed 'Noemax.GZip.dll'),$compression)
 & 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' /nologo /target:exe "/out:$exe" '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll' '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll' "/r:$compression" (Join-Path $root 'Scripts/Survivor/Persistence/RebirthStationRegionPayload.cs') (Join-Path $root 'Scripts/Survivor/Persistence/RebirthStationChunkInflate.cs') (Join-Path $PSScriptRoot 'test_station_chunk_inflate_fixture.cs')
 if($LASTEXITCODE -ne 0){throw 'Fixture compilation failed'}
 & $exe
 if($LASTEXITCODE -ne 0){throw 'Fixture failed'}
} finally {
 foreach($name in @('check.exe','Noemax.GZip.dll')){ $file=Join-Path $temp $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file} }
 [IO.Directory]::Delete($temp)
}