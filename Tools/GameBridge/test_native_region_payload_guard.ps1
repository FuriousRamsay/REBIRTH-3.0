$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthStationPublicationEvidence.cs')
$start=$source.IndexOf('    internal static void ValidateNativeWrite(')
$end=$source.IndexOf('    [HarmonyPrefix]',$start)
if($start -lt 0 -or $end -le $start){throw 'Production native region guard missing'}
$fixture=@'
using System;using System.IO;
static class RebirthStationRegionPayload{public const int MaximumPayloadBytes=255*4096-16;}
public static class NativeRegionPayloadGuardChecks{
// METHOD
static void Reject(int length,byte[] bytes){bool rejected=false;try{ValidateNativeWrite(length,bytes);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("unrepresentable native payload accepted");}
public static void Run(){
int maximum=RebirthStationRegionPayload.MaximumPayloadBytes;
ValidateNativeWrite(0,new byte[0]);ValidateNativeWrite(8,new byte[8]);ValidateNativeWrite(maximum,new byte[maximum]);
Reject(-1,new byte[0]);Reject(1,null);Reject(2,new byte[1]);Reject(maximum+1,new byte[maximum+1]);Reject(int.MaxValue,new byte[1]);
Console.WriteLine("PASS actual region payload guard boundaries; Harmony ordering, native save retry and disk preservation NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHOD',$source.Substring($start,$end-$start))
[NativeRegionPayloadGuardChecks]::Run()
