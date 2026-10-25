"""Verify automatic TSR without menu assets, option saves, or other graphics edits."""

from pathlib import Path
import unittest

from lupa.lua54 import LuaRuntime

MOD = Path(__file__).resolve().parents[1]
FIXTURE = MOD.parents[1] / "tests/unreal_fixture.lua"
AA_METHOD = "r.AntiAliasingMethod"


class RuntimeTests(unittest.TestCase):
    def start(self, setup=""):
        self.lua = LuaRuntime()
        self.lua.execute(FIXTURE.read_text())
        self.lua.execute(setup)
        self.lua.execute((MOD / "src/main.lua").read_text())
        self.state = self.lua.globals()
        self.graphics = self.state["values"]

    def setUp(self):
        self.start()
        self.state.poll()

    def tearDown(self):
        self.assertEqual(self.state.saveReads, 0, "Script read the menu choice")
        self.assertEqual(self.state.saveWrites, 0, "Script modified option saves")

    def assert_tsr(self):
        self.assertEqual(self.graphics[AA_METHOD], 4)

    def test_starts_automatically_without_option_data(self):
        self.start("saved = nil; validOptions = false")
        self.state.poll()
        self.assert_tsr()
        self.assertEqual(len(list(self.state.registered.items())), 6)

    def test_load_cutscene_and_menu_hooks_restore_tsr(self):
        for name, hook in self.state.registered.items():
            with self.subTest(hook=name):
                self.graphics[AA_METHOD] = 1
                parameter = self.state.menuParameter(0)
                self.assertIsNone(hook[1](None, parameter))
                self.assertIsNone(hook[2](None, parameter))
                self.assertEqual(parameter.get().SelectedItem, 0)
                self.assert_tsr()

    def test_poll_restores_resets_without_redundant_writes(self):
        for method in (0, 1, 2):
            self.graphics[AA_METHOD] = method
            self.state.poll()
            self.assert_tsr()
            writes = len(self.state.commands)
            self.state.poll()
            self.assertEqual(len(self.state.commands), writes)

    def test_unavailable_hooks_retry_without_blocking_tsr(self):
        for flag in ("missingFunctions", "failHook"):
            with self.subTest(failure=flag):
                self.start(f"{flag} = true")
                self.state.poll()
                self.assert_tsr()
                self.assertEqual(len(list(self.state.registered.items())), 0)
                self.state[flag] = False
                self.state.poll()
                self.assertEqual(len(list(self.state.registered.items())), 6)
                self.state.poll()  # Duplicate registrations fail in the fixture.

    def test_missing_world_and_queue_errors_recover(self):
        for flag, failure in {"validWorld": False, "failQueue": True}.items():
            with self.subTest(failure=flag):
                self.graphics[AA_METHOD] = 1
                self.state[flag] = failure
                self.state.poll()
                self.assertEqual(self.graphics[AA_METHOD], 1)
                self.state[flag] = not failure
                self.state.poll()
                self.assert_tsr()

    def test_missing_player_or_console_skips_gracefully(self):
        for object_name in ("controller", "console"):
            with self.subTest(object=object_name):
                self.graphics[AA_METHOD] = 1
                self.lua.execute(f"{object_name}.IsValid = function() return false end")
                self.state.poll()
                self.assertEqual(self.graphics[AA_METHOD], 1)
                self.lua.execute(f"{object_name}.IsValid = function() return true end")
                self.state.poll()
                self.assert_tsr()

    def test_only_one_poll_waits_on_game_thread(self):
        self.graphics[AA_METHOD] = 1
        self.state.holdQueue = True
        self.state.poll()
        self.state.poll()
        self.assertEqual(self.graphics[AA_METHOD], 1)
        pending = self.state.queued
        self.assertIsNotNone(pending)
        self.state.queued = None
        self.state.holdQueue = False
        pending()
        self.assert_tsr()

    def test_other_graphics_settings_are_untouched(self):
        settings = {
            "r.ScreenPercentage": 75,
            "r.TSR.History.ScreenPercentage": 200,
            "r.DepthOfFieldQuality": 2,
            "sg.AntiAliasingQuality": 3,
            "r.BloomQuality": 5,
            "r.EyeAdaptationQuality": 2,
        }
        for name, value in settings.items():
            self.graphics[name] = value
        self.state.poll()
        for name, value in settings.items():
            self.assertEqual(self.graphics[name], value, name)


if __name__ == "__main__":
    unittest.main(verbosity=2)
