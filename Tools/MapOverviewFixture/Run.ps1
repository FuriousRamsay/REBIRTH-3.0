param([switch]$NegativeControl)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$baseline=Get-Content "$root/_Documentation/MapZoom_20261028/XUiC_RebirthMapArea.cs.baseline" -Raw
$controller=Get-Content "$root/Scripts/UI/XUiC_RebirthMapArea.cs" -Raw
$restored=$controller.Replace('RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapOverviewZoom));','')
$restored=[regex]::Replace($restored,'    private readonly RebirthMapOverview overview=new RebirthMapOverview\(\);\r?\n','')
$restored=[regex]::Replace($restored,'    internal RebirthMapOverview Overview=>overview;\r?\n','')
$restored=[regex]::Replace($restored,'    public override void OnClose\(\)\{overview.Close\(this\);base.OnClose\(\);\}\r?\n','')
foreach($marker in @('FitToNavigation();targetZoomScale','_MainMapBGPosAndScale')){
 $old=@($baseline-split'\r?\n'|Where-Object {$_.Contains($marker)})
 $new=@($restored-split'\r?\n'|Where-Object {$_.Contains($marker)})
 if($old.Count-ne1-or$new.Count-ne1){throw 'Controller reconstruction marker changed'}
 $restored=$restored.Replace($new[0],$old[0])
}
if($restored.Replace("`r`n","`n")-cne$baseline.Replace("`r`n","`n")){throw 'Unrelated map controller behavior changed'}
$actual=Get-Content "$root/Scripts/UI/RebirthMapOverview.cs" -Raw
$actual=$actual.Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Reflection;','').Replace('using System.Reflection.Emit;','').Replace('using HarmonyLib;','').Replace('using UnityEngine;','')
$fixture=Get-Content "$PSScriptRoot/Checks.cs.txt" -Raw
Add-Type -TypeDefinition $fixture.Replace('/*ACTUAL*/',$actual)
[Checks]::Run([bool]$NegativeControl)
