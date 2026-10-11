$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/UI/RebirthItemPreviewLayout.cs'))
Add-Type -TypeDefinition $source
function Assert-Near($actual,$expected,$message){if([Math]::Abs($actual-$expected) -gt .51){throw "$message : actual $actual expected $expected"}}
foreach($size in @(64,80,96,110,120)){
    $ratio=$size/64.0
    foreach($kind in @('quantity','quality','volume')){
        $quality=$kind -eq 'quality';$volume=$kind -eq 'volume'
        $plain=[RebirthItemPreviewLayout]::Place(5.5,-5.5,64,20,-10,$size,-2,-45,26,$quality,$volume,$false,0,0,1,70,70)
        $railX=20+(1-5.5)*$ratio;$railY=-10+(-60+5.5)*$ratio
        $nested=[RebirthItemPreviewLayout]::Place(5.5,-5.5,64,20,-10,$size,-2,-45,26,$quality,$volume,$true,$railX,$railY,1,70,70)
        Assert-Near ($railX+$nested.X*$ratio) $plain.X "$kind/$size nested and native x differ"
        Assert-Near ($railY+$nested.Y*$ratio) $plain.Y "$kind/$size nested and native y differ"
        Assert-Near $plain.X (20-$(if($quality -or $volume){4.5}else{7.5})*$ratio) "$kind/$size horizontal position"
        if($quality -or $volume){Assert-Near ($plain.X+35*$ratio) ($railX+35*$ratio) "$kind/$size label is not exactly centered on its rail"}
        Assert-Near $plain.Y (-10-39.5*$ratio) "$kind/$size vertical position"
        $effectiveFont=$plain.FontSize*$plain.Scale
        $expectedFont=if($volume){21*$ratio}else{26*$ratio}
        if([Math]::Abs($effectiveFont-$expectedFont) -gt $ratio*.51){throw "$kind/$size incorrect effective font"}
    }
}
'PASS: shared production preview layout agrees for nested and native panels at five icon sizes; every count scales proportionally with its icon and volume is reduced.'