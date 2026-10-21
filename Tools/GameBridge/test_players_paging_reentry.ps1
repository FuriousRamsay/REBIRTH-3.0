#requires -Version 7.0
$ErrorActionPreference='Stop'
if(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue){throw 'Game must be closed'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/Players/XUiC_RebirthPlayers.cs'))
$start=$source.IndexOf('    public override void OnOpen()');$open=$source.IndexOf('{',$start);$end=$open+1;$depth=1
while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
$method=$source.Substring($start,$end-$start)
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'players_paging_reentry_fixture.cs'))
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_ON_OPEN',$method) -IgnoreWarnings -WarningAction SilentlyContinue
[PlayersPagingFixture]::Run()
