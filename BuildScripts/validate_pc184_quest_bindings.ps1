$ErrorActionPreference = 'Stop'
[Reflection.Assembly]::LoadFrom((Resolve-Path '../../7DaysToDie_Data/Managed/Antlr3.Runtime.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Resolve-Path '../../7DaysToDie_Data/Managed/NCalc.dll')) | Out-Null
[xml]$ui = Get-Content -LiteralPath 'Config/XUi_InGame/windows.xml' -Raw
$questWindow = @($ui.SelectNodes("//window[@name='windowQuestList']"))[-1]
$viewport = $questWindow.SelectSingleNode(".//*[@name='listViewport']")
$expected = @{ clippingsize = @('484,436','484,714'); clippingcenter = @('242,-218','242,-357') }
foreach ($attribute in $expected.Keys) {
    $raw = $viewport.GetAttribute($attribute)
    $formula = $raw.Substring(2, $raw.Length - 3).Trim()
    foreach ($enabled in @($false,$true)) {
        $expression = [NCalc.Expression]::new($formula)
        $expression.Parameters['questsautoaccept'] = $enabled
        $actual = $expression.Evaluate()
        if ($actual -ne $expected[$attribute][[int]$enabled]) { throw "$attribute evaluated incorrectly: $actual" }
    }
}
$count = 0
foreach ($node in $questWindow.SelectNodes('.//*')) {
    foreach ($attribute in $node.Attributes) {
        $raw = $attribute.Value
        if (-not $raw.StartsWith('{#') -or -not $raw.Contains('questsautoaccept')) { continue }
        foreach ($enabled in @($false,$true)) {
            $expression = [NCalc.Expression]::new($raw.Substring(2,$raw.Length-3).Trim())
            $expression.Parameters['questsautoaccept'] = $enabled
            $null = $expression.Evaluate()
            $count++
        }
    }
}
Write-Output "PASS: game NCalc parser evaluated $count Quest layout cases, including both clipping sizes and centers."
