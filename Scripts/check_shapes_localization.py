#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""比较 item_shapes 文件夹和 shapes.csv 的差异"""
import csv
from pathlib import Path

# 获取所有文件夹
shapes_dir = Path("GameResources/cultiway/icons/item_shapes")
folders = [d.name for d in shapes_dir.iterdir() if d.is_dir() and d.name != "others"]

# 读取 shapes.csv
csv_file = Path("Locales/shapes.csv")
existing_keys = set()
if csv_file.exists():
    with open(csv_file, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            key = row['key']
            if key.startswith('Cultiway.ItemShape.'):
                # 提取文件夹名（首字母小写）
                shape_name = key.replace('Cultiway.ItemShape.', '')
                existing_keys.add(shape_name.lower())

print("文件夹列表:")
for folder in sorted(folders):
    print(f"  {folder}")

print("\nshapes.csv 中已有的条目:")
for key in sorted(existing_keys):
    print(f"  {key}")

print("\n缺少的本地化条目:")
missing = []
for folder in sorted(folders):
    if folder.lower() not in existing_keys:
        missing.append(folder)
        print(f"  {folder}")

if missing:
    print(f"\n需要添加 {len(missing)} 个条目")

