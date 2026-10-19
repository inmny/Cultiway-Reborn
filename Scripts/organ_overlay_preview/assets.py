"""原版动物动画的资源定位；不再镜像游戏内的组合规则。"""
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
GAME_ASSETS = REPO_ROOT / ".GameSource/Assets/Resources/actors"
SPECIES_DIRS = {sp: f"species/animals/{sp}" for sp in
                ("wolf", "bear", "turtle", "fox", "crocodile", "frog",
                 "rabbit", "rat", "chicken", "sheep", "penguin", "snake")}
SPECIES_DIRS["dragon"] = "../actors_special"
SPECIES_SPRITE = {sp: "walk_1" for sp in SPECIES_DIRS}
SPECIES_SPRITE["dragon"] = "dragon_fly1"
