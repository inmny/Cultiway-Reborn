#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""根据文件名将 others 文件夹中的贴图移动到对应的形状文件夹"""
import os
import shutil
from pathlib import Path

# 定义文件名关键词到文件夹的映射规则（优先级从高到低）
KEYWORD_MAPPING = {
    # 果实类
    "果": "fruit",
    "桃": "fruit",
    "葡萄": "fruit",
    "柿": "fruit",
    "荔": "fruit",
    
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
    
    # 菌类
    "菇": "mushroom",
    "灵芝": "mushroom",
    "芝": "mushroom",
    
    # 根茎类
    "参": "root",
    "根": "root",
    
    # 木类
    "木": "wood",
}

# 特殊文件名映射（需要明确指定类别）
SPECIAL_MAPPING = {
    # 草类
    "一点红": "herb",
    "三化草": "herb",
    "云息草": "herb",
    "冰魄草": "herb",
    "冰齿草": "herb",
    "四叶草": "herb",
    "星落草": "herb",
    "紫茎草": "herb",
    "赤眼草": "herb",
    "黑脉草": "herb",
    
    # 竹类
    "冰晶竹": "bamboo",
    
    # 花类
    "火焰花": "flower",
    "蓝月花": "flower",
    "鬼怨花": "flower",
    
    # 莲类
    "天山雪莲": "lotus",
    "蓝莲": "lotus",
    
    # 藤类
    "长青藤": "vine",
    
    # 果实类
    "万寿桃": "fruit",
    "六叶地黄果": "fruit",
    "朱果": "fruit",
    "火焰果": "fruit",
    "火荔": "fruit",
    "炎精果": "fruit",
    "玉果": "fruit",
    "葡萄": "fruit",
    "金柿": "fruit",
    "黄羽果": "fruit",
    
    # 菌类
    "万年紫芝": "mushroom",
    "三绝菇": "mushroom",
    "千年金芝": "mushroom",
    "白环蓝菇": "mushroom",
    "百年灵芝": "mushroom",
    
    # 根茎类
    "万年血参": "root",
    "千年人参": "root",
    "地灵根": "root",
    "百年人参": "root",
    
    # 木类
    "燃火木": "wood",
}


def determine_folder(filename):
    """根据文件名确定应该移动到哪个文件夹"""
    # 移除扩展名
    name_without_ext = os.path.splitext(filename)[0]
    
    # 先检查特殊映射
    if name_without_ext in SPECIAL_MAPPING:
        return SPECIAL_MAPPING[name_without_ext]
    
    # 根据关键词匹配
    for keyword, folder in KEYWORD_MAPPING.items():
        if keyword in name_without_ext:
            return folder
    
    # 如果都不匹配，返回 None（可能需要手动处理）
    return None


def main():
    """主函数"""
    base_dir = Path("GameResources/cultiway/icons/item_shapes")
    others_dir = base_dir / "others"
    
    if not others_dir.exists():
        print(f"错误: {others_dir} 目录不存在")
        return
    
    # 获取所有文件
    files = list(others_dir.glob("*.png"))
    
    if not files:
        print("others 文件夹中没有文件")
        return
    
    moved_count = 0
    created_folders = set()
    
    for file_path in files:
        filename = file_path.name
        target_folder_name = determine_folder(filename)
        
        if target_folder_name is None:
            print(f"警告: 无法确定 {filename} 的目标文件夹，跳过")
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
    if created_folders:
        print(f"创建的文件夹: {', '.join(sorted(created_folders))}")


if __name__ == "__main__":
    main()

