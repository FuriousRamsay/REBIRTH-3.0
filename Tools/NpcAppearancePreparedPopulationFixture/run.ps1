$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthHumanNpcModelPipeline.cs'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualAppearance.cs'),$source.Substring(0,$source.IndexOf('public readonly struct RebirthHumanNpcModelPipelineResolution')),[Text.UTF8Encoding]::new($false))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$a=$source.IndexOf('public readonly struct RebirthNpcStableId');$b=$source.IndexOf('public sealed class RebirthNpcProfile',$a)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualStable.cs'),'using System; using System.Globalization;'+$source.Substring($a,$b-$a),[Text.UTF8Encoding]::new($false))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs'))
$parts=@(@('    internal static bool TryPrepareExistingPerson(', '    internal static bool TryPrepareSpawn('),@('    internal static bool TryConfirmConstructedSpawn(', '    // Journal the attempt before native publication.'),@('    internal static bool TryObservePublishedSpawn(', '    // Final-file intent witness only;'),@('    internal static bool HasSavedPendingSpawn(', '    public static bool TryComposeSpawn('),@('    private static void LoadNoLock()', '    private static void SaveNoLock()'),@('    private static void SaveNoLock()', '    public static void ResetForWorldChange()'),@('    public static bool TrySetDisplayName(', '    private static string GenerateName('))
$body='using System; using System.Collections.Generic; using System.IO; using System.Text; using System.Xml; using System.Globalization; partial class RebirthNpcWorldIntegrationService {'
foreach($pair in $parts){$a=$source.IndexOf($pair[0]);$b=$source.IndexOf($pair[1],$a);if($a -lt 0 -or $b -le $a){throw "missing source section $($pair[0])"};$body+=$source.Substring($a,$b-$a)}
$body+='}'
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualPreparedJournal.cs'),$body,[Text.UTF8Encoding]::new($false))
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Prepared population fixture failed'}