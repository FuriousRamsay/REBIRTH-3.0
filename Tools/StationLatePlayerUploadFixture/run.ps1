$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Extract($path,$signature){$s=Get-Content (Join-Path $root $path) -Raw;$a=$s.IndexOf($signature);if($a-lt0){throw $signature};$b=$s.IndexOf('{',$a);$level=1;$i=$b+1;while($level-gt0){if($s[$i]-eq'{'){$level++};if($s[$i]-eq'}'){$level--};$i++};$s.Substring($a,$i-$a)}
$n='Tools/StationOpenPaidExecutionLeaseFixture/native/'
$save=Extract ($n+'PlayerDataFile.cs') 'public void Save(string _dir, string _playerId)'
$manager=Extract ($n+'GameManager.cs') 'public void SavePlayerData(ClientInfo _cInfo, PlayerDataFile _playerDataFile)'
$packet=Extract ($n+'NetPackagePlayerData.cs') 'public override void ProcessPackage(World _world, GameManager _callbacks)'
Set-Content (Join-Path $PSScriptRoot 'ActualNativeSave.cs') ("partial class PlayerDataFile { $save }`npartial class GameManager { $manager }`nclass NetPackagePlayerData:NetPackage {public PlayerDataFile playerDataFile; $packet }")
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj')
if($LASTEXITCODE-ne0){throw 'Fixture failed'}
