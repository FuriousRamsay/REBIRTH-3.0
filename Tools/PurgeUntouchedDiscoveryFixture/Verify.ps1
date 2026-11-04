param()
$ErrorActionPreference='Stop'
$source=Get-Content "$PSScriptRoot/../../Scripts/Purge/Evidence/RebirthPoiNativeEvidence.cs" -Raw
$a=$source.IndexOf('private bool CanOmitUntouchedCandidate(')
$b=$source.IndexOf('internal enum ShutdownDrainResult',$a)
if($a -lt 0 -or $b -le $a){throw 'Production omission method missing'}
"using System;using System.Linq;partial class Probe { $($source.Substring($a,$b-$a)) }" | Set-Content "$PSScriptRoot/Source.g.cs"
$process=$source.Substring($source.IndexOf('private void ProcessCandidate('))
if($process.IndexOf('if(CanOmitUntouchedCandidate(c,store,snapshot))return;') -gt $process.IndexOf('var discovery=store.TryDiscover') -or -not $process.Contains('if(CanOmitUntouchedCandidate(c,store,snapshot))return;')){throw 'Omission must precede discovery publication'}
$shutdown=$source.Substring($source.IndexOf('internal ShutdownDrainResult PrepareShutdown()'))
if($shutdown.IndexOf('if(CanOmitUntouchedCandidate(c,store,store.Published)){complete=true;break;}') -gt $shutdown.IndexOf('AcknowledgedLatest(c,record.Observations)') -or -not $shutdown.Contains('if(CanOmitUntouchedCandidate(c,store,store.Published)){complete=true;break;}')){throw 'Shutdown still requires invented discovery'}
& dotnet run --project "$PSScriptRoot/Fixture.csproj" --no-restore
if($LASTEXITCODE){throw 'Untouched discovery fixture failed'}
