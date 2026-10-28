param([string]$ReportRoot = 'E:\_Haven\shared\REBIRTHTemp\VehicleSeatCycle')
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '..\..\Scripts\Vehicles\RebirthVehicleSeatCycle.cs'
Add-Type -TypeDefinition ((Get-Content -LiteralPath $source -Raw) + @'
public static class VehicleSeatCycleFixture {
    public static int Run() {
        int checks = 0;
        for (int current=0; current<6; current++) {
            for (int mask=0; mask<64; mask++) {
                for (int permission=0; permission<2; permission++) {
                    int result=RebirthVehicleSeatCycle.Next(6,current,s => (mask & (1 << s)) != 0,permission==1);
                    if (result==current || result>=6 || result < -1) throw new System.Exception("Invalid destination");
                    if (result>=0 && ((mask & (1 << result)) != 0 || (result==0 && permission==0)))
                        throw new System.Exception("Occupied or unauthorized destination");
                    // Walk every earlier candidate to verify deterministic cycle order.
                    bool found=false;
                    for (int offset=1; offset<6; offset++) {
                        int seat=(current+offset)%6;
                        if ((mask & (1 << seat)) != 0 || (seat==0 && permission==0)) continue;
                        if (result!=seat) throw new System.Exception("Skipped first eligible destination");
                        found=true; break;
                    }
                    if (!found && result!=-1) throw new System.Exception("Full vehicle changed seat");
                    checks++;
                }
            }
        }
        if (RebirthVehicleSeatCycle.Next(1,0,s=>false,true)!=-1
            || RebirthVehicleSeatCycle.Next(6,-1,s=>false,true)!=-1
            || RebirthVehicleSeatCycle.Next(6,6,s=>false,true)!=-1
            || RebirthVehicleSeatCycle.Next(6,0,null,true)!=-1)
            throw new System.Exception("Invalid membership accepted");
        return checks+4;
    }
}
'@)
$checks = [VehicleSeatCycleFixture]::Run()
New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null
$report = [ordered]@{ status='PASS'; checks=$checks; scope='Actual seat-cycle source: all 6 current seats, all 64 occupancy masks, both driver permissions, invalid membership'; limitations='Does not execute Unity transforms, native ownership changes, input dispatch or network transport'; sourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash }
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReportRoot 'result.json') -Encoding UTF8
$report | ConvertTo-Json
