$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot '../GameBridge/gamebridge.ps1'
$tokens=$null;$errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors | Out-String)}
$function=$ast.Find({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Get-StationTestValue'},$true)
if(!$function){throw 'Production assertion helper missing'}
Invoke-Expression $function.Extent.Text
$s='{"observationVersion":1,"station":"workbench","accessed":false,"queueCapacity":16,"tools":[{"name":"screwdriver","count":1,"slot":1}],"output":[]}'|ConvertFrom-Json
$a='{"station":"workbench","field":"queueCapacity"}'|ConvertFrom-Json
if((Get-StationTestValue $s $a) -ne 16){throw 'Capacity incorrect'}
$a='{"station":"workbench","field":"tools","item":"screwdriver","slot":1}'|ConvertFrom-Json
if((Get-StationTestValue $s $a) -ne 1){throw 'Matching slot incorrect'}
$a.slot=0;if((Get-StationTestValue $s $a) -ne 0){throw 'Wrong slot accepted'}
$a.field='output';if((Get-StationTestValue $s $a) -ne 0){throw 'Empty output incorrect'}
function MustReject($snapshot,$arg){$rejected=$false;try{$null=Get-StationTestValue $snapshot $arg}catch{$rejected=$true};if(!$rejected){throw 'Invalid evidence accepted'}}
$s.accessed=$true;MustReject $s $a
$s.accessed=$false;$a.field='fuel';MustReject $s $a
$a.field='output';$a.station='forge';MustReject $s $a
MustReject $null $a
'PASS actual helper: capacity, matching/nonmatching tool slot, empty output, open/missing/wrong-station/unavailable rejection'
