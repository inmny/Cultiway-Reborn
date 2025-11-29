#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成境界光晕贴图
根据配置生成不同境界的光晕效果
"""

import os
import sys
from PIL import Image, ImageDraw, ImageFilter
import numpy as np


def hex_to_rgb(hex_color):
    """将十六进制颜色转换为RGB元组"""
    hex_color = hex_color.lstrip('#')
    if len(hex_color) == 8:  # 包含alpha
        return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4, 6))
    elif len(hex_color) == 6:
        rgb = tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))
        return (*rgb, 255)
    else:
        raise ValueError(f"Invalid hex color: {hex_color}")


def generate_aura_sprite(size, color_hex, output_path, softness=1.5):
    """
    生成光晕贴图
    
    Args:
        size: 贴图尺寸 (width, height) 或单个数字（正方形）
        color_hex: 十六进制颜色（如 "#FFFFFFAA" 或 "#FFFFFF"）
        output_path: 输出文件路径
        softness: 边缘柔和度（越大越柔和，建议1.0-2.5）
    """
    if isinstance(size, int):
        size = (size, size)
    
    width, height = size
    center_x, center_y = width / 2, height / 2
    
    # 解析颜色
    rgba = hex_to_rgb(color_hex)
    r, g, b, a = rgba
    
    # 创建RGBA图像
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    
    # 创建numpy数组用于计算
    y, x = np.ogrid[:height, :width]
    
    # 计算每个像素到中心的距离
    distance_from_center = np.sqrt((x - center_x)**2 + (y - center_y)**2)
    
    # 计算最大半径（到边缘的距离）
    max_radius = min(center_x, center_y)
    
    # 归一化距离 (0-1)
    normalized_distance = distance_from_center / max_radius
    
    # 创建alpha通道：中心为1，边缘为0，使用平滑过渡
    # 使用多种函数组合，创建更自然的光晕效果
    alpha_base = 1.0 - np.clip(normalized_distance, 0, 1)
    
    # 使用平滑的衰减函数（类似高斯分布）
    # softness控制衰减速度，值越大边缘越柔和
    alpha = np.power(alpha_base, softness)
    
    # 添加轻微的噪点，使光晕更自然（可选）
    noise = np.random.normal(0, 0.02, (height, width))
    alpha = np.clip(alpha + noise, 0, 1)
    
    # 应用颜色
    r_channel = (r * alpha).astype(np.uint8)
    g_channel = (g * alpha).astype(np.uint8)
    b_channel = (b * alpha).astype(np.uint8)
    a_channel = (a * alpha).astype(np.uint8)
    
    # 组合为RGBA图像
    rgba_array = np.dstack([r_channel, g_channel, b_channel, a_channel])
    img = Image.fromarray(rgba_array.astype(np.uint8), 'RGBA')
    
    # 应用轻微的高斯模糊，使边缘更柔和
    img = img.filter(ImageFilter.GaussianBlur(radius=1.0))
    
    # 保存图像
    img.save(output_path, 'PNG')
    print(f"✓ 生成光晕: {output_path} ({size[0]}x{size[1]}, 颜色: {color_hex})")


def generate_all_auras(output_dir="GameResources/cultiway/special_effects/aura", size=128):
    """
    根据配置文件生成所有境界的光晕
    
    Args:
        output_dir: 输出目录
        size: 贴图尺寸（默认128x128）
    """
    # 确保输出目录存在
    os.makedirs(output_dir, exist_ok=True)
    
    # 根据配置文件定义的光晕参数
    aura_configs = [
        {
            "id": "qi_aura",
            "color": "#FFFFFF",  # 淡白色
            "softness": 1.2,  # 较柔和
            "description": "练气境界光晕 - 微弱的灵气波动"
        },
        {
            "id": "foundation_aura",
            "color": "#87CEEB",  # 淡蓝色
            "softness": 1.3,
            "description": "筑基境界光晕 - 稳定的灵气流转"
        },
        {
            "id": "jindan_aura",
            "color": "#FFD700",  # 金黄色
            "softness": 1.5,
            "description": "金丹境界光晕 - 金丹之光外显"
        },
        {
            "id": "yuanying_aura",
            "color": "#9370DB",  # 紫色
            "softness": 1.6,
            "description": "元婴境界光晕 - 元婴神识外放"
        },
        {
            "id": "huashen_aura",
            "color": "#FFFFFF",  # 纯白色
            "softness": 1.8,  # 最柔和
            "description": "化神境界光晕 - 天地法则共鸣"
        }
    ]
    
    print(f"开始生成境界光晕贴图...")
    print(f"输出目录: {output_dir}")
    print(f"贴图尺寸: {size}x{size}\n")
    
    for config in aura_configs:
        output_path = os.path.join(output_dir, f"{config['id']}.png")
        generate_aura_sprite(
            size=size,
            color_hex=config['color'],
            output_path=output_path,
            softness=config['softness']
        )
        print(f"  {config['description']}\n")
    
    print("所有光晕贴图生成完成！")


def generate_custom_aura(size, color_hex, output_path, softness=1.5):
    """
    生成自定义光晕（命令行工具）
    
    用法:
        python generate_aura_sprites.py --custom --size 128 --color "#FFD700" --output "custom_aura.png" --softness 1.5
    """
    generate_aura_sprite(size, color_hex, output_path, softness)
    print(f"自定义光晕已生成: {output_path}")


if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description='生成境界光晕贴图')
    parser.add_argument('--all', action='store_true', help='生成所有境界的光晕')
    parser.add_argument('--custom', action='store_true', help='生成自定义光晕')
    parser.add_argument('--size', type=int, default=128, help='贴图尺寸（默认128）')
    parser.add_argument('--color', type=str, help='颜色（十六进制，如 #FFD700）')
    parser.add_argument('--output', type=str, help='输出文件路径')
    parser.add_argument('--softness', type=float, default=1.5, help='边缘柔和度（默认1.5）')
    parser.add_argument('--output-dir', type=str, default='GameResources/cultiway/special_effects/aura',
                        help='输出目录（默认：GameResources/cultiway/special_effects/aura）')
    
    args = parser.parse_args()
    
    if args.all:
        # 获取脚本所在目录的父目录（项目根目录）
        script_dir = os.path.dirname(os.path.abspath(__file__))
        project_root = os.path.dirname(script_dir)
        output_dir = os.path.join(project_root, args.output_dir)
        generate_all_auras(output_dir, args.size)
    elif args.custom:
        if not args.color or not args.output:
            print("错误: 自定义模式需要 --color 和 --output 参数")
            sys.exit(1)
        generate_custom_aura(args.size, args.color, args.output, args.softness)
    else:
        # 默认生成所有
        script_dir = os.path.dirname(os.path.abspath(__file__))
        project_root = os.path.dirname(script_dir)
        output_dir = os.path.join(project_root, args.output_dir)
        generate_all_auras(output_dir, args.size)

