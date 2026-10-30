$ErrorActionPreference='Stop';$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die'
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll"
$types=@{'ItemValue'=[ItemValue];'ItemActionAttack'=[ItemActionAttack];'UnityEngine.Mathf'=[UnityEngine.Mathf];'Utils'=[Utils];'System.Single'=[float];'System.Int32'=[int];'System.Boolean'=[bool]}
$ops=@{};foreach($f in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')){$op=$f.GetValue($null);$ops[$op.Name]=$op}
function Emit($method,[type]$result,[type[]]$signature,[type]$delegate){
 $dynamic=[Reflection.Emit.DynamicMethod]::new('Native_'+$method.Name,$result,$signature,[NativeBridge],$true);$il=$dynamic.GetILGenerator();foreach($v in $method.Body.Variables){$il.DeclareLocal($types[$v.VariableType.FullName])|Out-Null}
 $labels=@{};foreach($i in $method.Body.Instructions){$labels[$i.Offset]=$il.DefineLabel()}
 foreach($i in $method.Body.Instructions){$il.MarkLabel($labels[$i.Offset]);$op=$ops[$i.OpCode.Name];$operand=$i.Operand
 if($operand-is[Mono.Cecil.MethodReference]){$owner=$types[$operand.DeclaringType.FullName];$parameters=[type[]]@($operand.Parameters|ForEach-Object {$types[$_.ParameterType.FullName]});$resolved=$owner.GetMethod($operand.Name,[Reflection.BindingFlags]'Public,NonPublic,Static,Instance',$null,$parameters,$null);if(!$resolved){throw "Unknown native method $operand"};$il.Emit($op,$resolved)}
 elseif($operand-is[Mono.Cecil.Cil.Instruction]){if($op.Name.EndsWith('.s')){$op=$ops[$op.Name.Substring(0,$op.Name.Length-2)]};$il.Emit($op,$labels[$operand.Offset])}
 elseif($operand-is[Mono.Cecil.Cil.VariableDefinition]){$il.Emit($op,[int]$operand.Index)}
 elseif($null-eq$operand){$il.Emit($op)}elseif($operand-is[single]){$il.Emit($op,[single]$operand)}elseif($operand-is[int]-or$operand-is[sbyte]){$il.Emit($op,[int]$operand)}else{throw "Unmapped native operand $operand"}
 };return $dynamic.CreateDelegate($delegate)
}
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll")
try{
 $degrade=($asm.MainModule.Types|Where-Object Name -eq ItemActionAttack).Methods|Where-Object Name -eq DegradationModifier
 $percent=($asm.MainModule.Types|Where-Object Name -eq ItemValue).Methods|Where-Object Name -eq get_PercentUsesLeft
 [NativeBridge]::Degrade=Emit $degrade ([float]) @([float],[float]) ([Func[float,float,float]])
 [NativeBridge]::Percent=Emit $percent ([float]) @([ItemValue]) ([Func[ItemValue,float]])
 [Checks]::Run()
}finally{$asm.Dispose()}
