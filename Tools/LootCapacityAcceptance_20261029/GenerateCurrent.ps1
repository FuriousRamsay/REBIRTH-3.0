$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$prod='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0'
$out="$root/_Documentation/LootCapacityAcceptance_20261029/out"
$source=Get-Content "$prod/Scripts/BlockPickup/RebirthBlockPickupEmptyVariantGenerator.cs" -Raw
$source=[regex]::Replace($source,'(?m)^using [^;]+;\r?\n','')
$legacy=Get-Content "$prod/Scripts/BlockPickup/RebirthBlockPickupLegacyTargets.cs" -Raw
$legacy=[regex]::Replace($legacy,'(?m)^using [^;]+;\r?\n','')
$adapter=@'
using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;
public class XmlFile {public XDocument XmlDoc;}
public static class TEFeatureStorage {public const string PropLootList="LootList";}
public static class TEFeatureRebirthContainerName {public const string FeatureName="TEFeatureRebirthContainerName";}
public static class RebirthContainerSizeService {public const string PropertyContainerSize="RebirthContainerSize",PropertyLegacyContainerSize="ContainerSize",PropertyLootContainerSize="LootContainerSize";}
public static class RebirthBlockPickupClassifier {public const string PropertyVanillaCanPickup="RebirthVanillaCanPickup";}
public static class RebirthBlockPickupTargetResolver {public const string PropertyVanillaPickupTarget="RebirthVanillaPickupTarget";}
public static class RebirthLogSettings {public static bool BlockPickupLoggingEnabled=false;}
public static class Log {public static void Out(string s){}public static void Warning(string s){throw new Exception(s);}}
public static class Localization {public static Dictionary<string,string[]> Dictionary=new Dictionary<string,string[]>();public static bool Exists(string s)=>Dictionary.ContainsKey(s);}
'@
Add-Type -TypeDefinition ($adapter+$source+$legacy)
foreach($key in Get-Content "$out/localization-keys.txt"){[Localization]::Dictionary[$key]=[string[]]@($key)}
$xml=[XmlFile]::new();$xml.XmlDoc=[System.Xml.Linq.XDocument]::Load("$out/current-blocks.xml")
[RebirthBlockPickupEmptyVariantGenerator]::Prepare($xml)
$xml.XmlDoc.Save("$out/current-generated-blocks.xml")
@{sources=@('RebirthBlockPickupEmptyVariantGenerator.cs','RebirthBlockPickupLegacyTargets.cs')|ForEach-Object {@{path="Scripts/BlockPickup/$_";sha256=(Get-FileHash "$prod/Scripts/BlockPickup/$_").Hash}};inputSha256=(Get-FileHash "$out/current-blocks.xml").Hash;outputSha256=(Get-FileHash "$out/current-generated-blocks.xml").Hash;adapted='Feature constants, logging, XmlFile holder and localization-key existence; native game/label rendering not executed'}|ConvertTo-Json -Depth 5|Set-Content "$root/_Documentation/LootCapacityAcceptance_20261029/generation-source.json"
'PASS actual current generation source executed against offline patched XML; native/localization-label boundaries adapted; no game execution'
