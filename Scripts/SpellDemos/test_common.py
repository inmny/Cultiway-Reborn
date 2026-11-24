"""录像版本检查：实际内容变化必须阻止复用旧片段。"""
from argparse import Namespace
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import common
from record_body_connections import existing_clips, specialized_files


class RegisteredConstructionTests(unittest.TestCase):
    def test_battle_keeps_independent_loadouts_without_replacing_same_id(self):
        left = dict(id="Demo.Palm", name="飞掌", construction={"Id": "Demo.Palm", "Cost": 4})
        right = dict(id="Demo.Legacy", name="旧术", adjustable=True)
        case = common.battle_case([left], [right, right])
        self.assertEqual(case["battleSides"], [[left], [{"id": right["id"], "name": right["name"]}]])
        changed = dict(left, construction={"Id": "Demo.Palm", "Cost": 5})
        with self.assertRaisesRegex(ValueError, "同编号能力资料不一致"):
            common.battle_case([left], [changed])

    def test_catalog_filters_combine_without_losing_definition_or_unavailable_reason(self):
        definition = {"Id": "Created.Cleanse", "Creator": 42}
        created = dict(id=definition["Id"], name="净土", adjustable=False, construction=definition,
                       labels=dict(source={"created": "自创构造"}, school={"semantic.element.water": "水"},
                                   scale={"city": "城域"}, effect={"land.cleanse": "净化地块"}))
        passive = dict(id="Artifact.Passive", name="器物护持", adjustable=False, variants=0,
                       labels=dict(source={"artifact.passive": "法宝被动"}), unavailable="需要被动触发")
        args = Namespace(filter="cleanse", scope="all", source="内置构造, created", school="水",
                         scale="城域", effect="LAND.CLEANSE")
        selection = common.filter_catalog([created, passive], args)
        self.assertEqual(selection, [created])
        self.assertIs(selection[0]["construction"], definition)
        args.scale = "世界"
        self.assertFalse(common.filter_catalog([created, passive], args))
        args.filter = args.school = args.effect = ""
        args.source, args.scale = "法宝被动", "未标注"
        selection = common.filter_catalog([created, passive], args)
        self.assertEqual(selection, [passive])
        args.cases, args.variants = "base,trajectory", "all"
        cases, skipped = common.choose_cases(selection, dict(modifiers=[], trajectories=[]), args)
        self.assertFalse(cases)
        self.assertEqual([entry["reason"] for entry in skipped], ["需要被动触发"] * 2)

    def test_mixed_recording_keeps_the_world_construction_and_reports_missing_scene(self):
        legacy = dict(id="Cultiway.Legacy", name="旧法术", group="基础法术", adjustable=True,
                      variants=1, trajectory="default", trajectories=["default"], modifiers=[])
        definition = {"Id": "Cultiway.Created.Demo", "Name": "原创", "Root": {"Form": "sword"}}
        construction = dict(id=definition["Id"], name=definition["Name"], group="万法阁构造",
                            trajectory="构造资料", construction=definition)
        unsupported = dict(construction, id="Cultiway.Created.Organ", name="兽足", unavailable="需要兽足")
        catalog = dict(spells=[legacy, construction, unsupported], modifiers=[], trajectories=[])
        args = Namespace(cases="base,trajectory", variant=0, variants="first", modifier=[], trajectory=None)

        cases, skipped = common.choose_cases(catalog["spells"], catalog, args)
        self.assertEqual([(case["id"], case["mode"]) for case in cases],
                         [(legacy["id"], "base"), (construction["id"], "base"), (construction["id"], "trajectory")])
        self.assertNotIn("construction", cases[0])
        self.assertEqual(cases[1]["construction"], definition)
        self.assertEqual(cases[2]["construction"], definition)
        self.assertIn(dict(id=unsupported["id"], case="base", reason="需要兽足"), skipped)
        selected = common.resolve(catalog["spells"], definition["Id"])
        self.assertEqual(common.choose_cases([selected], catalog, args)[0][0]["construction"], definition)


class RecordingVersionTests(unittest.TestCase):
    def test_clip_fingerprints_bind_the_movie_and_runtime(self):
        first = dict(version=dict(sha256="same"), runtime=dict(game="current"))
        second = dict(first, runtime=dict(game="other"))
        stamp = common.recording_stamp(first)
        self.assertNotEqual(stamp, common.recording_stamp(second))
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            clip = output / "fireball"
            clip.mkdir()
            for name in ("demo.mp4", "result.json", "preview.png"):
                (clip / name).write_bytes(name.encode())
            receipt = common.clip_facts(output, "fireball", stamp)
            common.write_json(clip / "clip.json", receipt)
            self.assertEqual(common.checked_clip(output, "fireball", stamp), receipt)
            (clip / "demo.mp4").write_bytes(b"replaced")
            with self.assertRaisesRegex(ValueError, "旧片段与本批凭据不符"):
                common.checked_clip(output, "fireball", stamp)

    def test_specialized_collection_rejects_another_environment_or_file(self):
        recording = dict(version=dict(sha256="same"), view="native", runtime={"game": "current"},
                         recordingTools={"encoder": "current"})
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            for name in ("shield.mp4", "result.json", "recording.json"):
                (output / name).write_bytes(name.encode())
            entry = dict(case="shield", view="native", recordingEnvironment=recording,
                         result=str(output / "result.json"), recording=str(output / "recording.json"))
            entry["files"] = specialized_files(entry, output / "shield.mp4")
            common.write_json(output / "shield.json", entry)
            self.assertEqual(existing_clips(output, ["shield"], recording), {"shield": entry})
            changed = dict(recording, recordingTools={"encoder": "other"})
            with self.assertRaisesRegex(ValueError, "版本或文件不符"):
                existing_clips(output, ["shield"], changed)
            (output / "shield.mp4").write_bytes(b"replaced")
            with self.assertRaisesRegex(ValueError, "版本或文件不符"):
                existing_clips(output, ["shield"], recording)

    def test_loaded_copy_and_fixture_are_checked_independently(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            work, loaded = root / "work", root / "loaded"
            for directory in (work, loaded):
                (directory / "Locales").mkdir(parents=True)
                (directory / "mod.json").write_text('{"version":"test"}', encoding="utf-8")
                (directory / "Locales/notice.json").write_text('{"notice":"原文"}', encoding="utf-8")
            fixture = work / common.FIXTURE_DIR / "map.wbox"
            fixture.parent.mkdir(parents=True)
            fixture.write_bytes(b"fixture")
            with patch.object(common, "PROJECT", work), patch.object(common.subprocess, "run") as command:
                command.return_value.stdout = "same-commit"
                version, _ = common.version_stamp()
                common.verify_loaded_inputs(dict(modPath=str(loaded)), version)
                fixture.write_bytes(b"changed fixture")
                changed, _ = common.version_stamp()
                self.assertEqual(changed["modSha256"], version["modSha256"])
                self.assertNotEqual(changed["sha256"], version["sha256"])
                (loaded / "Locales/notice.json").write_text('{"notice":"旧文"}', encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "模组代码或资源与录制输入不同"):
                    common.verify_loaded_inputs(dict(modPath=str(loaded)), changed)

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
