$ErrorActionPreference='Stop'
$s=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/QuickStack/RebirthLogisticsParcelReferenceJournal.cs'))
$start=$s.IndexOf('    private static string DomainPath(')
$end=$s.IndexOf('    private static int Owner(', $start)
$head='using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic; partial class Journal { const int MaximumBytes=32768; internal class Endpoint {internal Original Original;internal Death Death;internal Guid Incarnation;internal string Identity,Binding;} internal class Original {internal string Owner;internal Guid Operation;internal int Phase;} internal class Death {internal int OwnerEntity;}'
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'out') | Out-Null
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'out/JournalExtract.cs'),$head+[Environment]::NewLine+$s.Substring($start,$end-$start)+'}')
dotnet run --project (Join-Path $PSScriptRoot 'fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'fixture failed'}
