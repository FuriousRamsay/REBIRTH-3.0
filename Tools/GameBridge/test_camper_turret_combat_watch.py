"""Evidence validator regressions; these do not qualify live turret gameplay."""
import unittest
from camper_turret_combat_watch_live import ammo_transition


def gun(counts, selected, names=('ball', 'ap', 'hp')):
    return {'loadedSlot': selected,
            'ammo': [{'item': n if c else None, 'count': c} for n, c in zip(names, counts)]}


class AmmoEvidenceTests(unittest.TestCase):
    def test_selected_later_stack_preserved(self):
        self.assertFalse(ammo_transition(gun((5, 3, 2), 2), gun((5, 3, 1), 2)))

    def test_depletion_returns_to_first_not_next(self):
        self.assertTrue(ammo_transition(gun((5, 1, 2), 1), gun((5, 0, 2), 0)))
        with self.assertRaises(AssertionError):
            ammo_transition(gun((5, 1, 2), 1), gun((5, 0, 2), 2))

    def test_skips_empty_slots_and_handles_all_empty(self):
        self.assertTrue(ammo_transition(gun((0, 1, 2), 1), gun((0, 0, 2), 2)))
        self.assertFalse(ammo_transition(gun((0, 0, 1), 2), gun((0, 0, 0), -1)))

    def test_rejects_transfer_and_item_swap(self):
        with self.assertRaises(AssertionError):
            ammo_transition(gun((1, 0, 0), 0), gun((2, 0, 0), 0))
        with self.assertRaises(AssertionError):
            ammo_transition(gun((2, 0, 0), 0), gun((1, 0, 0), 0, ('rocket', 'ap', 'hp')))

    def test_rejects_selection_change_before_depletion(self):
        with self.assertRaises(AssertionError):
            ammo_transition(gun((2, 2, 0), 1), gun((2, 1, 0), 0))

    def test_rejects_consuming_unselected_stack(self):
        with self.assertRaises(AssertionError):
            ammo_transition(gun((2, 2, 0), 1), gun((1, 2, 0), 1))


if __name__ == '__main__':
    unittest.main()
