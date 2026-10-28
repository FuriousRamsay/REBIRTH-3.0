param([Parameter(Mandatory=$true)][string]$SourcePath)
$ErrorActionPreference='Stop'
$path=(Resolve-Path -LiteralPath $SourcePath).Path
$capturedHash=(Get-FileHash -LiteralPath $path).Hash
$text=Get-Content -LiteralPath $path -Raw
function Member([string]$source,[string]$marker){
 $start=$source.IndexOf($marker);if($start-lt0){throw "Missing actual member $marker"}
 $brace=$source.IndexOf('{',$start);$arrow=$source.IndexOf('=>',$start)
 if($arrow-ge0-and$arrow-lt$brace){return $source.Substring($start,$source.IndexOf(';',$arrow)-$start+1)}
 $depth=0
 for($i=$brace;$i-lt$source.Length;$i++){if($source[$i]-eq'{'){$depth++}elseif($source[$i]-eq'}'){$depth--;if(!$depth){return $source.Substring($start,$i-$start+1)}}}
 throw "Unclosed actual member $marker"
}
$actual=(@('private sealed class Baseline','private static XElement ReadLease(','private static byte[] Encode(','private static string PoiTarget(')|ForEach-Object{Member $text $_}) -join [Environment]::NewLine
$boundary=Get-Content (Join-Path $PSScriptRoot 'BoundariesAndChecks.cs.txt') -Raw
Add-Type -TypeDefinition ($boundary.Replace('/*ACTUAL_MEMBERS*/',$actual)) -CompilerOptions '/nowarn:0649'
[PoiLeaseCompatibilityChecks]::Run()
if((Get-FileHash -LiteralPath $path).Hash-ne$capturedHash){throw "Source changed during compatibility verification"}
"Source SHA256=$capturedHash"
