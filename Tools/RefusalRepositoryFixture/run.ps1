$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../.."
function Slice($text,$from,$to){$a=$text.IndexOf($from);$b=$text.IndexOf($to,$a);if($a -lt 0 -or $b -le $a){throw 'Extraction boundary missing'};$text.Substring($a,$b-$a)}
$r=Get-Content -Raw "$root/Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs"
$m=Get-Content -Raw "$root/Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs"
$body=Slice $r '    private static XElement SerializeSupport(' '    private static bool TryDeserialize('
$body+=Slice $r '    private static bool TryDeserializeSupport(' '    private static void AddIssue('
$helpers=Slice $r '    private static string A(' '    private static string D('
$models=Slice $m 'public sealed class RebirthTraitSupportRuntimeState' 'public sealed class RebirthWorldCharacterRecord'
[IO.File]::WriteAllText("$PSScriptRoot/ActualExtracted.cs","using System;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;"+$models+(Slice (Get-Content -Raw "$root/Scripts/Survivor/Support/RebirthGearInventorySnapshot.cs") 'public sealed class RebirthGearInventorySnapshot' '    // Arrays must come')+"public static bool TryCapture(object bag,object belt,int owned,out RebirthGearInventorySnapshot s){s=ServerDoubles.Image;return ServerDoubles.Upload;}}public static partial class Program {"+ $body+(Slice $r '    internal static bool HasSavedUnpreparedGearBase(' '    internal static bool HasSavedGearSettlement(')+$helpers+"}")
[IO.File]::WriteAllText("$PSScriptRoot/ActualServerHelper.cs",(Get-Content -Raw "$root/Scripts/Survivor/Support/RebirthRemoteGearPreparationRefusal.cs"))
[IO.File]::WriteAllText("$PSScriptRoot/ActualRetiredFile.cs",(Get-Content -Raw "$root/Scripts/Survivor/Support/RebirthGearPreparationRefusalPlayerFileWitness.cs"))
[IO.File]::WriteAllText("$PSScriptRoot/ActualRetirementConfirm.cs",(Get-Content -Raw "$root/Scripts/Survivor/Support/RebirthRemoteGearRefusalRetirement.cs"))
dotnet run --project "$PSScriptRoot/Fixture.csproj"
if($LASTEXITCODE){throw 'Fixture failed'}
