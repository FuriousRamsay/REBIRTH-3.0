param([string]$Python='C:/Users/Etienne/AppData/Local/Programs/Python/Python314/python.exe',
      [string]$Native='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/7DaysToDie_Data/Managed/Assembly-CSharp.dll')
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$workspace=Split-Path (Split-Path $taskRoot)
$report=Join-Path $workspace '_Documentation/PlayerReports_20261029/SUB1'
New-Item -ItemType Directory -Force $report | Out-Null
Push-Location $workspace
try {
    & $Python "$taskRoot/prepare_fixture.py"
    if($LASTEXITCODE -ne 0){throw 'fixture extraction failed'}
    & dotnet run --project "$taskRoot/Fixture/Fixture.csproj" -p:UseSharedCompilation=false -- "$taskRoot/inputs" 2>&1 | Tee-Object "$report/fixture.log"
    if($LASTEXITCODE -ne 0){throw 'positive fixture failed'}
    & $Python "$taskRoot/prepare_fixture.py" --baseline
    if($LASTEXITCODE -ne 0){throw 'baseline fixture extraction failed'}
    foreach($mode in @('combat','energy')) {
        $ErrorActionPreference='Continue' # Windows PowerShell wraps expected native stderr as error records.
        & dotnet run --project "$taskRoot/negative/Fixture.csproj" -p:UseSharedCompilation=false -- "$taskRoot/inputs" $mode 2>&1 | Tee-Object "$report/negative-$mode.log"
        $ErrorActionPreference='Stop'
        if($LASTEXITCODE -ne 1){throw "baseline negative $mode must fail with exit1"}
    }
    & dotnet run --project "$taskRoot/NativeExport/Export.csproj" -p:UseSharedCompilation=false -- audit $Native "$report/native-audit.json" 2>&1 | Tee-Object "$report/native-audit.log"
    if($LASTEXITCODE -ne 0){throw 'native call graph audit failed'}
    & dotnet build "$taskRoot/NativeAbi/Abi.csproj" -p:UseSharedCompilation=false --nologo -v:minimal 2>&1 | Tee-Object "$report/native-abi.log"
    if($LASTEXITCODE -ne 0){throw 'received boundary native ABI compilation failed'}
    Write-Output 'PASS:58 exact-source fixture checks; both baseline controls failed as expected;7 exact native call-graph checks; received boundary native ABI compilation.'
} finally {Pop-Location}
