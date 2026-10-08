$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '..\GameBridge\gamebridge.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Runner parse failure'}
$assignment=$ast.Find({param($n)$n -is [System.Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -eq '$KvCommands'},$true)
Invoke-Expression $assignment.Extent.Text
foreach($name in @('Get-KvTimeout','Invoke-Kv')){$fn=$ast.Find({param($n)$n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true);Invoke-Expression $fn.Extent.Text}
function Invoke-Bridge($method,$endpoint,$query,$body,$timeout){$script:lastCall=@{Method=$method;Endpoint=$endpoint;Query=$query;Timeout=$timeout};[pscustomobject]@{Status=200;Json=[pscustomobject]@{ok=$true;inventory='mock-read-only-data'}}}
$count=0
foreach($file in @('backpack_extra_sections.json','library_study_activity.json','p2p_gear_custody.json','tutorial_reading_receipts.json')){
 $test=Get-Content (Join-Path $PSScriptRoot "..\GameBridge\tests\$file") -Raw|ConvertFrom-Json
 $steps=@($test.steps|Where-Object {$_.action.command -eq 'state'})
 if(-not $steps.Count){throw "Expected original state step missing: $file"}
 foreach($step in $steps){$q=@{};foreach($prop in $step.action.args.PSObject.Properties){$q[$prop.Name]=[string]$prop.Value};if(-not $KvCommands.ContainsKey('state')){throw 'Unsupported state'};$r=Invoke-Kv 'state' $q;if($lastCall.Method -ne 'GET' -or $lastCall.Endpoint -ne '/state' -or $lastCall.Query['Sections'] -ne $step.action.args.Sections -or $r.Status -ne 200 -or $r.Json.ok -ne $true){throw "Dispatch mismatch: $file"};$count++}
}
if($KvCommands.ContainsKey('unsupported-state-alias')){throw 'Unknown action accepted'}
"PASS $count original scenario state steps dispatched to GET/state with original sections; mocked transport only, no game/contact."
