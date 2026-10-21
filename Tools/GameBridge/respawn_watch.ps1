# Retired: the in-game AutoRespawnTick already handles death/spawn.
# Separate watchers competed with batches and could follow unrelated game PIDs.
param([int]$Port = 8765, [int]$AfterSeconds = 6)
[Console]::Out.WriteLine('{"ok":true,"retired":true,"owner":"native AutoRespawnTick"}')
