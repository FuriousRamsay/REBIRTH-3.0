$ErrorActionPreference='Stop'
Add-Type -Path "$PSScriptRoot/FixtureAdapters.cs","$PSScriptRoot/RebirthRecipeDescriptionText.candidate.cs","$PSScriptRoot/../../Scripts/Survivor/UI/RebirthSkillDisplayNames.cs"
"PASS $([RecipeDescriptionChecks]::Run()) candidate description cases + $([ActualSkillDescriptionChecks]::Run()) actual skill-name cases; native registry/localization adapters explicit; no gameplay execution"
