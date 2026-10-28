$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSCommandPath
$source=Get-Content (Join-Path $project '../../Scripts/Survivor/UI/XUiC_RebirthMusicLibrary.cs') -Raw
$start=$source.IndexOf('    private void Refresh()')
$end=$source.IndexOf('    private void RefreshPlayback(', $start)
if($start-lt 0-or $end-le $start){throw 'Actual Refresh source anchors missing'}
New-Item -ItemType Directory -Path (Join-Path $project 'out') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $project 'out/ActualRefresh.cs'),"using System;`nusing System.Collections.Generic;`npartial class Audit {`n"+$source.Substring($start,$end-$start)+"`n}",[Text.UTF8Encoding]::new($false))
dotnet run --project (Join-Path $project 'audit.csproj')
if($LASTEXITCODE-ne 0){throw 'Music projection fixture failed'}