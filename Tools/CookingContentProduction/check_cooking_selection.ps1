$ErrorActionPreference='Stop'
$game=Resolve-Path (Join-Path $PSScriptRoot '../../../..')
foreach($dll in @('UnityEngine.CoreModule.dll','Assembly-CSharp.dll','LogLibrary.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $game "7DaysToDie_Data/Managed/$dll"))}
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../../RebirthUtils.dll'))
function Check($c,$m){if(-not $c){throw $m}}
function Bare($t){[Runtime.Serialization.FormatterServices]::GetUninitializedObject($t)}
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$ws=Bare ([XUiC_RebirthCookingWorkspace]);$type=[XUiC_RebirthCookingWorkspace]
function Field($name,$value){$type.GetField($name,$flags).SetValue($ws,$value)}
$classes=New-Object 'ItemClass[]' 20004
foreach($id in 20000..20003){$classes[$id]=Bare ([ItemClass]);[ItemClass].BaseType.GetField('pName',[Reflection.BindingFlags]'Instance,NonPublic,Public').SetValue($classes[$id],"test$id")}
[ItemClass].BaseType.GetField('pName',[Reflection.BindingFlags]'Instance,NonPublic,Public').SetValue($classes[20000],'foodMeatStew')
[ItemClass].GetField('list').SetValue($null,$classes)
function Stack($id,$count){$v=Bare ([ItemValue]);$v.type=$id;return New-Object ItemStack $v,$count}
$recipe=New-Object Recipe;$recipe.itemValueType=20000;$recipe.count=1
$recipe.ingredients.Add((Stack 20001 2));$recipe.ingredients.Add((Stack 20002 1))
$ghosts=New-Object 'ItemStack[]' 12;$ghosts[0]=Stack 20001 2;$ghosts[1]=Stack 20002 1
$slots=New-Object 'XUiC_RebirthCookingSlot[]' 12
foreach($i in 0..11){$slots[$i]=Bare ([XUiC_RebirthCookingSlot]);[XUiC_ItemStack].GetField('itemStack').SetValue($slots[$i],[ItemStack]::Empty.Clone())}
[XUiC_ItemStack].GetField('itemStack').SetValue($slots[0],(Stack 20001 2));[XUiC_ItemStack].GetField('itemStack').SetValue($slots[1],(Stack 20002 1))
Field 'slots' $slots;Field 'ghosts' $ghosts;Field 'selected' $recipe
Field 'resolvedAmounts' (New-Object 'System.Collections.Generic.Dictionary[int,int]')
$resolve=$type.GetMethod('Resolve',$flags);$matches=$type.GetMethod('Matches',$flags)
$inputs=New-Object 'System.Collections.Generic.List[ItemStack]';$inputs.Add([XUiC_ItemStack].GetField('itemStack').GetValue($slots[0]));$inputs.Add([XUiC_ItemStack].GetField('itemStack').GetValue($slots[1]))
Field 'batch' 1
Check ([object]::ReferenceEquals($recipe,$resolve.Invoke($ws,@()))) 'Selected recipe changed with one loaded serving'
Check ($matches.Invoke($ws,@($recipe.PSObject.BaseObject,$inputs.PSObject.BaseObject,$true,0))) 'One serving was rejected'
Field 'batch' 2
Check ([object]::ReferenceEquals($recipe,$resolve.Invoke($ws,@()))) 'Increasing servings changed the selected dish'
Check (-not $matches.Invoke($ws,@($recipe.PSObject.BaseObject,$inputs.PSObject.BaseObject,$true,0))) 'Insufficient grid was accepted for two servings'
$inputs[0].count=4;$inputs[1].count=2
Check ($matches.Invoke($ws,@($recipe.PSObject.BaseObject,$inputs.PSObject.BaseObject,$true,0))) 'Complete two-serving grid was rejected'
$inputs[1].count=1
Check (-not $matches.Invoke($ws,@($recipe.PSObject.BaseObject,$inputs.PSObject.BaseObject,$true,0))) 'A missing second serving of one role was accepted'
Write-Output 'Batch regression passed: selection stays fixed; every required quantity must cover the requested servings.'
