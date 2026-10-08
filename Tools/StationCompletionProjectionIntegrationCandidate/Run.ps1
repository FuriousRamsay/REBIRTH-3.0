$ErrorActionPreference='Stop'
$base=Join-Path $PSScriptRoot '../StationCompletionProjectionFixture/ActualNativeSlices.cs'
if(!(Test-Path $base)){throw 'run parent native projection qualification first'}
$s=[IO.File]::ReadAllText((Resolve-Path $base))
$paired=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PairedWitness.method.txt'))
$adapters=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Adapters.txt'))
# using declarations already available from actual dependency source; remove extra leading usings.
$adapters=$adapters.Substring($adapters.IndexOf('class RebirthStablePlayerIdentity'))
$checks=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Checks.txt'))
$s=$s.Replace('Console.WriteLine("PASS "+checks+', $checks+'Console.WriteLine("PASS "+checks+')
$s=$s.Replace('public bool Current=true;public RebirthStationGridAdmission Admission;', 'internal ProjectionOwner Owner;internal RebirthStablePlayerIdentity Identity=new();public bool Current=true;public RebirthStationGridAdmission Admission;')
$s=$s.Replace('public XElement Node;public XElement Write()', 'public RebirthStationCompletionPublication Clone()=>new(){Node=new XElement(Node)};public XElement Node;public XElement Write()')
$s=$s.Replace('public class RebirthStationSnapshotEvidence{public const', 'public class RebirthStationSnapshotEvidence{public static Publication Proof;public static bool TryGetPublished(object request,out Publication proof){proof=Proof;return proof!=null;}public const')
$s+=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ObservationShell.txt'))+[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Observation.method.txt'))+'}'
$repo=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Repository.candidate.txt'))
$write=($repo.Split("`n")|Where-Object{$_ -like '*if(state.StationCompletionExpectationProjections.Count*'})-join "`n"
$read=($repo.Split("`n")|Where-Object{$_ -like '*if(!RebirthStationCompletionExpectationProjection.ReadAllStored(node,state.*' -or $_ -like '*foreach(var pair in completionProjections)*'})-join "`n"
$s+='static class CandidateHooks{internal static XElement Write(ProjectionState state){var node=new XElement("progression");'+$write+'return node;}internal static bool Read(XElement node,ProjectionState state,out string error){error=null;'+$read+'return true;}}'
$s+=$adapters+"`npartial class RebirthWorldCharacterRepository{`n"+$paired+"`n}"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'Composed.cs'),$s)
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE){throw 'composed failed'}
