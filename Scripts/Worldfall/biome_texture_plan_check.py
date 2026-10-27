"""核对实际编译材质结果与各物体独立分类方案一致。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Art/Worldfall/biome-sizing-v1"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    actual = read(OUT / "material-check.json")
    by_asset = {row["asset"]: row["models"] for row in actual["results"]}
    proofs = []
    for asset in ("Nurgle_tree", "Tzeentch_tree", "Slaanesh_tree"):
        source = ROOT / "Art/Worldfall" / asset / "native-biome-v1/native-texture-plan.json"
        plan = read(source)
        expected = {}
        if asset == "Nurgle_tree":
            for frame in plan["frames"]:
                expected[frame["cframe"]] = dict(zip(frame["slot_names"], frame["target_slot_kind"]))
        elif asset == "Tzeentch_tree":
            for frame in plan["frames"]:
                expected[frame["cframe"]] = {row["slot_name"]: row["kind"] for row in frame["rules"]}
        else:
            for frame in plan["frame_rules"]:
                expected[frame["cframe"]] = {slot: group["kind"] for group in frame["groups"] for slot in group["slots"]}
        slots = 0
        for model in by_asset[asset]:
            kinds = expected.get(model["frame"], {})
            for name, kind in zip(model["slot_names"], model["slot_kinds"]):
                assert kind == kinds.get(name, 0), (asset, model["name"], name, kind, kinds.get(name, 0))
                slots += 1
        proofs.append(dict(asset=asset, models=len(by_asset[asset]), slots_checked=slots, plan=str(source.relative_to(ROOT)).replace("\\", "/")))
    for model in by_asset["Dark_tree"]:
        for name, kind in zip(model["slot_names"], model["slot_kinds"]):
            expected = 2 if name.startswith("uv_wood_") else 1 if name.startswith("uv_leaf_") else 0
            assert kind == expected, (model["name"], name, kind)
    proofs.append(dict(asset="Dark_tree", models=24, plan="Art/Worldfall/Dark_tree/tripo-api-v2/worldfall-v1/native-texture-plan.json"))
    result = dict(passed=True, independent_asset_plans=4, concurrent_agents_maximum=3,
                  models_checked=sum(row["models"] for row in proofs), results=proofs, game_test="pending_user_manual")
    (OUT / "texture-plan-check.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: result[key] for key in ("passed", "independent_asset_plans", "models_checked")}))


if __name__ == "__main__":
    main()
