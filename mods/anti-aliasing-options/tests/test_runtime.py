"""Exercise the production Lua against a small Unreal/UE4SS fixture."""

from pathlib import Path
import unittest

from lupa.lua54 import LuaRuntime

MOD = Path(__file__).resolve().parents[1]
AA_METHOD = "r.AntiAliasingMethod"
MENU_HOOK = "/Script/mucha.CPP_OptionFunctionHandler:HandleChangeAAType"
CHOICE_METHODS = {0: 0, 1: 1, 2: 2, 3: 4}


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        self.lua = LuaRuntime()
        self.lua.execute((MOD / "tests/unreal_fixture.lua").read_text())
        self.lua.execute((MOD / "src/main.lua").read_text())
        self.state = self.lua.globals()
        self.graphics = self.state["values"]
        self.state.poll()  # Register hooks and apply the fixture's saved TSR choice.

    def tearDown(self):
        self.assertEqual(self.state.saveWrites, 0, "Script modified option saves")

    def assert_method(self, expected):
        self.assertEqual(self.graphics[AA_METHOD], expected)

    def preview_choice(self, choice):
        parameter = self.state.menuParameter(choice)
        self.state.registered[MENU_HOOK][2](None, parameter)
        self.assertEqual(parameter.get().SelectedItem, choice, "Delegate was modified")

    def test_startup_registers_six_hooks_and_restores_tsr(self):
        self.assertEqual(len(list(self.state.registered.items())), 6)
        self.assert_method(4)

    def test_every_hook_restores_each_choice(self):
        for choice, method in CHOICE_METHODS.items():
            self.state.saved.Value = choice
            for name, hook in self.state.registered.items():
                with self.subTest(choice=choice, hook=name):
                    self.graphics[AA_METHOD] = 1  # Simulate a game reset.
                    parameter = self.state.menuParameter(choice)
                    self.assertIsNone(hook[1](None, parameter))
                    self.assertIsNone(hook[2](None, parameter))
                    self.assert_method(method)

    def test_poll_restores_each_choice_without_redundant_writes(self):
        for choice, method in CHOICE_METHODS.items():
            with self.subTest(choice=choice):
                self.state.saved.Value = choice
                self.graphics[AA_METHOD] = 1
                self.state.poll()
                self.assert_method(method)
                writes = len(self.state.commands)
                self.state.poll()
                self.assertEqual(len(self.state.commands), writes)

    def test_menu_preview_reverts_on_cancel_and_follows_saved_choice(self):
        self.state.saved.Value = 1
        self.preview_choice(3)
        self.assert_method(4)
        self.state.poll()  # The cancelled preview did not change the saved FXAA choice.
        self.assert_method(1)
        self.state.saved.Value = 3
        self.state.poll()
        self.assert_method(4)

    def test_unknown_choices_skip_without_looking_up_the_player(self):
        for choice in (-1, 4, 7, 1.5):
            with self.subTest(choice=choice):
                self.state.saved.Value = choice
                self.graphics[AA_METHOD] = 2
                lookups = self.state.controllerLookups
                self.state.poll()
                self.preview_choice(choice)
                self.assert_method(2)
                self.assertEqual(self.state.controllerLookups, lookups)

    def test_unavailable_objects_and_transient_errors_skip_then_recover(self):
        failures = {
            "validOptions": False,
            "validWorld": False,
            "failRead": True,
            "failQueue": True,
        }
        for flag, failure_value in failures.items():
            with self.subTest(failure=flag):
                self.graphics[AA_METHOD] = 2
                self.state[flag] = failure_value
                self.state.poll()
                self.assert_method(2)
                self.state[flag] = not failure_value
                self.state.poll()
                self.assert_method(4)

    def test_only_one_poll_can_wait_on_the_game_thread(self):
        self.graphics[AA_METHOD] = 2
        self.state.holdQueue = True
        self.state.poll()
        self.state.poll()  # The fixture rejects a second queued callback.
        self.assert_method(2)
        pending = self.state.queued
        self.assertIsNotNone(pending)
        self.state.queued = None
        self.state.holdQueue = False
        pending()
        self.assert_method(4)

    def test_other_graphics_settings_are_untouched(self):
        expected = {
            "r.ScreenPercentage": 75,
            "r.TSR.History.ScreenPercentage": 200,
            "r.DepthOfFieldQuality": 2,
            "sg.AntiAliasingQuality": 3,
            "r.BloomQuality": 5,
        }
        for name, value in expected.items():
            self.graphics[name] = value
        self.state.poll()
        for name, value in expected.items():
            with self.subTest(setting=name):
                self.assertEqual(self.graphics[name], value)


if __name__ == "__main__":
    unittest.main(verbosity=2)
