
$ErrorActionPreference='Stop'
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die'
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll"
$asm=$null
$script:types=@{'UnityEngine.Object'=[UnityEngine.Object];'UnityEngine.Texture'=[UnityEngine.Texture];'UnityEngine.Texture2D'=[UnityEngine.Texture2D];'UnityEngine.Material'=[UnityEngine.Material];'UnityEngine.MaterialPropertyBlock'=[UnityEngine.MaterialPropertyBlock];'UITexture'=[UITexture];'UIWidget'=[UIWidget];'UIRect'=[UIWidget];'UIDrawCall'=[UIDrawCall];'XUiC_MapArea'=[XUiC_MapArea];'XUiController'=[XUiController];'InputUtils'=[InputUtils];'Utils'=[Utils];'Audio.Manager'=[Audio.Manager];'System.Single'=[float];'System.Int32'=[int];'System.Boolean'=[bool];'System.String'=[string]}
$script:ops=@{};foreach($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')){$op=$field.GetValue($null);$script:ops[$op.Name]=$op}
function Resolve-Type($type){if($type.IsByReference){$base=Resolve-Type $type.ElementType;return $base.MakeByRefType()};return $script:types[$type.FullName]}
function Resolve-Member($member){
 $type=$script:types[$member.DeclaringType.FullName];if(!$type){return $member}
 if($member-is[Mono.Cecil.FieldReference]){$found=$type.GetField($member.Name,[Reflection.BindingFlags]'Public,NonPublic,Instance,Static');if($found){return $found};return $member}
 $parameters=@();foreach($p in $member.Parameters){$t=Resolve-Type $p.ParameterType;if(!$t){return $member};$parameters+=$t}
 if($member.Name-eq'.ctor'){$found=$type.GetConstructor([type[]]$parameters)}else{$found=$type.GetMethod($member.Name,[Reflection.BindingFlags]'Public,NonPublic,Instance,Static',$null,[type[]]$parameters,$null)}
 if($found){return $found};return $member
}
function Emit-Native($typeName,$methodName,[type]$result,[type[]]$signature,[type]$delegate,[bool]$transpile,[bool]$zoomOnly){
 $type=$asm.MainModule.Types|Where-Object FullName -eq $typeName;$method=$type.Methods|Where-Object Name -eq $methodName
 if(@($method).Count-ne1){throw "Ambiguous native method: $typeName.$methodName"}
 $dynamic=[Reflection.Emit.DynamicMethod]::new("Native_${typeName}_${methodName}",$result,$signature,[NativeBridge],$true);$il=$dynamic.GetILGenerator()
 foreach($variable in $method.Body.Variables){$local=Resolve-Type $variable.VariableType;if(!$local){$local=[object]};$il.DeclareLocal($local)|Out-Null}
 $labels=@{};foreach($instruction in $method.Body.Instructions){$labels[$instruction.Offset]=$il.DefineLabel()}
 $code=[Collections.Generic.List[HarmonyLib.CodeInstruction]]::new();$start=$null;$end=$null
 foreach($instruction in $method.Body.Instructions){
  $operand=$instruction.Operand
  if($operand-is[Mono.Cecil.MemberReference]){$operand=Resolve-Member $operand}
  elseif($operand-is[Mono.Cecil.Cil.Instruction]){$operand=$labels[$operand.Offset]}
  elseif($operand-is[Mono.Cecil.Cil.VariableDefinition]){$operand=[int]$operand.Index}
  $item=[HarmonyLib.CodeInstruction]::new($script:ops[$instruction.OpCode.Name],$operand);$item.labels.Add($labels[$instruction.Offset]);$code.Add($item)
  if($zoomOnly-and$instruction.Offset-eq0x280){$start=$item}
  if($zoomOnly-and$instruction.Offset-eq0x2eb){$end=$item}
 }
 if($transpile){$rewriter=[Checks].Assembly.GetType($(if($NegativeZoomControl){'RebirthMapOverviewZoomBaseline'}else{'RebirthMapOverviewZoom'})).GetMethod('Transpiler',[Reflection.BindingFlags]'NonPublic,Static');$nativeName=[XUiC_MapArea].GetMethod($methodName);$argsList=[object[]]::new($(if($NegativeZoomControl){1}else{2}));$argsList[0]=$code;if(!$NegativeZoomControl){$argsList[1]=$nativeName};$code=@($rewriter.Invoke($null,$argsList))}
 $emitting=!$zoomOnly;$written=0
 foreach($item in $code){
  if($zoomOnly-and[object]::ReferenceEquals($item,$start)){$emitting=$true}
  if($zoomOnly-and[object]::ReferenceEquals($item,$end)){break}
  if(!$emitting){continue};$written++
  foreach($label in $item.labels){$il.MarkLabel($label)}
  $op=$item.opcode;$operand=$item.operand
  if($null-eq$operand){$il.Emit($op)}
  elseif($operand-is[Reflection.Emit.Label]){if($op.Name.EndsWith('.s')){$op=$script:ops[$op.Name.Substring(0,$op.Name.Length-2)]};$il.Emit($op,$operand)}
  elseif($operand-is[Reflection.FieldInfo]){$il.Emit($op,$operand)}
  elseif($operand-is[Reflection.ConstructorInfo]){$il.Emit($op,$operand)}
  elseif($operand-is[Reflection.MethodInfo]){$il.Emit($op,$operand)}
  elseif($operand-is[string]){$il.Emit($op,$operand)}
  elseif($operand-is[single]){$il.Emit($op,[single]$operand)}
  elseif($operand-is[int]-or$operand-is[sbyte]){if($op.Name-eq'ldc.i4.s'){$op=[Reflection.Emit.OpCodes]::Ldc_I4};$il.Emit($op,[int]$operand)}
  else {throw "Unmapped actual native operand $methodName $op $operand"}
 }
 if($zoomOnly){if(!$start-or!$end-or$written-lt20){throw 'Native zoom block boundary changed'};$il.Emit([Reflection.Emit.OpCodes]::Ret)}
 return $dynamic.CreateDelegate($delegate)
}try {
 $asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$game/7DaysToDie_Data/Managed/NGUI.dll")
 [NativeBridge]::TextureSetter=Emit-Native 'UITexture' 'set_mainTexture' ([void]) @([UITexture],[UnityEngine.Texture]) ([Action[UITexture,UnityEngine.Texture]]) $false $false
 [NativeBridge]::DrawSetter=Emit-Native 'UIDrawCall' 'set_mainTexture' ([void]) @([UIDrawCall],[UnityEngine.Texture]) ([Action[UIDrawCall,UnityEngine.Texture]]) $false $false
 $asm.Dispose();$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll")
 [NativeBridge]::Scroll=Emit-Native 'XUiC_MapArea' 'onMapScrolled' ([void]) @([XUiC_MapArea],[XUiController],[float]) ([Action[XUiC_MapArea,XUiController,float]]) $false $false
 [Checks]::Run()
 [WheelCases]::Run()
} finally {if($asm){$asm.Dispose()}}

