#requires -Version 7.0
$ErrorActionPreference='Stop'
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperGate=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Crafting/UI/RebirthStationPaidCompletionCallsite.cs'))
$camperExpectedMvid=[regex]::Match($camperGate,'NativeMvid="([^"]+)"').Groups[1].Value
$camperExpectedBody=[regex]::Match($camperGate,'NativeBody="([^"]+)"').Groups[1].Value
if(!$camperExpectedMvid -or !$camperExpectedBody){throw 'Native completion gate constants missing.'}
$camperNativePath=[IO.Path]::GetFullPath((Join-Path $camperRoot '../../7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
$camperStream=[IO.File]::OpenRead($camperNativePath)
try {
 $camperPE=[System.Reflection.PortableExecutable.PEReader]::new($camperStream)
 $camperMetadata=[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($camperPE)
 $camperMvid=$camperMetadata.GetGuid($camperMetadata.GetModuleDefinition().Mvid).ToString('D')
 if($camperMvid -ne $camperExpectedMvid){throw 'Installed game module differs from completion gate.'}
 $camperMatches=0
 foreach($camperTypeHandle in $camperMetadata.TypeDefinitions){
  $camperType=$camperMetadata.GetTypeDefinition($camperTypeHandle)
  if($camperMetadata.GetString($camperType.Name) -ne 'TileEntityWorkstation'){continue}
  foreach($camperMethodHandle in $camperType.GetMethods()){
   $camperMethod=$camperMetadata.GetMethodDefinition($camperMethodHandle)
   if($camperMetadata.GetString($camperMethod.Name) -ne 'HandleRecipeQueue'){continue}
   $camperMatches++
   $camperBody=[System.Reflection.Metadata.PEReaderExtensions]::GetMethodBody($camperPE,$camperMethod.RelativeVirtualAddress)
   $camperSHA=[Security.Cryptography.SHA256]::Create()
   try {$camperDigest=[BitConverter]::ToString($camperSHA.ComputeHash($camperBody.GetILBytes())).Replace('-','')}
   finally {$camperSHA.Dispose()}
   if($camperDigest -ne $camperExpectedBody){throw 'Installed native completion body differs from gate.'}
  }
 }
 if($camperMatches -ne 1){throw 'Expected exactly one installed native completion method.'}
 Write-Host 'PASS installed native completion MVID and raw IL match the production gate. Harmony transformation and live execution remain unverified.'
} finally {
 if($camperPE){$camperPE.Dispose()}
 $camperStream.Dispose()
}
