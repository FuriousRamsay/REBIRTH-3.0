import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from camper_ui_qualification import unique, item_totals, persistence_image, native_plan, local_labels, CamperUI
from resume_camper_persistence import compare_checkpoint


class CamperUIQualification(unittest.TestCase):
    def test_actual_definitions_and_source_labels_resolve(self):
        plan = native_plan()
        self.assertEqual([row['item'] for row in plan['containers']], ['cntWallSafe_Player', 'cntFridgeStainlessSteel_Player'])
        self.assertEqual([row['capacity'] for row in plan['containers']], [64, 24])
        self.assertEqual(plan['ingredients'], [{'item': 'resourceCoal', 'count': 1}, {'item': 'resourcePotassiumNitratePowder', 'count': 1}])
        self.assertEqual(local_labels()['take'], 'Take')

    def test_ambiguous_hidden_disabled_controls_are_refused(self):
        node = {'path': 'actual/slot', 'ctrl': 'slot', 'rect': [1, 2, 20, 20]}
        self.assertEqual(unique([node], lambda n: n['ctrl'] == 'slot', 'slot'), node)
        for rows in ([], [node, dict(node)], [dict(node, enabled=False)], [dict(node, rect=None)]):
            with self.assertRaises(RuntimeError):
                unique(rows, lambda n: n['ctrl'] == 'slot', 'slot')

    def test_player_inventory_is_not_output_or_cargo_clone(self):
        self.assertTrue(CamperUI.player_slot({'path': 'station/root/rebirthCraftingInventoryRegion/inventory/0'}))
        self.assertTrue(CamperUI.player_slot({'path': 'loot/context/backpackSlot', 'id': 'contextBackpackGridSlot0'}))
        self.assertFalse(CamperUI.player_slot({'path': 'station/root/windowOutput/inventory/0', 'ctrl': 'XUiC_RebirthCraftingInventorySlot'}))
        self.assertFalse(CamperUI.player_slot({'path': 'loot/context/cargoSlot', 'id': 'contextContainerGridSlot0'}))

    def test_persistence_checks_contents_and_stable_identity_not_new_entity_id(self):
        snapshot = {'entity': 12, 'vehicleIdentity': 'vehicle-id', 'dye': None, 'storageCapacity': 2,
            'cargo': [{'slot': 0, 'item': {'name': 'marker', 'count': 7}}, {'slot': 1, 'item': None}],
            'storage': [{'slot': 0, 'item': {'name': 'safe', 'count': 1}}],
            'stations': [{'slot': 0, 'identity': 'station-id', 'fuelSeconds': 2, 'queue': [], 'output': []}]}
        checkpoint = {'kind': 'CAMPER_NATIVE_UI_PERSISTENCE_CHECKPOINT', 'save': 'CodexTest', 'expected': persistence_image(snapshot)}
        compare_checkpoint(checkpoint, dict(snapshot, entity=99))
        for key, value in [('vehicleIdentity', 'another'), ('dye', 'modDyeRed'), ('storageCapacity', 3), ('stations', [])]:
            with self.assertRaises(RuntimeError):
                compare_checkpoint(checkpoint, dict(snapshot, **{key: value}))
        self.assertEqual(item_totals(snapshot['cargo']), {'marker': 7})


if __name__ == '__main__':
    unittest.main()
