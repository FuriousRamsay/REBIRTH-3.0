$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path;$native=Join-Path $root 'Tools/HarvestNativeAudit/installed-3.2b10';$count=0
function Check($ok,$label){$script:count++;if(!$ok){throw $label}}
$hash=(Get-FileHash 'C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/7DaysToDie_Data/Managed/Assembly-CSharp.dll').Hash
Check ($hash -eq 'FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705') 'exact installed native capture'
$b=[IO.File]::ReadAllText((Join-Path $native 'Block.cs'));$start=$b.IndexOf('public virtual int OnBlockDamaged(');$end=$b.IndexOf('public virtual', $start+25);$body=$b.Substring($start,$end-$start)
Check ($body.Contains('if (!flag && num >= block2.MaxDamage)')) 'original damage crosses surviving stage threshold'
Check ($body.Contains('if (destroyedResult != DestroyedResult.Keep)')) 'Keep excludes completed transition'
Check ($body.Contains('_world.SetBlockRPC(_bvRef, BlockValue.Air);')) 'original native air transition'
Check ($body.Contains('_world.SetBlockRPC(_bvRef, downgradeBlock')) 'original downgrade preserves stages'
Check ($body.Contains('return block2.MaxDamage;')) 'numeric result alone also includes Keep'
$g=[IO.File]::ReadAllText((Join-Path $native 'GameManager.cs'));$a=$g.IndexOf('public void SetBlocksRPC(');$x=$g.Substring($a,600);Check ($x.IndexOf('ChangeBlocks(') -lt $x.IndexOf('NetPackageSetBlock package')) 'original local apply precedes broadcast'
Check ($g.Contains('BlockValue bvOld = chunkCluster.SetBlock(')) 'actual old-value native mutation boundary exists'
$p=[IO.File]::ReadAllText((Join-Path $native 'NetPackageSetBlock.cs'));Check ($p.Contains('ValidUserIdForSender(persistentPlayerId)') -and $p.Contains('ValidEntityIdForSender(localPlayerThatChanged)')) 'remote batch authenticates sender identity'
$hit=[IO.File]::ReadAllText((Join-Path $native 'ItemActionAttack.cs'));$a=$hit.IndexOf('public class AttackHitInfo');$x=$hit.Substring($a,$hit.IndexOf('public enum EnumAttackMode',$a)-$a);Check (!$x.Contains('ItemValue')) 'attack hit info does not authenticate original tool item'
[xml]$rules=[IO.File]::ReadAllText((Join-Path $root 'Config/_Survivor/theory_solo.xml'));Check ($rules.survivor_theory_solo.subjects.subject.Where({$_.skill_id -eq 'skill.mining'}).producer_family -eq 'world_continuous') 'accepted mining subject family'
Check (@($rules.survivor_theory_solo.policy.Attributes|Where-Object Name -match 'mining|logging').Count -eq0) 'no authored mining/logging Theory difficulty model present'
"PASS $count source ordering/signature checks; NOT executed native mining, authenticated item lease, durable replay or Theory gain qualification."