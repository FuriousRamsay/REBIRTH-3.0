$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../Scripts/Survivor/Condition/RebirthStressState.cs')
function Check($ok,$why){if(-not $ok){throw $why}}
function Tick($s,$seconds,$threat=$false,$dark=$false,$night=$false,$sheltered=$false,$unseen=$false,$anxious=$false,$fear=$false,$mult=1,$food=1,$water=1){
    for($i=0;$i -lt $seconds;$i++){$s.Tick(1,$threat,$false,$dark,$night,$sheltered,$unseen,$anxious,$fear,$mult,$food,$water)}
}
$s=[RebirthStressState]::new();Tick $s 900 $true
Check ([math]::Abs($s.Value-40) -lt .01) 'Pursuit alone exceeds its ceiling'
$s=[RebirthStressState]::new();1..10|ForEach-Object{$s.Hit(1)}
Check ($s.Value -eq 15) 'Burst damage bypasses hit cap'
$s=[RebirthStressState]::new();$s.Value=70;Tick $s 29
Check ($s.Value -eq 70) 'Recovery ignores clearance delay'
Tick $s 121
Check ($s.Value -lt 61 -and $s.Value -gt 59) 'Recovery rate incorrect'
$s=[RebirthStressState]::new();Tick $s 1800 $false $false $true $false $false $false $true
Check ([math]::Abs($s.Value-45) -lt .01) 'Night exploration ceiling incorrect'
$s.Value=40;Tick $s 600 $false $false $true $true $false $false $true
Check ([math]::Abs($s.Value-15) -lt .01) 'Camp does not retain reduced nighttime anxiety'
$s=[RebirthStressState]::new();Tick $s 120 $false $false $false $false $true $true
Check ($s.Value -gt 0) 'Unseen hostile activity does not affect Anxious'
$s=[RebirthStressState]::new();$s.Value=70;$s.Drink(.5);$s.Drink(.5)
Check ($s.TeaSeconds -eq 300 -and $s.TeaRelief -eq 10) 'Split full serving grants wrong tea dose'
$s.Drink(1);Check ($s.TeaRelief -eq 10) 'Chugging multiplies relief'
Tick $s 30 $true
Check ($s.Value -lt 61) 'Tea does not relieve stress during danger'
$copy=[RebirthStressState]::Read($s.Write())
Check ($copy.Value -eq $s.Value -and $copy.TeaCooldown -eq $s.TeaCooldown) 'Save/load loses stress or tea limit'
$legacy=[RebirthStressState]::Read($null);Check ($legacy.Value -eq 0) 'Legacy save does not start safely'
$a=[RebirthStressState]::new();$b=[RebirthStressState]::new();$a.Hit(.75);$b.Hit(1.25)
Check ($a.Value -eq 3.75 -and $b.Value -eq 6.25) 'Trait gain modifiers incorrect'
Write-Output 'Stress checks passed: caps, recovery, darkness/camp, anxiety, combat tea, partial servings, persistence and traits.'

$s=[RebirthStressState]::new();Tick $s 60 -food .5 -water .5
Check ($s.Value -eq 0) 'Adequate food/water adds stress'
Tick $s 60 -food .25 -water .25
Check ([math]::Abs($s.Value-1.5) -lt .01) 'Hunger/thirst should slowly accumulate despite normal recovery'
Tick $s 1800 -food .25 -water .25
Check ([math]::Abs($s.Value-30) -lt .01) 'Moderate unmet needs have incorrect stress floor'
Tick $s 600 -food 1 -water 1
Check ($s.Value -lt .01) 'Eating/drinking does not allow recovery'
$s=[RebirthStressState]::new();Tick $s 1800 -food 0 -water 0
Check ([math]::Abs($s.Value-60) -lt .01) 'Severe unmet needs have incorrect ceiling'
Write-Output 'Hunger/thirst checks passed: threshold, gradual gain, severity, ceiling and recovery.'
