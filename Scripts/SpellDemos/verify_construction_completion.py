#!/usr/bin/env python3
"""在私有游戏验证按需求补全、正式恢复及已有身体改造。"""
import json
from pathlib import Path

from benchmark_baselines import private_installation
from common import PROJECT, version_stamp, write_json
import verify_view_switch as view


def run_case(name, folder):
    """身体回归只跑当前修改影响的实际编辑、支付及命中，不沿用旧生成器的抽样假设。"""
    script = Path(__file__).with_name(name + ".script")
    if name == "verify_body_editing":
        source, separator, _ = script.read_text().partition("var created=new List<AbilityDefinition>();")
        if not separator:
            raise RuntimeError("身体编辑回归的范围标记已改变")
        source = source.replace("artifacts/skill_rebuild/body-editing", "artifacts/skill_rebuild/construction-completion/body-editing")
        source += '\nvar report=new{passed=true,checks,process=System.Diagnostics.Process.GetCurrentProcess().Id};File.WriteAllText(Path.Combine(folder,"checks.json"),JsonConvert.SerializeObject(report,Formatting.Indented));return report;\n'
        script = folder / "body-editing.script"
        script.write_text(source)
    result = view.baseline.call("run", str(script), "120000")
    count = result["checks"]
    print(json.dumps(dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count)), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("检查未通过：" + name)


def main():
    """加载最终代码，执行专项与受影响的合成、身体改造和派生检查。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/construction-completion"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection))
    for name in ("verify_construction_completion", "verify_definition_layers", "verify_body_editing", "verify_derivation"):
        run_case(name, folder)


if __name__ == "__main__":
    main()
