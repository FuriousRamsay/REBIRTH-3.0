$ErrorActionPreference='Stop'
$applied=Get-Content "$PSScriptRoot/../../Scripts/Crafting/UI/RebirthStationCompletionCapture.cs" -Raw
$source=Get-Content "$PSScriptRoot/Capture.before-guard.cs.txt" -Raw
function ExtractMethod([string]$needle){$start=$source.IndexOf($needle);if($start -lt 0){throw 'method missing'};$open=$source.IndexOf('{',$start);$depth=1;$end=$open+1;while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++};$source.Substring($start,$end-$start)}
$matches=ExtractMethod '        internal bool MatchesReceipt()'
$claim=ExtractMethod '        internal bool TryClaim()'
$shell='using System;using System.Collections.Generic; class ActualClaim { internal Scope Original; internal CraftCompleteData receipt; private bool claimed; private bool claiming;'+$matches+$claim+'}'
$candidate='        internal bool TryClaim(){if(claimed||claiming)return false;claiming=true;try{if(!MatchesReceipt()||!Original.IsCurrent())return false;claimed=true;return true;}finally{claiming=false;}}'
$source=$applied
$appliedMatches=ExtractMethod '        internal bool MatchesReceipt()'
$appliedClaim=ExtractMethod '        internal bool TryClaim()'
$fixed='using System;using System.Collections.Generic; class FixedClaim { internal Scope Original; internal CraftCompleteData receipt; private bool claimed; private bool claiming;'+$appliedMatches+$appliedClaim+'}'
Set-Content "$PSScriptRoot/Extracted.cs" ($shell+$fixed.Replace('using System;using System.Collections.Generic;','')) -Encoding utf8
Add-Type -IgnoreWarnings -Path "$PSScriptRoot/Extracted.cs","$PSScriptRoot/Adapters.cs"
[ClaimChecks]::Run()

