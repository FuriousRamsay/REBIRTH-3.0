# REBIRTH Vehicle Studio 7DTD Vanilla Reference Probe v1.7
# Windows PowerShell 5.1-compatible. READ-ONLY against 7DTD and REBIRTH.
# Produces an XML-only donor reference; DOES NOT capture characters, IK execution or meshes.
[CmdletBinding()]
param(
    [string]$GameDir = '',
    [string]$VehicleName = '',
    [string]$OutputRoot = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-GameDirectory {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (-not (Test-Path -LiteralPath $Explicit -PathType Container)) {
            throw "Specified 7DTD directory does not exist: $Explicit"
        }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }
    $dirs = New-Object 'System.Collections.Generic.List[string]'
    if (-not [string]::IsNullOrWhiteSpace($env:GAME_7DTD)) { $dirs.Add($env:GAME_7DTD) }
    foreach ($p in @(
        'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die',
        'C:\Program Files\Steam\steamapps\common\7 Days To Die',
        'D:\SteamLibrary\steamapps\common\7 Days To Die',
        'E:\SteamLibrary\steamapps\common\7 Days To Die',
        'D:\Steam\steamapps\common\7 Days To Die',
        'E:\Steam\steamapps\common\7 Days To Die'
    )) { $dirs.Add($p) }
    try {
        $steamReg = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
        if (-not [string]::IsNullOrWhiteSpace($steamReg)) {
            $dirs.Add((Join-Path $steamReg 'steamapps\common\7 Days To Die'))
        }
    } catch { }
    foreach ($d in $dirs) {
        if ([string]::IsNullOrWhiteSpace($d)) { continue }
        if (Test-Path -LiteralPath (Join-Path $d 'Data\Config\vehicles.xml') -PathType Leaf) {
            return (Resolve-Path -LiteralPath $d).Path
        }
    }
    throw '7DTD not found in common Steam locations. Rerun with -GameDir "FULL_PATH_TO_7_DAYS_TO_DIE".'
}
function Read-Properties {
    param([System.Xml.XmlNode]$Element)
    $items = New-Object 'System.Collections.Generic.List[object]'
    foreach ($p in @($Element.SelectNodes('./property[not(@class)]'))) {
        $name = $p.Attributes['name']
        if ($null -eq $name) { continue }
        $value = $p.Attributes['value']
        $items.Add([ordered]@{ key = $name.Value; value = $(if ($null -ne $value) { $value.Value } else { '' }) })
    }
    return $items.ToArray()
}
function Property-Value {
    param([object[]]$Pairs, [string]$Name)
    foreach ($p in $Pairs) { if ($p.key -ceq $Name) { return [string]$p.value } }
    return ''
}
function Save-Utf8Json {
    param([object]$Payload, [string]$Path)
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $json = ConvertTo-Json -InputObject $Payload -Depth 15
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, $utf8)
}

$GameDir = Find-GameDirectory -Explicit $GameDir
$xmlPath = Join-Path $GameDir 'Data\Config\vehicles.xml'
$sourceShaBefore = (Get-FileHash -LiteralPath $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
$xml = New-Object System.Xml.XmlDocument
$xml.XmlResolver = $null
$xml.Load($xmlPath)
if ($null -eq $xml.DocumentElement -or $xml.DocumentElement.Name -cne 'vehicles') {
    throw 'Unexpected XML root. Expected <vehicles> in the installed game file.'
}
$allVehicleNodes = @($xml.SelectNodes('/vehicles/vehicle[@name]'))
if ($allVehicleNodes.Count -eq 0) { throw 'No named <vehicle> definitions found in installed vehicles.xml.' }
$names = @($allVehicleNodes | ForEach-Object { $_.Attributes['name'].Value })

$vehicleNode = $null
if (-not [string]::IsNullOrWhiteSpace($VehicleName)) {
    $matches = @($allVehicleNodes | Where-Object { $_.Attributes['name'].Value -ceq $VehicleName })
    if ($matches.Count -ne 1) {
        throw ("Vehicle '$VehicleName' not found exactly once. Names: " + ($names -join ', '))
    }
    $vehicleNode = $matches[0]
} else {
    foreach ($preferred in @('vehicleTruck4x4', 'vehicle4x4Truck')) {
        $matches = @($allVehicleNodes | Where-Object { $_.Attributes['name'].Value -ceq $preferred })
        if ($matches.Count -eq 1) { $vehicleNode = $matches[0]; break }
    }
    if ($null -eq $vehicleNode) {
        $matches = @($allVehicleNodes | Where-Object { $_.Attributes['name'].Value -match '(?i)4x4' })
        if ($matches.Count -eq 1) { $vehicleNode = $matches[0] }
    }
    if ($null -eq $vehicleNode) {
        throw ("Could not uniquely identify the vanilla 4x4. Rerun with -VehicleName. Names: " + ($names -join ', '))
    }
}
$selectedName = [string]$vehicleNode.Attributes['name'].Value
$rootProps = @(Read-Properties -Element $vehicleNode)
$parts = New-Object 'System.Collections.Generic.List[object]'
$seats = New-Object 'System.Collections.Generic.List[object]'
$wheelCount = 0
$steeringCount = 0
foreach ($part in @($vehicleNode.SelectNodes('./property[@class]'))) {
    $cls = [string]$part.Attributes['class'].Value
    $props = @(Read-Properties -Element $part)
    $parts.Add([ordered]@{ className = $cls; properties = $props })
    if ($cls -match '^wheel\d+$') { $wheelCount++ }
    if ($cls -ceq 'handlebars') { $steeringCount++ }
    if ($cls -match '^seat(\d+)$') {
        $seatIndex = [int]$Matches[1]
        $seats.Add([ordered]@{
            index = $seatIndex
            pose = Property-Value -Pairs $props -Name 'pose'
            position = Property-Value -Pairs $props -Name 'position'
            rotation = Property-Value -Pairs $props -Name 'rotation'
            exit = Property-Value -Pairs $props -Name 'exit'
            mod = Property-Value -Pairs $props -Name 'mod'
            modT = Property-Value -Pairs $props -Name 'modT'
            properties = $props
        })
    }
}
$seatsSorted = @($seats.ToArray() | Sort-Object -Property index)
if (@($seatsSorted | Where-Object { $_.index -eq 0 }).Count -ne 1) {
    throw 'The chosen donor does not define exactly one driver seat0; no capture will be exported.'
}
$driver = @($seatsSorted | Where-Object { $_.index -eq 0 })[0]
if ([string]::IsNullOrWhiteSpace($driver.position)) {
    throw 'Driver seat0 has no position; cannot create a useful reference.'
}

$exe = Join-Path $GameDir '7DaysToDie.exe'
$fileVersion = 'unknown'
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    try { $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion } catch { }
}
$sourceShaAfter = (Get-FileHash -LiteralPath $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceShaBefore -cne $sourceShaAfter) { throw 'Installed vehicles.xml changed during the read; please retry.' }

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Join-Path $env:USERPROFILE 'Downloads') 'REBIRTH_7DTD_Vanilla_Reference_Reports'
}
$runId = [Guid]::NewGuid().ToString('N')
$outDir = Join-Path $OutputRoot $runId
if (Test-Path -LiteralPath $outDir) { throw 'Refusing to reuse report directory.' }
[void](New-Item -ItemType Directory -Force -Path $outDir)

# Compatible with RebirthVehicleReferenceImporter.FromJson(schemaVersion 1).
# status deliberately NOT 'ok': XML has NOT been bound to a selected running vehicle.
$reference = [ordered]@{
    schemaVersion = 1
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    gameVersion = 'exe-file-version:' + [string]$fileVersion
    xmlConfig = [ordered]@{
        name = $selectedName
        filePath = 'Data/Config/vehicles.xml'
        prefab = Property-Value -Pairs $rootProps -Name 'prefab'
        status = 'xml_only_runtime_unverified'
        rootProperties = $rootProps
        parts = @($parts.ToArray())
        seats = @($seatsSorted)
    }
}
$warnings = New-Object 'System.Collections.Generic.List[string]'
$warnings.Add('XML ONLY: no player bones, effective patched configuration, live prefab, or runtime animations were inspected.')
$warnings.Add('Seating pose IDs and hand IK target coordinates are references, not proof that a character fits the camper.')
if ([string]::IsNullOrWhiteSpace($driver.pose)) {
    $warnings.Add('Vanilla driver seat pose property absent/empty; do not invent an explicit pose ID.')
}
if ($steeringCount -eq 0) { $warnings.Add('No handlebars steering component found in this XML definition.') }
if ($wheelCount -lt 4) { $warnings.Add('Fewer than four wheel parts found; check installed XML and inheritance.') }
$result = [ordered]@{
    kind = 'REBIRTH_VANILLA_REFERENCE_PROBE_V1_7'
    status = 'PASS_XML_REFERENCE_ONLY'
    runId = $runId
    vehicleName = $selectedName
    sourceFile = $xmlPath
    sourceSha256 = $sourceShaAfter
    gameExeFileVersion = [string]$fileVersion
    seatsFound = [int]$seatsSorted.Count
    wheelsFound = [int]$wheelCount
    steeringBlocksFound = [int]$steeringCount
    allAvailableVehicleNames = $names
    changedGameFiles = $false
    installedGameDll = $false
    recordedCharacterBones = $false
    matchedRuntimeVehicle = $false
    warnings = @($warnings.ToArray())
}
$referencePath = Join-Path $outDir 'donor_xml_capture.json'
$resultPath = Join-Path $outDir 'result.json'
Save-Utf8Json -Payload $reference -Path $referencePath
Save-Utf8Json -Payload $result -Path $resultPath
$summary = @(
    'REBIRTH Vehicle Studio - Vanilla 4x4 donor XML probe v1.7',
    ('Game: ' + $GameDir),
    ('Selected exact vehicle ID: ' + $selectedName),
    ('vehicles.xml SHA-256: ' + $sourceShaAfter),
    ('Seats: ' + $seatsSorted.Count + ' | Wheels: ' + $wheelCount + ' | Steering blocks: ' + $steeringCount),
    ('Output: ' + $outDir),
    'STATUS: PASS_XML_REFERENCE_ONLY. No runtime character or rig capture was performed.'
) -join [Environment]::NewLine
[System.IO.File]::WriteAllText((Join-Path $outDir 'SUMMARY.txt'), $summary + [Environment]::NewLine, ([System.Text.UTF8Encoding]::new($false)))
$zipPath = Join-Path (Join-Path $env:USERPROFILE 'Downloads') ("REBIRTH_7DTD_Vanilla_Reference_Probe_Report_$runId.zip")
if (Test-Path -LiteralPath $zipPath) { throw 'Refusing to overwrite an existing report ZIP.' }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host $summary
foreach ($warning in $warnings) { Write-Warning $warning }
Write-Host "Upload this ZIP for calibration analysis: $zipPath"
