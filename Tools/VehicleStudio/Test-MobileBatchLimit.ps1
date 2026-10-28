#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSourceRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperBatchCode=[IO.File]::ReadAllText((Join-Path $camperSourceRoot 'Scripts/Vehicles/Camper/RebirthCamperStationBatchLimit.cs'))
$camperBatchCode+=@'
public static class MobileBatchLimitFixture
{
    public static void Run()
    {
        if(RebirthCamperStationBatchLimit.Find(null)!=0)throw new Exception("Missing planner must yield zero.");
        for(int available=0;available<=10000;available++)
        {
            int calls=0;
            int actual=RebirthCamperStationBatchLimit.Find(request=>{calls++;return request<=available;});
            if(actual!=Math.Min(available,9999)||calls>15)
                throw new Exception("Incorrect maximum or unbounded work at "+available+": "+actual+" / "+calls);
        }
        Console.WriteLine("PASS actual mobile batch search: all limits 0..10000, zero-resource refusal, plan bound 9999, at most15 planner calls. Ingredient planner/UI not exercised.");
    }
}
'@
Add-Type -TypeDefinition $camperBatchCode
[MobileBatchLimitFixture]::Run()
