$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$native=Join-Path $root '../../7DaysToDie_Data/Managed/Assembly-CSharp.dll'
if((Get-FileHash $native -Algorithm SHA256).Hash -ne '086755F825EEE6ABD4A13789F2F428D9043D36C8C54EBB3B38A9518B4A5146FC'){throw 'Native version changed; re-export and review the coordinate contract'}
$source=Get-Content (Join-Path $root '_Documentation/PlayerReports_20261029/finite-native-sectorfile.cs.txt') -Raw
$start=$source.IndexOf('public virtual long GetOffsetFromXz(')
if($start -lt 0){throw 'Native method export missing'}
$brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
if($depth -ne 0){throw 'Native method export incomplete'}
$method=$source.Substring($start,$end-$start).Replace('public virtual long','public static long')
Add-Type -Path (Join-Path $root 'Scripts/Survivor/Persistence/RebirthStationRegionPayload.cs')
Add-Type -TypeDefinition ("public static class NativeRegionCoordinates {"+$method+"}")
$checks=0
foreach($coordinate in (@([int]::MinValue)+@(-4096..4096)+@([int]::MaxValue))){
    foreach($v in $coordinate){
        $index=[RebirthStationRegionPayload]::NativeLocalIndex($v)
        if($index -lt 0 -or $index -gt 31 -or 4*$index -ne [NativeRegionCoordinates]::GetOffsetFromXz($v,0) -or 128*$index -ne [NativeRegionCoordinates]::GetOffsetFromXz(0,$v)){throw "Native index mismatch $v"}
        $checks++
    }
}
# The failing live barrel is chunk (-15,38): native entry (16,6), not (17,6).
$data=New-Object byte[] 16384
$data[0]=55;$data[1]=114;$data[2]=103;$data[3]=1
$offset=4096+[NativeRegionCoordinates]::GetOffsetFromXz(-15,38)
$data[$offset]=3;$data[$offset+3]=1;$data[12288]=9
$data[12304]=116;$data[12305]=116;$data[12306]=99;$data[12308]=47;$data[12312]=99
$stream=New-Object IO.MemoryStream(,$data)
try{
    $payload=$null
    if(![RebirthStationRegionPayload]::TryRead($stream,[RebirthStationRegionPayload]::NativeLocalIndex(-15),[RebirthStationRegionPayload]::NativeLocalIndex(38),[ref]$payload) -or $payload[8] -ne 99){throw 'Native negative-coordinate entry not read'}
    if([RebirthStationRegionPayload]::TryRead($stream,(-15 -band 31),(38 -band 31),[ref]$payload)){throw 'Old wrong entry unexpectedly accepted'}
}finally{$stream.Dispose()}
"PASS $checks coordinates against exported native method, both axes; exact negative-coordinate region entry and old-index refusal"
