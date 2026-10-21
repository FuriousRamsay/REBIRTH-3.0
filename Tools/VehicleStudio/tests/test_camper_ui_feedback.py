from pathlib import Path
import csv
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
XML = ET.parse(ROOT / 'Config/_Vehicles/camper_turret_windows.xml').getroot()
ACCESS = (ROOT / 'Scripts/Vehicles/Camper/Turrets/RebirthCamperTurretAccess.cs').read_text()
HOLD = (ROOT / 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyOwnerHold.cs').read_text()
SOUND = (ROOT / 'Scripts/Vehicles/Camper/RebirthCamperCommittedItemSound.cs').read_text()


class CamperUIFeedback(unittest.TestCase):
    def test_caption_and_native_icons_exist(self):
        labels = dict(row for row in csv.reader((ROOT / 'Config/_Vehicles/camper_turret_localization.csv').read_text().splitlines()) if len(row) == 2)
        self.assertEqual(labels['xuiCamperTurretCctv'], 'CCTV')
        atlas = (ROOT / '_Documentation/Implementation/SurvivorSystem/Chunk01/AuditArtifacts/VanillaUIAtlasSpriteMetrics.csv').read_text()
        for icon in ('ui_game_symbol_hard_target', 'ui_game_symbol_shape_ammo'):
            self.assertIn(icon + ',', atlas)

    def test_merged_runtime_captions_and_xml_keys(self):
        keys = {node.get('text_key') for node in XML.iter() if node.get('text_key')}
        for name, column in (('Localization.csv', 1), ('Localization.txt', 4)):
            rows = dict((row[0], row[column]) for row in csv.reader((ROOT / 'Config' / name).read_text(encoding='utf-8-sig').splitlines()) if len(row) > column)
            self.assertEqual(rows['xuiCamperTurretCctv'], 'CCTV', name)
            self.assertFalse(keys - rows.keys(), name + ': missing runtime XML localization keys')

    def test_two_neutral_panel_fills_match_existing_window(self):
        fills = [n for n in XML.iter('sprite') if n.get('depth') == '0' and n.get('width') == '922' and n.get('height') in ('408', '393')]
        self.assertEqual(len(fills), 2)
        self.assertTrue(all(n.get('color') == '64,64,64,255' for n in fills))
        other = (ROOT / 'Config/_Vehicles/windows.xml').read_text()
        self.assertIn('color="64,64,64,255"', other)

    def test_mode_centers_are_identical_for_all_four_buttons(self):
        for i in range(4):
            button = XML.find('.//rect[@name="turretMode' + str(i) + '"]')
            self.assertEqual(button.get('height'), '28')
            icon = button.find('./sprite[@name="turretModeIcon' + str(i) + '"]')
            text = button.find('./label[@name="turretModeText' + str(i) + '"]')
            self.assertEqual(icon.get('pivot'), 'center')
            self.assertEqual(text.get('pivot'), 'center')
            self.assertEqual(icon.get('pos').split(',')[1], '-14')
            self.assertEqual(text.get('pos').split(',')[1], '-14')
            self.assertEqual(text.get('height'), '28')

    def test_dynamic_mode_icon(self):
        controller = (ROOT / 'Scripts/Vehicles/Camper/Turrets/XUiC_RebirthCamperTurrets.cs').read_text()
        self.assertIn('slot?.SniperMode==true?"ui_game_symbol_hard_target":"ui_game_symbol_shape_ammo"', controller)

    def test_native_item_audio_not_invented_sound(self):
        self.assertIn('slot.PlayPlaceSound(moved)', SOUND)
        self.assertIn('ItemClass?.SoundPlace', SOUND)
        self.assertIn('after[i].count<stack.count', SOUND)
        self.assertIn('if(moved==null)return', SOUND)

    def test_defense_success_and_duplicate_gates(self):
        self.assertIn('if(error==null&&bag!=null)CommittedSound(s,bag)', ACCESS)
        self.assertIn('if(string.IsNullOrEmpty(status)&&state.Length>0)CommittedSound(s,after)', ACCESS)
        self.assertIn('session.Operation==session.SoundOperation', ACCESS)
        self.assertIn('session.SoundOperation=session.Operation', ACCESS)

    def test_energy_retire_is_only_audio_boundary(self):
        retire = HOLD[HOLD.index('internal static bool Retire'):HOLD.index('internal static bool Mutation')]
        self.assertLess(retire.index('holds.Remove(frame.Operation)'), retire.index('RebirthCamperCommittedItemSound.Play'))
        self.assertIn('if(!holds.TryGetValue(frame.Operation,out var hold))return true', retire)
        self.assertEqual(HOLD.count('RebirthCamperCommittedItemSound.Play'), 1)


if __name__ == '__main__':
    unittest.main()
