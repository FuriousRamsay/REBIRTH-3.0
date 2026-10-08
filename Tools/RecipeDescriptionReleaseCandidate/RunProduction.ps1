$ErrorActionPreference='Stop'
Add-Type -Path "$PSScriptRoot/FixtureAdapters.cs","$PSScriptRoot/../../Scripts/Survivor/Progression/Explorer/RebirthRecipeDescriptionText.cs","$PSScriptRoot/../../Scripts/Survivor/UI/RebirthSkillDisplayNames.cs"
"PASS $([RecipeDescriptionChecks]::Run()) production description cases + $([ActualSkillDescriptionChecks]::Run()) actual skill-name cases; native registry/localization adapters explicit; no gameplay execution"

