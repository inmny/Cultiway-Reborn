"""录像版本检查：实际内容变化必须阻止复用旧片段。"""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import common


class RecordingVersionTests(unittest.TestCase):
    def test_commit_metadata_does_not_replace_content_check(self):
        first = {"version": {"sha256": "same-content", "commit": "before"}, "fps": 20}
        committed = {"version": {"sha256": "same-content", "commit": "after"}, "fps": 20}
        self.assertTrue(common.same_recording(first, committed))
        committed["version"]["sha256"] = "changed-content"
        self.assertFalse(common.same_recording(first, committed))
        self.assertFalse(common.same_recording({"fps": 20}, first))

    def test_dirty_source_and_resource_invalidate_recording(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(common, "PROJECT", Path(folder)):
            root = Path(folder)
            (root / "mod.json").write_text('{"version":"test","tags":["Visuals"]}')
            for directory in ["Source", "Content", "GameResources", "Scripts/SpellDemos"]:
                (root / directory).mkdir(parents=True, exist_ok=True)
            with patch.object(common.subprocess, "run") as command:
                command.return_value.stdout = "same-commit"
                original = common.version_stamp()[0]
                source = root / "Source/new.cs"
                source.write_text("未提交的新增代码")
                changed = common.version_stamp()[0]
                self.assertNotEqual(original, changed)
                source.unlink()
                self.assertEqual(original, common.version_stamp()[0])
                resource = root / "Content/model.json"
                resource.write_text("模型甲")
                first = common.version_stamp()[0]
                resource.write_text("模型乙")
                self.assertNotEqual(first, common.version_stamp()[0])

    def test_loader_runtime_fields_do_not_invalidate_recording(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(common, "PROJECT", Path(folder)):
            metadata = Path(folder) / "mod.json"
            value = {"version": "test", "tags": ["Visuals"]}
            with patch.object(common.subprocess, "run") as command:
                command.return_value.stdout = "same-commit"
                metadata.write_text(json.dumps(value))
                original = common.version_stamp()[0]
                value.update(FolderPath="运行时路径", FailReason="")
                metadata.write_text(json.dumps(value))
                self.assertEqual(original, common.version_stamp()[0])
                value["version"] = "更新版本"
                metadata.write_text(json.dumps(value))
                self.assertNotEqual(original, common.version_stamp()[0])


if __name__ == "__main__":
    unittest.main()
