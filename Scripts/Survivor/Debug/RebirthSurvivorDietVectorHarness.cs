using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

public static class RebirthSurvivorDietVectorHarness
{
    private sealed class Vector
    {
        public string Name;
        public string Rule;
        public string[] Tags;
        public RebirthDietCompatibilityState State;
        public Func<float> Modifier;
        public Vector(string name,string rule,string[] tags,RebirthDietCompatibilityState state,Func<float> modifier=null)
        { Name=name; Rule=rule; Tags=tags; State=state; Modifier=modifier; }
    }

    public static string RunAll()
    {
        List<Vector> vectors = new List<Vector>
        {
            new Vector("unrestricted-empty","unrestricted",new string[0],RebirthDietCompatibilityState.Compatible),
            new Vector("unrestricted-plant","unrestricted",new[]{"Plant"},RebirthDietCompatibilityState.Compatible),
            new Vector("pescatarian-plant","pescatarian",new[]{"Plant"},RebirthDietCompatibilityState.Compatible),
            new Vector("pescatarian-fish","pescatarian",new[]{"Fish"},RebirthDietCompatibilityState.Compatible),
            new Vector("pescatarian-egg","pescatarian",new[]{"Egg"},RebirthDietCompatibilityState.Compatible),
            new Vector("pescatarian-honey","pescatarian",new[]{"Honey"},RebirthDietCompatibilityState.Compatible),
            new Vector("pescatarian-dairy","pescatarian",new[]{"Dairy"},RebirthDietCompatibilityState.Compatible),
            new Vector("pescatarian-meat","pescatarian",new[]{"Meat"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VegetarianViolation),
            new Vector("pescatarian-animal-fat","pescatarian",new[]{"AnimalFat"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VegetarianViolation),
            new Vector("vegetarian-plant","vegetarian",new[]{"Plant"},RebirthDietCompatibilityState.Compatible),
            new Vector("vegetarian-egg","vegetarian",new[]{"Egg"},RebirthDietCompatibilityState.Compatible),
            new Vector("vegetarian-honey","vegetarian",new[]{"Honey"},RebirthDietCompatibilityState.Compatible),
            new Vector("vegetarian-dairy","vegetarian",new[]{"Dairy"},RebirthDietCompatibilityState.Compatible),
            new Vector("vegetarian-meat","vegetarian",new[]{"Meat"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VegetarianViolation),
            new Vector("vegetarian-fish","vegetarian",new[]{"Fish"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VegetarianViolation),
            new Vector("vegetarian-animal-fat","vegetarian",new[]{"AnimalFat"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VegetarianViolation),
            new Vector("vegan-plant","vegan",new[]{"Plant"},RebirthDietCompatibilityState.Compatible),
            new Vector("vegan-plant-sweet","vegan",new[]{"Plant","Sweet"},RebirthDietCompatibilityState.Compatible),
            new Vector("vegan-egg","vegan",new[]{"Egg"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VeganViolation),
            new Vector("vegan-honey","vegan",new[]{"Honey"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.VeganViolation),
            new Vector("carnivore-meat","carnivore",new[]{"Meat"},RebirthDietCompatibilityState.Compatible),
            new Vector("carnivore-fish","carnivore",new[]{"Fish"},RebirthDietCompatibilityState.Compatible),
            new Vector("carnivore-egg","carnivore",new[]{"Egg"},RebirthDietCompatibilityState.Compatible),
            new Vector("carnivore-honey","carnivore",new[]{"Honey"},RebirthDietCompatibilityState.Compatible),
            new Vector("carnivore-mixed","carnivore",new[]{"Meat","Plant"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.CarnivoreViolation),
            new Vector("carnivore-plant","carnivore",new[]{"Plant"},RebirthDietCompatibilityState.OffDiet,()=>RebirthConditionRuntimeConfig.CarnivoreViolation),
            new Vector("vegetarian-empty","vegetarian",new string[0],RebirthDietCompatibilityState.UnknownComposition),
            new Vector("vegan-sweet-only","vegan",new[]{"Sweet"},RebirthDietCompatibilityState.UnknownComposition),
            new Vector("unknown-rule","flexitarian",new[]{"Fish"},RebirthDietCompatibilityState.UnknownRule),
            new Vector("normalization"," CARNIVORE ",new[]{" meat ","Sweet"},RebirthDietCompatibilityState.Compatible),
            new Vector("extra-noncomposition-tag","carnivore",new[]{"Meat","FuturePresentationTag"},RebirthDietCompatibilityState.Compatible)
        };

        int pass = 0; List<string> failures = new List<string>();
        for (int i = 0; i < vectors.Count; i++)
        {
            Vector v = vectors[i];
            RebirthDietCompatibilityResult r = RebirthDietCompatibility.Evaluate(v.Rule, v.Tags);
            float expectedModifier = v.Modifier != null ? v.Modifier() : 0f;
            bool ok = r.State == v.State && Math.Abs(r.DietModifier - expectedModifier) < 0.0001f;
            if (ok) pass++; else failures.Add(v.Name + " expected=" + v.State + "/" + expectedModifier.ToString("0.##",CultureInfo.InvariantCulture) + " actual=" + r.State + "/" + r.DietModifier.ToString("0.##",CultureInfo.InvariantCulture) + " reason=" + r.Reason);
        }

        int definitionPass = 0;
        Dictionary<string,int> expectedPoints = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        { {"diet.unrestricted",0},{"diet.vegetarian",2},{"diet.carnivore",3},{"diet.vegan",4} };
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle != null)
        {
            foreach (KeyValuePair<string,int> kv in expectedPoints)
            {
                RebirthDietDefinition d = null;
                for (int i=0;i<bundle.Diets.Count;i++) if (string.Equals(bundle.Diets[i].Id,kv.Key,StringComparison.OrdinalIgnoreCase)) { d=bundle.Diets[i]; break; }
                if (d != null && d.Points == kv.Value) definitionPass++;
                else failures.Add("definition " + kv.Key + " expectedPoints=" + kv.Value + " actual=" + (d==null?"<missing>":d.Points.ToString(CultureInfo.InvariantCulture)));
            }
        }
        else failures.Add("definition bundle unavailable");

        StringBuilder sb = new StringBuilder();
        bool all = pass == vectors.Count && definitionPass == expectedPoints.Count;
        sb.Append("[REBIRTH Survivor Diet vectors] ").Append(all?"PASS":"FAIL")
          .Append(" compatibility=").Append(pass).Append('/').Append(vectors.Count)
          .Append(" definitionPoints=").Append(definitionPass).Append('/').Append(expectedPoints.Count);
        for (int i=0;i<failures.Count;i++) sb.Append("\n  FAIL ").Append(failures[i]);
        return sb.ToString();
    }
}
