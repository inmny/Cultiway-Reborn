#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""重新分类 item_shapes 中的文件：将竹、花、莲、藤分别归类"""
import os
import shutil
from pathlib import Path

# 定义文件名关键词到文件夹的映射规则（优先级从高到低）
KEYWORD_MAPPING = {
    # 竹类
    "竹": "bamboo",
    
    # 花类
    "花": "flower",
    
    # 莲类
    "莲": "lotus",
    
    # 藤类
    "藤": "vine",
    
    # 草类（放在herb中）
    "草": "herb",
}

# 特殊文件名映射（需要明确指定类别）
SPECIAL_MAPPING = {
    "一点红": "herb",  # 草类
    "三化草": "herb",
    "云息草": "herb",
    "冰魄草": "herb",
    "冰齿草": "herb",
    "四叶草": "herb",
    "星落草": "herb",
    "紫茎草": "herb",
    "赤眼草": "herb",
    "黑脉草": "herb",
    
    "冰晶竹": "bamboo",
    
    "火焰花": "flower",
    "蓝月花": "flower",
    "鬼怨花": "flower",
    
    "天山雪莲": "lotus",
    "蓝莲": "lotus",
    
    "长青藤": "vine",
}


def determine_folder(filename):
    """根据文件名确定应该移动到哪个文件夹"""
    # 移除扩展名
    name_without_ext = os.path.splitext(filename)[0]
    
    # 先检查特殊映射
    if name_without_ext in SPECIAL_MAPPING:
        return SPECIAL_MAPPING[name_without_ext]
    
    # 根据关键词匹配（按优先级顺序检查）
    # 注意：需要按优先级顺序，因为一个文件名可能包含多个关键词
    for keyword, folder in KEYWORD_MAPPING.items():
        if keyword in name_without_ext:
            return folder
    
    # 如果都不匹配，返回 None
    return None


def main():
    """主函数：重新分类 herb 文件夹中的文件"""
    base_dir = Path("GameResources/cultiway/icons/item_shapes")
    herb_dir = base_dir / "herb"
    
    if not herb_dir.exists():
        print(f"错误: {herb_dir} 目录不存在")
        return
    
    # 获取所有文件
    files = list(herb_dir.glob("*.png"))
    
    if not files:
        print("herb 文件夹中没有文件")
        return
    
    moved_count = 0
    created_folders = set()
    kept_in_herb = []
    
    for file_path in files:
        filename = file_path.name
        target_folder_name = determine_folder(filename)
        
        # 如果目标文件夹是 herb，则不需要移动
        if target_folder_name == "herb":
            kept_in_herb.append(filename)
            continue
        
        if target_folder_name is None:
            print(f"警告: 无法确定 {filename} 的目标文件夹，保留在 herb 中")
            kept_in_herb.append(filename)
            continue
        
        target_folder = base_dir / target_folder_name
        target_path = target_folder / filename
        
        # 创建目标文件夹（如果不存在）
        if not target_folder.exists():
            target_folder.mkdir(parents=True, exist_ok=True)
            created_folders.add(target_folder_name)
            print(f"创建文件夹: {target_folder_name}")
        
        # 移动文件
        try:
            shutil.move(str(file_path), str(target_path))
            print(f"移动: {filename} -> {target_folder_name}/")
            moved_count += 1
        except Exception as e:
            print(f"错误: 移动 {filename} 失败: {e}")
    
    print(f"\n完成!")
    print(f"移动文件数: {moved_count}")
    print(f"保留在 herb 中的文件数: {len(kept_in_herb)}")
    if kept_in_herb:
        print(f"保留的文件: {', '.join(kept_in_herb)}")
    if created_folders:
        print(f"创建的文件夹: {', '.join(sorted(created_folders))}")


if __name__ == "__main__":
    main()

