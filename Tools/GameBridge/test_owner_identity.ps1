$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '../..'
$models = Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs')
$codec = Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Network/RebirthSurvivorNetworkCodec.cs')
$scope = Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs')
function Slice([string]$source,[string]$from,[string]$to) {
    $start=$source.IndexOf($from); $end=$source.IndexOf($to,$start)
    if($start -lt 0 -or $end -le $start){throw "Missing production section $from"}
    return $source.Substring($start,$end-$start)
}
$improvisation = Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Persistence/RebirthImprovisationProgressPersistence.cs')
$types = (Slice $models 'public static class RebirthSurvivorNetworkProtocol' 'public enum RebirthSurvivorCreationNetworkOperation') +
    (Slice $models 'public enum RebirthSurvivorOwnerCreationState' 'public sealed class RebirthSurvivorCreationNetworkRequest') +
    (Slice $models 'public sealed class RebirthSurvivorOwnerAttributeSnapshot' 'public static class RebirthSurvivorNetworkClone')
$helpers = (Slice $codec '    public static int EstimateString(' '    public static string ReadBoundedString(') +
    (Slice $codec '    private static int SevenBitEncodedIntLength(' '    public static int EstimateCreationRequest(') +
    (Slice $codec '    public static int EstimateOwnerState(' '    public static string ReadString(BinaryReader reader, int maxLength,') +
    (Slice $codec '    public static string ReadString(BinaryReader reader, int maxLength)' '    public static void WriteCreationResult(')
$ownerMethods = $codec.Substring($codec.IndexOf('    public static void WriteOwnerState('))
# ownerMethods already ends with the production codec's closing brace.
$code = "using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Globalization; using System.Xml.Linq;`n" +
    $types + $scope.Replace('using System;', '') +
    'public static class RebirthSurvivorDefinitionRegistry {public static string SemanticHash="test";}' +
    $improvisation.Substring($improvisation.IndexOf('public static class RebirthImprovisationProgressPersistence')) +
    'public static class OwnerCodec {' + $helpers + $ownerMethods + @'
public static class OwnerIdentityChecks {
 public static void Run(){
  var id=Guid.NewGuid();var other=Guid.NewGuid();
  if(!RebirthSurvivorRequestScope.Matches(id.ToString("N"),id.ToString("D").ToUpperInvariant()))throw new Exception("Equivalent IDs rejected");
  foreach(var bad in new[]{null,"","invalid",Guid.Empty.ToString(),other.ToString()})
   if(RebirthSurvivorRequestScope.Matches(bad,id.ToString())||RebirthSurvivorRequestScope.Matches(id.ToString(),bad))throw new Exception("Invalid or stale identity accepted");
  var legacy="legacy-"+new string('a',64);
  if(!RebirthSurvivorRequestScope.Matches(legacy,legacy))throw new Exception("Migrated identity rejected");
  foreach(var bad in new[]{"legacy-"+new string('b',64),"legacy-"+new string('a',63),"legacy-"+new string('z',64),legacy.ToUpperInvariant(),id.ToString(),null,""})
   if(RebirthSurvivorRequestScope.Matches(legacy,bad)||RebirthSurvivorRequestScope.Matches(bad,legacy))throw new Exception("Wrong migrated identity accepted");
  foreach(var creation in new[]{id.ToString("N"),legacy})
  foreach(bool populated in new[]{false,true}){
   var state=new RebirthSurvivorOwnerStateSnapshot{CreationId=creation,HasCharacter=populated,CharacterRevision=42,PhysicalBagSlots=78,SourceProfileName="Survivor"};
   if(populated){state.GearSlots.Add(new RebirthSurvivorOwnerGearSnapshot{SlotId="walkman",ItemId="rebirthGearWalkmanHeadphones"});state.TraitIds.Add("FastLearner");state.Skills.Add(new RebirthSurvivorOwnerSkillSnapshot{Id="medicine",Value=13,Progress=.5f});}
   if(state.Clone().CreationId!=state.CreationId)throw new Exception("Clone lost identity");
   using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
    OwnerCodec.WriteOwnerState(writer,state);
    if(OwnerCodec.EstimateOwnerState(state)<stream.Length)throw new Exception("Packet length underestimated");
    stream.Position=0;var decoded=OwnerCodec.ReadOwnerState(new BinaryReader(stream));
    if(decoded.CreationId!=state.CreationId||decoded.CharacterRevision!=42||decoded.PhysicalBagSlots!=78||decoded.SourceProfileName!="Survivor"||stream.Position!=stream.Length)throw new Exception("Owner packet roundtrip failed");
    if(populated&&(decoded.GearSlots[0].ItemId!=state.GearSlots[0].ItemId||decoded.Skills[0].Value!=13))throw new Exception("Owner fields shifted");
   }
  }
 }
}
'@
Add-Type -TypeDefinition $code
[OwnerIdentityChecks]::Run()
Write-Output 'PASS: production identity guard, owner clone, empty/populated codec roundtrips and length estimates. Native network/runtime untested.'
