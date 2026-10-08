$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText("$PSScriptRoot/native/ItemStack.cs")
$methods=@('public bool IsEmpty()','public static int AddToItemStackArray(','public bool CanStackWith(','public bool CanStack(')
$parts=@()
foreach($signature in $methods){$start=$source.IndexOf($signature);if($start -lt 0){throw 'Installed method absent'};$open=$source.IndexOf('{',$start);$depth=1;$end=$open+1;while($end -lt $source.Length -and $depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++};if($depth -ne 0){throw 'Installed method boundary'};$parts+=$source.Substring($start,$end-$start)}
[IO.File]::WriteAllText("$PSScriptRoot/ActualNativeStackMethods.cs",'partial class ItemStack{'+($parts -join [Environment]::NewLine)+'}')
dotnet run --project "$PSScriptRoot/Fixture.csproj" -c Release
if($LASTEXITCODE -ne 0){throw 'Independent native merge/output-delta fixture failed'}
