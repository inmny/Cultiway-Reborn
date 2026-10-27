"""下载已核对的 Trellis 512 档量化权重，仅写入项目工具目录。"""
import hashlib
import json
import os
import sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
os.environ["HF_HUB_DISABLE_PROGRESS_BARS"] = "1"
os.environ["HF_XET_HIGH_PERFORMANCE"] = "1"
os.environ["HF_XET_CACHE"] = str(root / "Tools/Trellis/cache/xet")
from huggingface_hub import hf_hub_download

config = json.loads((root / "Scripts/Worldfall/trellis-config.json").read_text(encoding="utf-8"))
destination = root / config["modelDirectory"]
cache = root / "Tools/Trellis/cache"
destination.mkdir(parents=True, exist_ok=True)

for index, model in enumerate(config["models"], 1):
    target = destination / model["name"]
    marker = target.with_suffix(".verified.json")
    if target.is_file() and marker.is_file() and target.stat().st_size == model["size"]:
        verified = json.loads(marker.read_text(encoding="utf-8"))
        if verified.get("sha256") == model["sha256"]:
            print(f"{index}/{len(config['models'])} 已有 {target.name}", flush=True)
            continue
    print(f"{index}/{len(config['models'])} 下载 {model['name']}", flush=True)
    downloaded = Path(hf_hub_download(repo_id=config["modelRepo"], filename=model["remote"],
        cache_dir=str(cache), local_dir=str(destination / "_download")))
    if downloaded.stat().st_size != model["size"]:
        raise RuntimeError(f"文件大小不符：{model['name']}")
    checksum = hashlib.sha256()
    with downloaded.open("rb") as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            checksum.update(chunk)
    if checksum.hexdigest() != model["sha256"]:
        raise RuntimeError(f"文件校验不符：{model['name']}")
    downloaded.replace(target)
    marker.write_text(json.dumps({"sha256": model["sha256"], "size": model["size"]}), encoding="utf-8")
    print(f"已校验 {target.name}", flush=True)
print("Trellis 512 档几何与贴图模型准备完成", flush=True)
