# 境界光晕贴图生成器

## 功能说明

`generate_aura_sprites.py` 用于自动生成境界视觉表现系统中的光晕贴图。

## 光晕设计特点

### 视觉效果
- **圆形光晕**：围绕角色身体的圆形光效
- **径向渐变**：中心最亮，边缘逐渐透明
- **柔和边缘**：使用高斯模糊和衰减函数，边缘自然过渡
- **半透明效果**：支持Alpha通道，可叠加显示

### 境界差异
| 境界 | 颜色 | 柔和度 | 视觉效果 |
|-----|------|--------|---------|
| 练气 | 淡白色 (#FFFFFF) | 1.2 | 微弱、柔和 |
| 筑基 | 淡蓝色 (#87CEEB) | 1.3 | 稳定、清晰 |
| 金丹 | 金黄色 (#FFD700) | 1.5 | 明亮、外显 |
| 元婴 | 紫色 (#9370DB) | 1.6 | 较强、神识外放 |
| 化神 | 纯白色 (#FFFFFF) | 1.8 | 耀眼、法则共鸣 |

## 使用方法

### 1. 生成所有境界的光晕（推荐）

```bash
python Scripts/generate_aura_sprites.py --all --size 128
```

这会生成所有5个境界的光晕贴图到 `GameResources/cultiway/special_effects/aura/` 目录。

### 2. 生成自定义光晕

```bash
python Scripts/generate_aura_sprites.py --custom \
    --size 128 \
    --color "#FFD700" \
    --output "custom_aura.png" \
    --softness 1.5
```

参数说明：
- `--size`: 贴图尺寸（默认128，建议64或128）
- `--color`: 颜色（十六进制格式，如 `#FFD700` 或 `#FFFFFFAA`）
- `--output`: 输出文件路径
- `--softness`: 边缘柔和度（1.0-2.5，值越大边缘越柔和）

### 3. 指定输出目录

```bash
python Scripts/generate_aura_sprites.py --all \
    --size 128 \
    --output-dir "GameResources/cultiway/special_effects/aura"
```

## 技术实现

### 算法原理

1. **距离计算**：计算每个像素到图像中心的距离
2. **归一化**：将距离归一化到 0-1 范围
3. **衰减函数**：使用幂函数 `alpha = (1 - distance)^softness` 创建平滑衰减
4. **噪点添加**：添加轻微随机噪点，使光晕更自然
5. **高斯模糊**：应用轻微模糊，进一步柔化边缘

### 数学公式

```
distance = sqrt((x - center_x)² + (y - center_y)²)
normalized = distance / max_radius
alpha_base = 1.0 - clamp(normalized, 0, 1)
alpha = (alpha_base)^softness + noise
color = base_color × alpha
```

## 依赖库

```bash
pip install Pillow numpy
```

## 输出文件

生成的文件会保存到 `GameResources/cultiway/special_effects/aura/` 目录：

- `qi_aura.png` - 练气境界光晕
- `foundation_aura.png` - 筑基境界光晕
- `jindan_aura.png` - 金丹境界光晕
- `yuanying_aura.png` - 元婴境界光晕
- `huashen_aura.png` - 化神境界光晕

## 自定义调整

如果需要调整光晕效果，可以修改脚本中的参数：

1. **softness（柔和度）**：
   - 较小值（1.0-1.2）：边缘较硬，光晕更集中
   - 中等值（1.3-1.6）：平衡效果（推荐）
   - 较大值（1.7-2.5）：边缘很柔和，光晕范围更大

2. **噪点强度**：修改 `noise = np.random.normal(0, 0.02, ...)` 中的 `0.02` 值
   - 较小值：更平滑
   - 较大值：更有质感

3. **模糊半径**：修改 `ImageFilter.GaussianBlur(radius=1.0)` 中的 `1.0` 值
   - 较小值：更清晰
   - 较大值：更柔和

## 注意事项

1. 生成的贴图是PNG格式，支持透明度
2. 建议使用128x128或64x64尺寸，过大的尺寸可能影响性能
3. 颜色值建议使用纯色，系统会根据透明度自动调整显示效果
4. 如果生成的光晕太亮或太暗，可以调整配置文件中的 `alpha_range` 参数

