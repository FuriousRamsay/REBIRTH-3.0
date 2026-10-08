$ErrorActionPreference='Stop'
$native=Get-Content "$PSScriptRoot/native/ItemActionAttack.cs" -Raw
$start=$native.IndexOf('public float GetDamageBlock(')
$end=$native.IndexOf('public DamageMultiplier GetDamageMultiplier()', $start)
$method=$native.Substring($start,$end-$start)
$adapter=@"
using System;
public enum PassiveEffects { BlockDamage }
public class ItemClass { public int ItemTags; }
public class ItemValue { public ItemClass ItemClass=new ItemClass(); public string Name; }
public class Material { public float MaxIncomingDamage=float.MaxValue; }
public class Block { public int Tags; public Material blockMaterial=new Material(); }
public struct BlockValue { public Block Block; }
public class EntityAlive { public int CurrentStanceTag, CurrentMovementTag; }
public static class Utils { public static float FastMin(float a,float b){return Math.Min(a,b);} }
public static class EffectManager { public static float CoverBonus, SafeBonus; public static ItemValue Seen; public static float GetValue(PassiveEffects p, ItemValue item,float baseline,EntityAlive owner,object unused,int tags){Seen=item; return baseline+(item.Name=="crowbar"?((tags&8)!=0?CoverBonus:0)+((tags&16)!=0?SafeBonus:0):0);} }
public class QualifiedNativeAttack {
public int tmpTag,PrimaryTag=1,SecondaryTag=2,MeleeTag=4;public float damageBlock=11;
"@
Add-Type -TypeDefinition ($adapter+$method+'}')
[xml]$xml=Get-Content "$PSScriptRoot/../../Config/_Survivor/starter_legacy_items.xml" -Raw
$node=$xml.SelectSingleNode("//item[@name='ItemsWeaponsCrowbar001_FR']")
[EffectManager]::CoverBonus=[float]$node.SelectSingleNode("effect_group/passive_effect[@name='BlockDamage' and @tags='rbCrowbarCover']").value
[EffectManager]::SafeBonus=[float]$node.SelectSingleNode("effect_group/passive_effect[@name='BlockDamage' and @tags='safes,hardenedSafe,buriedTreasure,timecharge']").value
$a=New-Object QualifiedNativeAttack;$i=New-Object ItemValue;$i.Name='crowbar';$b=New-Object BlockValue;$b.Block=New-Object Block
$count=0
function Check($ok,$name){if(-not $ok){throw "FAIL $name"};$script:count++;"PASS $name"}
$b.Block.Tags=8;Check ($a.GetDamageBlock($i,$b,$null,0) -eq 261) 'actual native method configured cover bonus'
Check ([object]::ReferenceEquals([EffectManager]::Seen,$i)) 'native query original source item'
$i.Name='other';Check ($a.GetDamageBlock($i,$b,$null,0) -eq 11) 'other source tool no bonus'
$i.Name='crowbar';$b.Block.Tags=0;Check ($a.GetDamageBlock($i,$b,$null,0) -eq 11) 'opened/ordinary untagged no bonus'
$b.Block.Tags=16;Check ($a.GetDamageBlock($i,$b,$null,0) -eq 1011) 'safe configured bonus'
$b.Block.Tags=8;$b.Block.blockMaterial.MaxIncomingDamage=5;Check ($a.GetDamageBlock($i,$b,$null,0) -eq 5) 'native material cap'
$b.Block.blockMaterial.MaxIncomingDamage=0;Check ($a.GetDamageBlock($i,$b,$null,0) -eq 0) 'zero cap stays zero'
"$count PASS extracted actual GetDamageBlock; effect system explicit adapter, no gameplay proof"

