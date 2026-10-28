$ErrorActionPreference='Stop'
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceAdmission.cs') -Raw
$start=$source.IndexOf('    private List<Frame> Plan(')
$end=$source.IndexOf('    private bool BeginIdentity(', $start)
$currentStart=$source.IndexOf('    private bool Current()=>')
$currentEnd=$source.IndexOf('    private bool Eligible(', $currentStart)
$eligibleStart=$source.IndexOf('    private bool EligibleSource(')
$eligibleEnd=$source.IndexOf('    private List<Frame> Plan(', $eligibleStart)
if($start -lt 0 -or $end -lt 0 -or $currentStart -lt 0 -or $eligibleStart -lt 0){throw 'Production method markers changed; review extractor.'}
$methods=$source.Substring($start,$end-$start)+$source.Substring($currentStart,$currentEnd-$currentStart)+$source.Substring($eligibleStart,$eligibleEnd-$eligibleStart)
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot out)|Out-Null
('using System;using System.IO;using System.Linq;using System.Collections.Generic;partial class AdmissionFixture {'+$methods+'}')|Set-Content (Join-Path $PSScriptRoot out/AdmissionExtract.cs)
dotnet run --project (Join-Path $PSScriptRoot fixture.csproj) -p:NuGetAudit=false
exit $LASTEXITCODE
