$ErrorActionPreference = 'Stop'
# Compile the production formatter against small definition fixtures. No game state is mutated.
$fixtures = @'
using System;
using System.Collections.Generic;
public class RebirthSurvivorOwnerStateSnapshot { public string BackgroundId="chef"; public List<string> TraitIds=new List<string>(); }
public class RebirthSkillDefinition { public string Id="skill.knives"; }
public class RebirthSurvivorCreationResult { public Dictionary<string,float> StartingSkills=new Dictionary<string,float>(); public Dictionary<string,float> StartingSkillKnowledge=new Dictionary<string,float>(); }
public class Bias { public string SkillId="skill.knives",TierId="trained"; public float Value; public bool HasExplicitValue; }
public class RebirthBackgroundDefinition { public string Id="chef",NameKey="Chef"; public List<Bias> StartingSkills=new List<Bias>(); }
public class RebirthTraitDefinition { public string Id,NameKey,ModifierId; }
public class Component { public string Phase="creation",Target,Value,Note; }
public class RebirthConditionModifierProfileDefinition { public List<Component> Components=new List<Component>(); }
public class Progression { public Dictionary<string,int> SkillBiasTiers=new Dictionary<string,int>(); public float SkillKnowledgeMax=100; }
public class Bundle { public Progression Progression=new Progression(); }
public static class RebirthSurvivorDefinitionRegistry {
 public static Bundle Bundle=new Bundle(); public static RebirthBackgroundDefinition Background=new RebirthBackgroundDefinition();
 public static Dictionary<string,RebirthTraitDefinition> Traits=new Dictionary<string,RebirthTraitDefinition>();
 public static Dictionary<string,RebirthConditionModifierProfileDefinition> Profiles=new Dictionary<string,RebirthConditionModifierProfileDefinition>();
 public static bool TryGetBackground(string id,out RebirthBackgroundDefinition b){b=Background;return true;}
 public static bool TryGetTrait(string id,out RebirthTraitDefinition t){return Traits.TryGetValue(id,out t);}
 public static bool TryGetModifier(string id,out RebirthConditionModifierProfileDefinition p){return Profiles.TryGetValue(id,out p);}
}
public static class RebirthSurvivorUiText { public static string L(string key,string fallback){return key;} }
public static class RebirthTraitGameplayModifierService { public static float GetSkillGainMultiplier(IEnumerable<string> ids,string skill,float current){return 1f;} }
public static class RebirthSkillAptitudeTraitFactory {
 public static bool IsAptitude(RebirthTraitDefinition t){return t.Id=="aptitude";}
 public static bool TryParse(string id,out string skill,out int tier,out int bonus,out int cost){skill="skill.knives";tier=1;bonus=10;cost=1;return true;}
}
public static class StartingPointChecks {
 public static void Run(){
  var owner=new RebirthSurvivorOwnerStateSnapshot();var skill=new RebirthSkillDefinition();var start=new RebirthSurvivorCreationResult();
  RebirthSurvivorDefinitionRegistry.Bundle.Progression.SkillBiasTiers["trained"]=20;
  RebirthSurvivorDefinitionRegistry.Background.StartingSkills.Add(new Bias());
  start.StartingSkills[skill.Id]=20;start.StartingSkillKnowledge[skill.Id]=25;
  string text=RebirthSkillStartingPoint.Describe(owner,skill,23,start);
  Require(text.Contains("Chef: +20") && text.Contains("Change since creation: +3") && text.Contains("Starting knowledge: 25"),"background / gained levels / knowledge");
  owner.TraitIds.Add("weak");RebirthSurvivorDefinitionRegistry.Traits["weak"]=new RebirthTraitDefinition{Id="weak",NameKey="Weak knives",ModifierId="weak"};
  var profile=new RebirthConditionModifierProfileDefinition();profile.Components.Add(new Component{Target="skill.start.skill.knives",Value="-40"});
  // Non-creation effects must not be counted as starting levels.
  profile.Components.Add(new Component{Phase="runtime",Target="skill.start.skill.knives",Value="500"});
  RebirthSurvivorDefinitionRegistry.Profiles["weak"]=profile;start.StartingSkills[skill.Id]=-20;
  text=RebirthSkillStartingPoint.Describe(owner,skill,-17,start);
  Require(text.Contains("Weak knives -40") && text.Contains("Starting level: -20") && text.Contains("Change since creation: +3"),"negative levels");
  owner.TraitIds.Add("aptitude");RebirthSurvivorDefinitionRegistry.Traits["aptitude"]=new RebirthTraitDefinition{Id="aptitude",NameKey="Aptitude"};start.StartingSkills[skill.Id]=-10;
  text=RebirthSkillStartingPoint.Describe(owner,skill,-10,start);Require(text.Contains("Aptitude +10") && !text.Contains("limit applied"),"aptitude");
  start.StartingSkills[skill.Id]=0;text=RebirthSkillStartingPoint.Describe(owner,skill,0,start);Require(text.Contains("starting limit applied"),"bounded origin");
  Require(RebirthSkillStartingPoint.Describe(owner,skill,0,null).Contains("unavailable"),"historical definition mismatch");
 }
 static void Require(bool condition,string name){if(!condition)throw new Exception(name);}
}
'@
$production = Get-Content 'Scripts/Survivor/UI/RebirthSkillStartingPoint.cs' -Raw
$production = $production -replace '(?m)^using [^;]+;\r?\n', ''
Add-Type -TypeDefinition ("using System.Globalization;`nusing System.Text;`n" + $fixtures + "`n" + $production)
[StartingPointChecks]::Run()
'PASS: production starting-point formatter: background, named negative trait, aptitude, bounded start, knowledge and missing history.'
