$ErrorActionPreference = 'Stop'
Add-Type -Path 'Scripts/Survivor/UI/RebirthShotDamage.cs'
# Ten pellets carrying ten ammo damage each; weapon and mods remain separate.
$shot = [RebirthShotDamage]::new(2,12,15,10,10)
if ($shot.Base -ne 20 -or $shot.Ammo -ne 100 -or $shot.Mods -ne 30 -or $shot.Total -ne 150) { throw 'Shotgun breakdown failed' }
# A slug uses one projectile. A mod may change the projectile count.
foreach ($rays in @(1,10)) {
    foreach ($modifiedRays in @(1,10,12)) {
        $shot = [RebirthShotDamage]::new(2,12,15,$rays,$modifiedRays)
        if ([Math]::Abs($shot.Base + $shot.Ammo + $shot.Mods - $shot.Total) -gt .001) { throw 'Breakdown does not sum to total' }
        if ($shot.Total -ne 15*$modifiedRays) { throw 'Total uses wrong projectile count' }
    }
}
'PASS: production damage arithmetic, ten-pellet ammo contribution, slugs, and modded pellet counts.'
