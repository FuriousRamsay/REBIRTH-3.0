$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Xml.Linq
Add-Type -Path @((Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthMusicTransferState.cs'),(Join-Path $PSScriptRoot '../../Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'))
$state = New-Object RebirthMusicTransferState
$state.TransactionId = [guid]::NewGuid().ToString('N')
$state.CreationId = [guid]::NewGuid().ToString('N')
$state.Operation = 1
$state.ExpectedRevision = 19
$state.LibraryIndex = 3
$state.SourceIsBag = $true
$state.SourceIndex = 8
$state.ItemId = 'FuriousRamsayCassette01'
$state.ItemData = 'AQIDBA=='
$restored = $null
if (-not [RebirthMusicTransferState]::TryRead($state.ToXml(), [ref]$restored)) { throw 'Round trip failed' }
if ($restored.ToXml().ToString() -ne $state.ToXml().ToString()) { throw 'Round trip changed custody fields' }
$state.CreationId='legacy-'+('a'*64)
if (-not [RebirthMusicTransferState]::TryRead($state.ToXml(),[ref]$restored) -or $restored.CreationId -ne $state.CreationId) {throw 'Legacy creation roundtrip failed'}
$copy = $state.Clone()
$copy.ItemData = 'BQY='
if ($state.ItemData -ne 'AQIDBA==') { throw 'Clone altered original' }
if (-not [RebirthMusicTransferState]::TryRead($null, [ref]$restored) -or $null -ne $restored) { throw 'Legacy no-pending state rejected' }
foreach ($invalid in @(
    @('transactionId','bad'), @('creationId',[guid]::Empty.ToString()),
    @('operation','3'), @('revision','-1'), @('libraryIndex','24'),
    @('sourceIndex','-1'), @('sourceIsBag','maybe'), @('itemId',''), @('itemData','not base64')
)) {
    $xml = $state.ToXml()
    $xml.SetAttributeValue($invalid[0], $invalid[1])
    if ([RebirthMusicTransferState]::TryRead($xml, [ref]$restored) -or $null -ne $restored) { throw "Invalid $($invalid[0]) accepted" }
}
Write-Output 'PASS: durable transfer round trip, clone, legacy absence and 9 invalid records. No network/inventory exercised.'
