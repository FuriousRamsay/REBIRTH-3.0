param([string]$Assembly='E:/_Haven/shared/REBIRTHStaged/LogFix_20261018/Build/RebirthUtils.dll')
$ErrorActionPreference='Stop'
Add-Type -Path 'C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/0_TFP_Harmony/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
try {
 $lifecycle=$a.MainModule.Types | Where-Object Name -eq RebirthNpcLifecycle
 $bootstrap=$lifecycle.Methods | Where-Object Name -eq EnsureRegistered
 $calls=@($bootstrap.Body.Instructions | Where-Object {$_.OpCode.Code -in 'Call','Callvirt'} | ForEach-Object {if($_.Operand){$_.Operand.ToString()}})
 if($calls -match 'RebirthNpcPersistenceTimelineService::EnsureAligned|RebirthNpcAggregatePersistenceStore::EnsureLoaded'){throw 'World persistence still called directly at InitMod'}
 'PASS process bootstrap defers timeline and aggregate loading'
 # Follow compiled initializer calls, including same-assembly static constructors.
 # Enabled world admission has a separately tested no-World early return.
 # Check that compiled control flow has a path to return before any REBIRTH
 # call, and that this prefix performs no save/retirement operation.
 $bundle=$a.MainModule.Types|Where-Object Name -eq RebirthNpcSaveQuarantineBundle
 $gateStores=@(($bundle.Methods|Where-Object Name -eq '.cctor').Body.Instructions|Where-Object {$_.OpCode.Code -eq 'Stsfld' -and $_.Operand.Name -eq 'AdmissionEnabled'})
 if($gateStores.Count -ne 1 -or $gateStores[0].Previous.OpCode.Code -ne 'Ldc_I4_1'){throw 'Reviewed whole-bundle admission activation missing'}
 $qualifier=$lifecycle.Methods|Where-Object Name -eq QualifyWholeBundleAdmission
 $pending=[Collections.Generic.Queue[object]]::new();$pending.Enqueue($qualifier.Body.Instructions[0])
 $prefixSeen=[Collections.Generic.HashSet[int]]::new();$earlyReturn=$false;$worldRead=$false
 while($pending.Count){
  $instruction=$pending.Dequeue();if(!$prefixSeen.Add($instruction.Offset)){continue}
  $code=$instruction.OpCode.Code.ToString()
  if($code -eq 'Ret'){$earlyReturn=$true;continue}
  if($code -in @('Call','Callvirt')){
   $reference=$instruction.Operand
   if($reference.DeclaringType.Name -like 'Rebirth*'){continue}
   if($reference.Name -notin @('get_Instance','get_World','op_Equality','op_Inequality')){throw "Unexpected process admission prefix call: $reference"}
   if($reference.Name -eq 'get_World'){$worldRead=$true}
  }
  if($code -eq 'Ldfld' -and $instruction.Operand.Name -eq 'World'){$worldRead=$true}
  if($instruction.OpCode.FlowControl.ToString() -in @('Branch','Cond_Branch')){
   if($instruction.Operand -is [Mono.Cecil.Cil.Instruction]){$pending.Enqueue($instruction.Operand)}
   if($instruction.OpCode.FlowControl.ToString() -eq 'Branch'){continue}
  }
  if($instruction.Next){$pending.Enqueue($instruction.Next)}
 }
 if(!$earlyReturn -or !$worldRead){throw 'Compiled no-World admission return is missing'}
 'PASS enabled admission has a compiled no-World bootstrap return before world effects'
 $forbidden='RebirthNpcPersistenceTimelineService::EnsureAligned|PersistenceStore::(?:EnsureLoaded|Load)\(|RebirthNpcLifecycleGameplayService::(?:EnsureInitialized|LoadLifecycleBound)\(|RebirthNpcWorldIntegrationService::(?:EnsureInitialized|LoadNoLock)\(|RebirthNpcPathBudgetCoordinator::EnsureInitialized|RebirthNpcReleaseCandidateValidationService::Run\('
 $queue=[Collections.Generic.Queue[object]]::new();$queue.Enqueue($bootstrap);$queue.Enqueue(($bundle.Methods|Where-Object Name -eq '.cctor'))
 $seen=[Collections.Generic.HashSet[string]]::new();$walked=[Collections.Generic.List[string]]::new()
 while($queue.Count -gt 0){
  $method=$queue.Dequeue();if(!$seen.Add($method.FullName)){continue};$walked.Add($method.FullName)
  if($seen.Count -gt 15000){throw 'Bootstrap graph exceeded bounded review limit'}
  if($method.FullName -match $forbidden){throw "World-owned operation reachable from process bootstrap: $($method.FullName)"}
  if($method.DeclaringType.Name -eq 'RebirthNpcLifecycle' -and $method.Name -eq 'QualifyWholeBundleAdmission'){continue}
  foreach($initializer in $method.DeclaringType.Methods|Where-Object Name -eq '.cctor'){if(!$seen.Contains($initializer.FullName)){$queue.Enqueue($initializer)}}
  if(!$method.HasBody){continue}
  foreach($instruction in $method.Body.Instructions){
   if($instruction.OpCode.Code -notin @('Call','Callvirt','Newobj')){continue}
   $reference=$instruction.Operand;if(!$reference -or !$reference.DeclaringType){continue}
   $scope=$reference.DeclaringType.Scope
   if($scope.Name -ne $a.MainModule.Name -and $scope.Name -ne $a.Name.Name){continue}
   $target=$reference.Resolve();if($target -and $target.Module -eq $a.MainModule){$queue.Enqueue($target)}
  }
 }
 "PASS compiled process initializer graph: $($walked.Count) methods; world loads/guards are not process initialization"
 $walked|ConvertTo-Json|Set-Content -LiteralPath ($Assembly+'.bootstrap-scope-graph.json')
 foreach($pair in @(@('RebirthNpcProgressionService','EnsureDefinitionsInitialized'),@('RebirthNpcAdvancedProgressionService','EnsureDefinitionsInitialized'),@('RebirthNpcEconomyLogisticsService','EnsureDefinitionsInitialized'),@('RebirthNpcScaleHardeningService','EnsureRegistered'),@('RebirthNpcProductionProfileCatalogue','EnsureInitialized'))){
  if(-not ($calls -match ($pair[0]+'::'+$pair[1]))){throw "Process catalogue registration missing: $($pair[0])"}
 }
 'PASS process catalogues and work sink registration retained'
 $world=$lifecycle.Methods | Where-Object Name -eq OnWorldAvailable
 if(-not ($world.Body.Instructions.Operand.ToString() -match 'RebirthNpcPersistenceCoordinator::EnsureLoaded')){
  if(-not (($world.Body.Instructions | ForEach-Object {$_.Operand}) -match 'RebirthNpcPersistenceCoordinator::EnsureLoaded')){throw 'World-start coordinated load missing'}
 }
 'PASS native world-start persistence load retained'
 $coordinator=$a.MainModule.Types | Where-Object Name -eq RebirthNpcPersistenceCoordinator
 $load=$coordinator.Methods | Where-Object Name -eq EnsureLoaded
 $loadCalls=@($load.Body.Instructions | ForEach-Object {if($_.Operand){$_.Operand.ToString()}})
 if(-not ($loadCalls -match 'RebirthNpcPersistenceTimelineService::EnsureAligned')){throw 'Timeline guard removed'}
 if(-not ($loadCalls -match 'RebirthNpcAggregatePersistenceStore::EnsureLoaded')){throw 'Aggregate load removed'}
 'PASS coordinated timeline guard and aggregate load retained'
 $timeline=$a.MainModule.Types | Where-Object Name -eq RebirthNpcPersistenceTimelineService
 $aligned=$timeline.Methods | Where-Object Name -eq EnsureAligned
 if(-not (($aligned.Body.Instructions | ForEach-Object {$_.Operand}) -match 'RequireTimelineScope')){throw 'Native custody guard removed'}
 'PASS authoritative native scope guard retained'
 $gameplay=$a.MainModule.Types|Where-Object Name -eq RebirthNpcLifecycleGameplayService
 $init=$gameplay.Methods|Where-Object Name -eq EnsureInitialized
 if(-not (($init.Body.Instructions|ForEach-Object {$_.Operand}) -match 'RequireLifecycleCurrent')){throw 'Lifecycle gameplay native guard removed'}
 'PASS lifecycle gameplay still requires its authoritative native world'
} finally {$a.Dispose()}

