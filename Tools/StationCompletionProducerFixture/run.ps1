$ErrorActionPreference='Stop'
$taskSource=[IO.File]::ReadAllText("$PSScriptRoot/../StationPaidCompletionIndependentFixture/native/TileEntityWorkstation.cs")
$taskStart=$taskSource.IndexOf('public void HandleRecipeQueue(float _timePassed)');if($taskStart -lt 0){throw 'Native method absent'}
$taskOpen=$taskSource.IndexOf('{',$taskStart);$taskDepth=1;$taskEnd=$taskOpen+1
while($taskEnd -lt $taskSource.Length -and $taskDepth -gt 0){if($taskSource[$taskEnd] -eq '{'){$taskDepth++};if($taskSource[$taskEnd] -eq '}'){$taskDepth--};$taskEnd++}
if($taskDepth -ne 0){throw 'Native method boundary invalid'}
$taskBody=$taskSource.Substring($taskStart,$taskEnd-$taskStart)
$taskInsert='ItemStack.AddToItemStackArray(output, new ItemStack(itemValue, recipeQueueItem.Recipe.count))'
$taskComplete='AddCraftComplete(recipeQueueItem.StartingEntityId'
if(($taskBody.Split([string[]]@($taskInsert),[StringSplitOptions]::None).Length-1) -ne 1 -or ($taskBody.Split([string[]]@($taskComplete),[StringSplitOptions]::None).Length-1) -ne 1){throw 'Native sites changed'}
$taskBody=$taskBody.Replace('HandleRecipeQueue(','ActualNativeHandleRecipeQueue(').Replace($taskInsert,'RebirthStationPaidCompletionCallsite.Insert(output, new ItemStack(itemValue, recipeQueueItem.Recipe.count), -1, this)').Replace($taskComplete,'RebirthStationPaidCompletionCallsite.Complete(this, recipeQueueItem.StartingEntityId')
[IO.File]::WriteAllText("$PSScriptRoot/ActualNativeCompletionWhile.cs",'partial class TileEntityWorkstation{'+$taskBody+'}')
dotnet run --project "$PSScriptRoot/Fixture.csproj" -c Release
if($LASTEXITCODE -ne 0){throw 'Producer fixture failed'}
