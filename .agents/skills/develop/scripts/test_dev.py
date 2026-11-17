"""检查环境归属、快照、归档和 worktree 回收边界；不启动游戏。"""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("cultiway_dev", Path(__file__).with_name("dev.py"))
dev = importlib.util.module_from_spec(spec)
spec.loader.exec_module(dev)


class EnvironmentTests(unittest.TestCase):
    """使用真实的小型 Git 仓库和独立文件，验证会删除资料的流程。"""

    def setUp(self):
        """准备彼此独立的模组、游戏和工具来源。"""
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.worktree = self.base / "repo"
        self.source = self.base / "installed-game"
        self.toolbox = self.base / "toolbox"
        self.root = self.base / "runtime"
        for path in (self.worktree, self.toolbox):
            path.mkdir()
            dev.command(["git", "init", "-b", "master", path])
            dev.git(path, "config", "user.name", "环境检查")
            dev.git(path, "config", "user.email", "test@example.invalid")
        for relative in ("worldbox", "UnityPlayer.so", "libdoorstop.so",
                         "BepInEx/core/BepInEx.Preloader.dll", "BepInEx/config/BepInEx.cfg",
                         "worldbox_Data/Managed/UnityEngine.CoreModule.dll",
                         "worldbox_Data/StreamingAssets/mods/NeoModLoader.dll",
                         "worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll",
                         "worldbox_Data/StreamingAssets/mods/NML/Assemblies/Harmony.dll",
                         "worldbox_Data/StreamingAssets/mods/NML/commit"):
            self.put(self.source / relative, "original")
        self.put(self.source / "worldbox_Data/StreamingAssets/mods/NML/CompiledMods/CULTIWAY.dll", "wrong-branch")
        self.put(self.toolbox / "Runtime/Transport/ScriptRpcServer.cs", "// runtime")
        self.put(self.toolbox / "Client/DebugToolbox.Client.csproj", "<Project />")
        self.put(self.toolbox / "mod.json", "{}")
        self.put(self.toolbox / "ModClass.cs", "// entry")
        dev.git(self.toolbox, "add", ".")
        dev.git(self.toolbox, "commit", "-m", "工具样本")
        self.put(self.worktree / ".gitignore", ".cultiway-dev.props\n")
        self.put(self.worktree / "Source/Entry.cs", "// first")
        self.put(self.worktree / "Locales/cz.json", '{"name":"原名"}')
        self.put(self.worktree / "mod.json", '{"GUID":"CULTIWAY"}')
        dev.git(self.worktree, "add", ".")
        dev.git(self.worktree, "commit", "-m", "模组样本")
        self.client = self.base / "client.dll"
        self.put(self.client, "client")
        self.client_patch = patch.object(dev.Environment, "prepare_client", return_value=self.client)
        self.client_patch.start()
        self.addCleanup(self.client_patch.stop)
        self.environment = dev.Environment(self.worktree, self.root)

    @staticmethod
    def put(path, text):
        """建立测试资料。"""
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def setup_environment(self, environment=None):
        """准备环境并返回完整状态。"""
        environment = environment or self.environment
        environment.setup(self.source, self.toolbox)
        return environment.load()

    def prepare_delivery(self, environment=None):
        """建立已实际复制的输入与待归档验证资料。"""
        environment = environment or self.environment
        state = self.setup_environment(environment)
        environment.snapshot(state)
        environment.save(state)
        self.put(environment.directory / "runs/sample/result.json", '{"passed":true}')
        self.put(environment.directory / "runs/sample/temp/DebugToolbox/session.json", '{"token":"secret"}')
        return {"state": "OPEN", "url": "https://github.com/example/repo/pull/1",
                "headRefOid": dev.git(environment.worktree, "rev-parse", "HEAD"),
                "headRefName": dev.git(environment.worktree, "branch", "--show-current")}

    def test_two_environments_have_independent_files_and_sessions(self):
        """一个实例写日志和资料不会改变来源或另一个实例。"""
        self.setup_environment()
        peer = dev.Environment(self.worktree, self.root, "peer")
        self.setup_environment(peer)
        original = self.source / "worldbox"
        self.assertNotEqual(original.stat().st_ino, (self.environment.game / "worldbox").stat().st_ino)
        self.put(self.environment.game / "worldbox", "changed")
        self.assertEqual(original.read_text(), "original")
        self.assertEqual((peer.game / "worldbox").read_text(), "original")
        self.assertNotEqual(self.environment.profile, peer.profile)
        self.assertFalse((peer.game / "worldbox_Data/StreamingAssets/mods/NML/CompiledMods").exists())

    def test_repeated_setup_preserves_profile_and_ready_game(self):
        """重复准备保留同一份保存重启资料。"""
        self.setup_environment()
        self.put(self.environment.profile / "world.fixture", "saved")
        self.put(self.environment.game / "test-cache", "keep")
        self.environment.setup()
        self.assertEqual((self.environment.profile / "world.fixture").read_text(), "saved")
        self.assertTrue((self.environment.game / "test-cache").exists())

    def test_partial_setup_is_rebuilt_on_retry(self):
        """准备中断不能把半份副本当成完整环境。"""
        with patch.object(dev, "copy_path", side_effect=OSError("磁盘中断")):
            with self.assertRaises(OSError):
                self.setup_environment()
        self.environment.setup()
        self.assertTrue((self.environment.game / "UnityPlayer.so").exists())
        self.assertEqual(self.environment.load()["status"], "prepared")

    def test_changed_template_rebuilds_game_and_keeps_profile(self):
        """来源升级使游戏副本失效，但不删除测试存档。"""
        self.setup_environment()
        self.put(self.environment.profile / "world.fixture", "saved")
        self.put(self.environment.game / "old-cache", "old")
        self.put(self.source / "worldbox_Data/Managed/UnityEngine.CoreModule.dll", "upgraded")
        self.environment.setup()
        self.assertFalse((self.environment.game / "old-cache").exists())
        self.assertTrue((self.environment.profile / "world.fixture").exists())

    def test_snapshot_detects_content_change_with_old_timestamp(self):
        """保留旧修改时间的代码改动也能被识别，现有快照不被修改。"""
        state = self.setup_environment()
        self.environment.snapshot(state)
        source = self.worktree / "Source/Entry.cs"
        times = source.stat()
        source.write_text("// second")
        os.utime(source, ns=(times.st_atime_ns, times.st_mtime_ns))
        self.assertNotEqual(dev.mod_inputs(self.worktree)["sha256"], state["inputs"]["sha256"])
        self.assertEqual((self.environment.game / "Mods/Cultiway/Source/Entry.cs").read_text(), "// first")

    def test_localization_participates_in_input_identity(self):
        """只修改本地化也会使上次运行输入失效。"""
        before = dev.mod_inputs(self.worktree)
        self.put(self.worktree / "Locales/cz.json", '{"name":"新名"}')
        self.assertNotEqual(before["sha256"], dev.mod_inputs(self.worktree)["sha256"])

    def test_restart_preserves_artifacts_and_reuses_unchanged_snapshot(self):
        """保存重启保留旧输出，相同输入不删除已经编译的缓存。"""
        state = self.setup_environment()
        self.environment.snapshot(state)
        output = self.environment.game / "Mods/Cultiway/artifacts/previous.json"
        self.put(output, '{"saved":true}')
        cache = self.environment.game / "worldbox_Data/StreamingAssets/mods/NML/CompiledMods/CULTIWAY.dll"
        self.put(cache, "compiled")
        self.environment.snapshot(state)
        self.assertTrue(cache.exists())
        self.put(self.worktree / "Source/Entry.cs", "// changed")
        self.environment.snapshot(state)
        self.assertEqual(output.read_text(), '{"saved":true}')
        self.assertFalse(cache.exists())

    def test_environment_lock_rejects_second_controller(self):
        """同环境的第二个调用不会交错，其他环境的锁可独立取得。"""
        with dev.locked(self.root / "locks/one.lock"):
            with self.assertRaises(dev.DevError):
                with dev.locked(self.root / "locks/one.lock"):
                    pass
            with dev.locked(self.root / "locks/two.lock"):
                pass

    def test_foreign_process_is_not_stopped(self):
        """旧记录指向其他进程时，拒绝终止它。"""
        state = self.setup_environment()
        child = subprocess.Popen(["python3", "-c", "import time; time.sleep(30)"])
        try:
            state["launch"] = {"id": "foreign", "pid": child.pid, "processStart": dev.process_identity(child.pid)}
            self.environment.save(state)
            with self.assertRaises(dev.DevError):
                self.environment.stop()
            self.assertIsNone(child.poll())
        finally:
            child.terminate()
            child.wait()

    def test_foreign_session_does_not_receive_command(self):
        """会话文件的进程号错误时，不向客户端发送任何脚本。"""
        state = self.setup_environment()
        state["launch"] = {"id": "one", "pid": 123}
        self.put(self.environment.directory / "runs/one/temp/DebugToolbox/session.json", '{"processId":456}')
        with patch.object(self.environment, "check_process", return_value=True), patch.object(dev, "command") as call:
            with self.assertRaises(dev.DevError):
                self.environment.rpc(state, "hello")
            call.assert_not_called()

    def test_loading_world_does_not_accept_validation(self):
        """通道已经可用但世界未加载时，不执行验证命令。"""
        state = self.setup_environment()
        self.environment.snapshot(state)
        self.environment.save(state)
        with patch.object(self.environment, "check_process", return_value=True), \
                patch.object(self.environment, "probe", return_value={"ready": False}), \
                patch.object(dev.subprocess, "run") as run:
            with self.assertRaises(dev.DevError):
                self.environment.execute(["should-not-run"])
            run.assert_not_called()

    def test_exec_timeout_waits_for_the_whole_group(self):
        """即使子进程改用独立的输出位置，超时返回前也要等它退出。"""
        state = self.setup_environment()
        state.update(launch={"id": "sample", "pid": 0}, inputs={"sha256": "sample"})
        self.environment.save(state)
        child_pid = self.base / "child.pid"
        spawn = (
            "import subprocess, sys, time; from pathlib import Path; "
            "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(5)'], "
            "stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL); "
            "Path(sys.argv[1]).write_text(str(child.pid)); time.sleep(5)"
        )
        with patch.object(self.environment, "require_current"):
            with self.assertRaises(subprocess.TimeoutExpired):
                self.environment.execute([sys.executable, "-c", spawn, str(child_pid)], timeout=1)
        self.assertIsNone(dev.process_identity(int(child_pid.read_text())))
        self.assertEqual(self.environment.load()["status"], "uncertain")

    def test_exec_interrupt_reaps_group_when_parent_already_exited(self):
        """主命令先退出时，Ctrl+C 仍会结束持有输出管道的子进程。"""
        state = self.setup_environment()
        state.update(launch={"id": "sample", "pid": 0}, inputs={"sha256": "sample"})
        self.environment.save(state)
        child_pid = self.base / "child.pid"
        child = """
import os, signal, sys, time
while os.getppid() == int(sys.argv[1]):
    time.sleep(0.01)
os.kill(int(sys.argv[2]), signal.SIGINT)
time.sleep(5)
"""
        spawn = """
import os, subprocess, sys
from pathlib import Path
child = subprocess.Popen([sys.executable, "-c", sys.argv[1], str(os.getpid()), str(os.getppid())])
Path(sys.argv[2]).write_text(str(child.pid))
"""
        entry = "import dev; dev.Environment.require_current = lambda *_: None; dev.main()"
        result = subprocess.run(
            [sys.executable, "-c", entry, "--worktree", str(self.worktree), "--root", str(self.root),
             "exec", "--timeout", "3", "--", sys.executable, "-c", spawn, child, str(child_pid)],
            cwd=Path(__file__).parent, capture_output=True, text=True, timeout=8,
        )
        self.assertIsNone(dev.process_identity(int(child_pid.read_text())))
        self.assertEqual(result.returncode, 130, result.stderr)
        self.assertEqual(self.environment.load()["status"], "uncertain")

    def test_cleanup_rejects_symlink_and_outside_directory(self):
        """不能借环境内符号链接删除外部资料。"""
        self.setup_environment()
        outside = self.base / "personal"
        self.put(outside / "keep", "keep")
        link = self.environment.directory / "external"
        link.symlink_to(outside, target_is_directory=True)
        with self.assertRaises(dev.DevError):
            dev.remove_owned(link, self.environment.directory)
        with self.assertRaises(dev.DevError):
            dev.remove_owned(outside, self.environment.directory)
        self.assertTrue((outside / "keep").exists())

    def test_delivery_archives_inputs_and_results_before_cleanup(self):
        """正式交付后证据独立保留，会话令牌不进入证据。"""
        info = self.prepare_delivery()
        with patch.object(dev, "pr_info", return_value=info):
            result = self.environment.finish(info["url"])
        archive = Path(result["evidence"])
        self.assertFalse(self.environment.game.exists())
        self.assertFalse(self.environment.profile.exists())
        self.assertTrue((archive / "runs/sample/result.json").exists())
        self.assertFalse((archive / "runs/sample/temp").exists())
        self.assertTrue((archive / "inputs/Cultiway/Source/Entry.cs").exists())
        self.assertTrue(self.worktree.exists())
        self.assertEqual(self.environment.finish(info["url"])["status"], "delivered")
        self.assertEqual(self.environment.stop()["status"], "delivered")
        self.assertEqual(self.environment.archive("cleaned")["status"], "delivered")

    def test_failed_archive_preserves_game_and_profile(self):
        """证据保存失败时不删除运行资料。"""
        info = self.prepare_delivery()
        with patch.object(dev, "pr_info", return_value=info), patch.object(dev.shutil, "copytree", side_effect=OSError("空间不足")):
            with self.assertRaises(OSError):
                self.environment.finish(info["url"])
        self.assertTrue(self.environment.game.exists())
        self.assertTrue(self.environment.profile.exists())

    def test_failed_validation_blocks_delivery_until_success(self):
        """验证失败及随后退出游戏都不能绕过交付检查。"""
        info = self.prepare_delivery()
        state = self.environment.load()
        state["launch"] = {"id": "sample", "pid": 0}
        self.environment.save(state)
        with patch.object(dev, "pr_info", return_value=info), patch.object(self.environment, "require_current"):
            with self.assertRaisesRegex(dev.DevError, "验证命令失败"):
                self.environment.execute([sys.executable, "-c", "raise SystemExit(1)"])
            with self.assertRaisesRegex(dev.DevError, "重新完成验证"):
                self.environment.finish(info["url"])
            self.assertTrue(self.environment.game.exists())

            # 旧版留下的 uncertain 状态也要在退出游戏后继续阻止交付。
            state = self.environment.load()
            state.pop("validationFailed")
            self.environment.save(state)
            self.environment.stop()
            with self.assertRaisesRegex(dev.DevError, "重新完成验证"):
                self.environment.finish(info["url"])
            self.environment.execute([sys.executable, "-c", "print('验证完成')"])
            self.assertEqual(self.environment.finish(info["url"])["status"], "delivered")

    def test_clean_retries_partial_removal_without_rearchiving(self):
        """回收中断后继续清理剩余目录，保留同一份证据。"""
        self.prepare_delivery()
        directory = self.environment.directory
        self.put(directory / "control/marker", "saved")
        remove = dev.remove_owned

        def fail_at_profile(path, owner):
            """模拟游戏已经删除，但清理资料时磁盘报错。"""
            if path == self.environment.profile:
                raise OSError("无法清理资料")
            remove(path, owner)

        with patch.object(dev, "remove_owned", side_effect=fail_at_profile):
            with self.assertRaisesRegex(OSError, "无法清理资料"):
                self.environment.archive("cleaned")
        archive = self.environment.load()["evidence"]
        self.assertFalse(self.environment.game.exists())
        self.assertTrue(self.environment.profile.exists())

        result = self.environment.archive("cleaned")
        self.assertEqual(result["evidence"], archive)
        self.assertEqual(result["status"], "cleaned")
        for name in ("game", "profile", "control", "runs"):
            self.assertFalse((directory / name).exists())
        self.assertTrue((Path(archive) / "runs/sample/result.json").is_file())

    def test_delivery_rejects_unpushed_or_uncommitted_changes(self):
        """PR 不包含当前提交或本地仍有修改时，不能当作已交付。"""
        info = self.prepare_delivery()
        with patch.object(dev, "pr_info", return_value=dict(info, headRefOid="0" * 40)):
            with self.assertRaises(dev.DevError):
                self.environment.finish(info["url"])
        self.put(self.worktree / "Source/Entry.cs", "// uncommitted")
        with patch.object(dev, "pr_info", return_value=info):
            with self.assertRaises(dev.DevError):
                self.environment.finish(info["url"])
        self.assertTrue(self.environment.game.exists())

    def test_reap_preserves_open_pr_and_main_checkout(self):
        """未合并 PR 和主工作目录均不允许回收。"""
        info = self.prepare_delivery()
        with patch.object(dev, "pr_info", return_value=info):
            self.environment.finish(info["url"])
            with self.assertRaises(dev.DevError):
                self.environment.reap(True)
        with patch.object(dev, "pr_info", return_value=dict(info, state="MERGED")):
            with self.assertRaises(dev.DevError):
                self.environment.reap(True)
        self.assertTrue(self.worktree.exists())

    def test_merged_clean_linked_worktree_can_be_removed(self):
        """压缩合并由 PR 状态确认，回收普通 worktree 后证据仍在。"""
        linked = self.base / "linked"
        dev.git(self.worktree, "worktree", "add", "-b", "codex/task", str(linked), "HEAD")
        environment = dev.Environment(linked, self.root)
        info = self.prepare_delivery(environment)
        with patch.object(dev, "pr_info", return_value=info):
            delivered = environment.finish(info["url"])
        with patch.object(dev, "pr_info", return_value=dict(info, state="MERGED")):
            self.assertTrue(environment.reap()["eligible"])
            environment.reap(True)
        self.assertFalse(linked.exists())
        self.assertTrue(Path(delivered["evidence"]).exists())
        self.assertTrue(self.worktree.exists())


if __name__ == "__main__":
    unittest.main()
