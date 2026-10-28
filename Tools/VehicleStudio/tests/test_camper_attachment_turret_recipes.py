from pathlib import Path
import xml.etree.ElementTree as ET
import unittest
import sys
import json

ROOT=Path(__file__).resolve().parents[3]
RECIPES=ET.parse(ROOT/'Config/_Vehicles/recipes.xml').getroot()


class CamperRecipes(unittest.TestCase):
    def test_exact_ingredients_and_native_tool(self):
        expected={
            'modVehicleCamperBody':dict(FuriousRamsayWoodPlank=40,FuriousRamsayScrews=160,resourceScrapIron=1000,
                resourceForgedIron=200,resourceForgedSteel=100,resourceMechanicalParts=30,resourceElectricParts=25,resourceScrapPolymers=80),
            'rebirthCamperTurret':dict(FuriousRamsayScrews=30,resourceScrapIron=150,resourceForgedIron=25,
                resourceForgedSteel=25,resourceMechanicalParts=15,resourceElectricParts=25,resourceScrapPolymers=20,motionsensor=1)}
        for name,ingredients in expected.items():
            rows=RECIPES.findall('.//recipe[@name="'+name+'"]');self.assertEqual(len(rows),1)
            recipe=rows[0]
            self.assertEqual({r.attrib['name']:int(r.attrib['count']) for r in recipe},ingredients)
            self.assertEqual(len(recipe),8)
            self.assertEqual(recipe.get('craft_area'),'workbench')
            self.assertEqual(recipe.get('craft_tool'),'FuriousRamsayScrewdriver')
            self.assertEqual(recipe.get('count'),'1')

    def test_no_guessed_turret_time_or_unlock(self):
        turret=RECIPES.find('.//recipe[@name="rebirthCamperTurret"]')
        self.assertNotIn('craft_time',turret.attrib)
        self.assertNotIn('always_unlocked',turret.attrib)
        self.assertNotIn('learnable',turret.attrib)
        self.assertEqual(RECIPES.find('.//recipe[@name="modVehicleCamperBody"]').get('craft_time'),'600')

    def test_live_workspace_exposes_and_enforces_screwdriver(self):
        workspace=(ROOT/'Scripts/Crafting/UI/XUiC_RebirthStationWorkspace.cs').read_text()
        self.assertIn('grid.requiredTools="FuriousRamsayHammerPliers,FuriousRamsayScrewdriver"',workspace)
        availability=(ROOT/'Scripts/Crafting/UI/RebirthStationToolAvailability.cs').read_text()
        self.assertIn('item.UseTimes < maximum',availability)
        self.assertIn('typeof(XUiC_WorkstationToolGrid)',availability)
        self.assertIn('typeof(XUiC_RecipeStack)',availability)

    def test_eight_entries_reachable_with_current_paging(self):
        requirements=(ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirements.cs').read_text()
        self.assertIn('(requirements.Count + Columns - 1) / Columns',requirements)
        self.assertIn('firstVisibleRow * Columns',requirements)
        pages=[list(range(first*2,min(8,first*2+6))) for first in range(2)]
        self.assertEqual(set(sum(pages,[])),set(range(8)))

    def test_native_batch_observation_requires_exact_tool_and_materials(self):
        sys.path.insert(0,str(ROOT/'Tools/GameBridge'))
        from test_vehicle_parts_batch import recipe_observation,RECIPE_INGREDIENTS
        for name,ingredients in RECIPE_INGREDIENTS.items():
            value=dict(schema=1,observationOnly=True,recipe=name,outputItem=name,outputPerBatch=1,batches=1,tier=0,
                area='workbench',requiredToolType=123,requiredTool='FuriousRamsayScrewdriver',nativeSecondsPerBatch=0,
                ingredients=[dict(name=n,perBatch=q,payment=q) for n,q in ingredients.items()])
            def reply():return {'results':[{'output':[json.dumps(value)]}]}
            self.assertEqual(recipe_observation(reply(),name)['nativeSecondsPerBatch'],0)
            value['requiredTool']='wrong'
            with self.assertRaises(AssertionError):recipe_observation(reply(),name)
        observer=(ROOT/'Scripts/GameBridge/RebirthGameBridgeCamperRecipeObservation.cs').read_text()
        self.assertIn('["requiredToolType"]=recipe.craftingToolType',observer)


if __name__=='__main__':unittest.main()
