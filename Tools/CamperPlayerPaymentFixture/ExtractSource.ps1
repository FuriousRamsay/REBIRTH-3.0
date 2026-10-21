param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
function Region([string]$source,[string]$start,[string]$end) {
  $a=$source.IndexOf($start,[StringComparison]::Ordinal)
  if($a -lt 0 -or $source.IndexOf($start,$a+1,[StringComparison]::Ordinal) -ge 0){throw "Unique source boundary required: $start"}
  $b=$source.IndexOf($end,$a,[StringComparison]::Ordinal)
  if($b -le $a){throw "Source boundary changed: $end"}
  return $source.Substring($a,$b-$a)
}
$admission=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Crafting/UI/RebirthStationGridAdmission.cs'))
$offline=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthStationOfflineClaims.cs'))
$adapter=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationPlayerPayment.cs'))
$debit=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationPlayerDebitPayload.cs'))
$phase=Region $admission '    public bool TryMarkPublicationAttempted(' '    public static bool TryCreate(Guid job,Guid creation'
$branch=Region $admission '        if(schema=="12"||schema=="13"||schema=="14")' '        if(schema=="7")'
$sharing=Region $admission '    public bool SharesStation(' '    public XElement Write()'
$binding=Region $admission '    internal XElement WritePlayerPaymentBinding()' '    public bool IsPerBatch'
$operation=Region $adapter '    internal static Guid Operation(' '    private static bool[] Locks('
$compat=Region $offline '    public static bool Compatible(' '    public static bool CheckDocument('
$payloadRead=Region $debit '    private static bool Valid(' '    internal static bool TryApply('
$body=@"
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Xml;using System.Xml.Linq;using System.Security.Cryptography;using System.Text;
public partial class RebirthStationGridAdmission {
$phase
$sharing
$binding
private static bool TryReadCore(XElement node,IList<Recipe> definitions,bool requireDefinition,out RebirthStationGridAdmission admission,out Recipe queueImage,out ItemStack[] beforeImage,out ItemStack[] afterImage) {
admission=null;queueImage=null;beforeImage=null;afterImage=null;string schema=(string)node?.Attribute("version");
$branch
return LegacyRead(node,out admission,out queueImage,out beforeImage,out afterImage);
}
}
internal static class RebirthCamperStationPlayerPayment {
$operation
}
public static class RebirthStationOfflineClaims {
$compat
}
internal static class ActualDebitPayloadAdmission {
private const int Maximum=4*1024*1024;
$payloadRead
internal static bool Read(byte[] bytes,RebirthStablePlayerIdentity identity,int actor,Guid operation,out RebirthStationGridAdmission admitted)=>TryRead(bytes,identity,actor,operation,out admitted);
}
"@
$path=[IO.Path]::GetFullPath($Output);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null
[IO.File]::WriteAllText($path,$body)
Write-Output 'Extracted current actor admission parser branch, forward phases, source binding, operation, station collision and OfflineClaims compatibility bodies; native legacy parent is an explicit fixture double.'
