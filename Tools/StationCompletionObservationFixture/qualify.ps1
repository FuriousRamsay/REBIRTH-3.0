$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=[IO.File]::ReadAllText((Join-Path $root 'Tools/GearNativeQualification/reconnect/ItemStack.cs'))
function Extract([string]$signature){$s=$source.IndexOf($signature);if($s-lt 0){throw $signature};$b=$source.IndexOf('{',$s);$d=1;$e=$b+1;while($d-gt 0){if($source[$e]-eq '{'){$d++};if($source[$e]-eq '}'){$d--};$e++};return $source.Substring($s,$e-$s)}
$insert=(Extract 'public static int AddToItemStackArray(').Replace('AddToItemStackArray(','NativeInsert(')
$methods=@($insert,(Extract 'public bool CanStackWith('),(Extract 'public bool CanStack('))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeAlgorithms.cs'),"partial class ItemStack{`n"+($methods-join "`n")+"`n}")
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE-ne 0){throw 'Fixture failed'}