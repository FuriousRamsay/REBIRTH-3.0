$ErrorActionPreference='Stop'
. "$PSScriptRoot/medicine-startup-gate.ps1"
$a='[REBIRTH MedicinePractice][Startup] target=ItemActionEat.consume doseTranspiler=APPLIED reason=exact-native-dose-sites'
$b='[REBIRTH MedicinePractice][Startup] target=ItemActionEat.ExecuteInstantAction doseTranspiler=APPLIED reason=exact-native-dose-sites'
$cases=@(@{name='missing';log='';expected='FAIL'},@{name='consume-only';log=$a;expected='FAIL'},@{name='instant-only';log=$b;expected='FAIL'},@{name='both';log=$a+"`n"+$b;expected='PASS'},@{name='refused';log=$a+"`n"+$b.Replace('APPLIED','REFUSED');expected='FAIL'},@{name='applied-and-refused';log=$a+"`n"+$b+"`n"+$a.Replace('APPLIED','REFUSED');expected='FAIL'})
$results=@()
foreach($case in $cases){$r=Test-MedicinePracticeStartup $case.log;if($r.status -ne $case.expected){throw ('Unexpected '+$case.name)};$results+=@{case=$case.name;expected=$case.expected;actual=$r.status;pass=$true;targets=$r.targets}}
$results|ConvertTo-Json -Depth 7|Set-Content "$PSScriptRoot/MEDICINE_STARTUP_GATE_FIXTURES.json"
$results|ForEach-Object { $_.case+' '+$_.actual+' PASS' }
