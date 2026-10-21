Add-Type -Path '../../7DaysToDie_Data/Managed/Noemax.GZip.dll'
Add-Type -Path 'Scripts/Survivor/Persistence/RebirthStationRegionPayload.cs','Scripts/Survivor/Persistence/RebirthStationChunkInflate.cs' -ReferencedAssemblies '../../7DaysToDie_Data/Managed/Noemax.GZip.dll'
$file=[IO.File]::OpenRead('C:/Users/Etienne/AppData/Roaming/7DaysToDie/Saves/West Xuyofu Territory/CodexTest/Region/r.0.2.7rg')
try{$payload=$null;$decoded=$null; $read=[RebirthStationRegionPayload]::TryRead($file,1,3,[ref]$payload); $decode=[RebirthStationChunkInflate]::TryDecode($payload,8388608,[ref]$decoded); @{read=$read;decode=$decode;bytes=$decoded.Length}|ConvertTo-Json}finally{$file.Dispose()}
