$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$repo=[IO.File]::ReadAllText("$root/Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs")
function Method($s,$signature){$start=$s.IndexOf($signature);if($start-lt0){throw "missing $signature"};$open=$s.IndexOf('{',$start);$depth=0;for($i=$open;$i-lt$s.Length;$i++){if($s[$i]-eq'{'){$depth++};if($s[$i]-eq'}'){$depth--;if($depth-eq0){return $s.Substring($start,$i-$start+1)}}};throw 'unbalanced'}
$loader=Method $repo '    private static bool TryLoadValidatedRecord('
$outer=Method $repo '    private static bool TryDeserialize(XDocument doc,'
[IO.File]::WriteAllText("$PSScriptRoot/ActualOuter.cs","using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Globalization;using System.Xml.Linq;`r`ninternal static partial class MixedFinalFileRepository {`r`n"+$loader+"`r`n"+$outer+"`r`n}")
$auth=[IO.File]::ReadAllText("$root/Tools/StationMixedPublicationIntegrationReview/Fixture/AuthorityAdapters.cs")
$auth=$auth.Replace('class RebirthWorldCharacterRecord{','partial class RebirthWorldCharacterRecord{').Replace('class RebirthWorldProgressionState{','partial class RebirthWorldProgressionState{').Replace('class Origin{','partial class Origin{')
[IO.File]::WriteAllText("$PSScriptRoot/AuthorityAdapters.cs",$auth)
