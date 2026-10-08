$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$a=$source.IndexOf('    public override void OnUpdateEntity()');$b=$source.IndexOf('    public override void InitLocalActivationCommands',$a)
if($a -lt 0 -or $b -le $a){throw 'actual foundation update gate missing'}
$body='class FoundationGateHarness:NativeGateCallbacks { private bool rebirthPreparedRestorationPending; public void Prepare(bool held){rebirthPreparedRestorationPending=held;}'+$source.Substring($a,$b-$a)+'}'
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualFoundationGate.cs'),$body)
$a=$source.IndexOf('    internal RebirthNpcRuntimeState CloneForProjection()');$b=$source.IndexOf('public readonly struct RebirthNpcTransactionResult',$a)
if($a -lt 0 -or $b -le $a){throw 'actual clone missing'}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualRuntimeClone.cs'),'public partial class RebirthNpcRuntimeState {'+$source.Substring($a,$b-$a))
$restore=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Persistence/RebirthNpcNativeReconstructionRestore.cs'))
$a=$restore.IndexOf('    private static void RestoreToolbelt');$b=$restore.IndexOf('    private static bool Preflight',$a)
if($a -lt 0 -or $b -le $a){throw 'actual hydration methods missing'}
$hydrate=$restore.Substring($a,$b-$a).Replace('private static void','internal static void')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualHydration.cs'),'using System;using System.IO;namespace HydrationDoubles { internal static class ActualHydration {'+$hydrate+'}}')
$gate=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcPreparedEventGateInstaller.cs'))
$a=$gate.IndexOf('    internal static bool Prefix');$b=$gate.IndexOf('    }',$a)+5
if($a -lt 0 -or $b -le $a){throw 'actual prepared minEffect prefix missing'}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualEventGate.cs'),'namespace EventGateDoubles { static class ActualEventGate {'+$gate.Substring($a,$b-$a)+'}}')
foreach($pair in @(@('RebirthNpcPreparedHandReconcileGate','ActualHandReconcileGate'),@('RebirthNpcPreparedHandEquipGate','ActualHandEquipGate'),@('RebirthNpcPreparedHandModeGate','ActualHandModeGate'),@('RebirthNpcPreparedHandSlotGate','ActualHandSlotGate'),@('RebirthNpcPreparedHeldQuestGate','ActualHeldQuestGate'))){$a=$gate.IndexOf('internal static class '+$pair[0]);$b=$gate.IndexOf('[HarmonyPatch',$a);if($b -lt 0){$b=$gate.Length};if($a -lt 0){throw 'actual hand guard absent'};$body=$gate.Substring($a,$b-$a).Replace($pair[0],$pair[1]);[IO.File]::WriteAllText((Join-Path $PSScriptRoot ($pair[1]+'.cs')),$body)}
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'NPC reconstruction fixture failed'}
