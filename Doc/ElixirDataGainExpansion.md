# 丹药数据增益（DataGain）设计精简版

## 目标与原则
- 目标：DataGain 仅聚焦三类长期效果——永久属性增益、特质获取、一次性操作，方便实现与验证。
- 原则：数据可追踪（UI/存档可展示）、可控上限（防止堆叠失衡）、与 StatusGain/Restore/Change 解耦（避免重复）。

## 类型划分与内容清单
- **永久属性增益**（叠加有上限）
  - 生命上限（hp_max）、悟性（wisdom/intelligence）、寿元（lifespan）
  - 强度：基于丹药 `value` 计算；可配置“同 ID 叠加次数上限/递减倍率”
- **特质获取**（ActorTrait）
  - 体质类：辟谷、耐毒、灵敏、刚健等
  - 心性类：勤学、专注、勇毅、冷静等
  - 限制：同一特质只可获得一次，已有则跳过或转为微量属性补偿
- **一次性操作库**（只执行一次且记录来源）
  - 变性：切换性别
  - 开灵根：添加/刷新灵根
  - 唤醒金丹：授予或强化金丹
  - 其他可拓展动作：洗髓（重置部分负面状态/基础数据）、赐名（重命名+小幅声望）

## LLM 生成流程（两段式）
1. **决策类别**：先让 LLM 在三类中选一（属性增益 / 特质 / 一次性操作）。
2. **细项选择**：根据选出的类别再喂可选列表，引导 LLM 选具体属性/特质/操作。
3. **描述生成**：生成 effect_description 供 UI，本地化 key 可后置或直接用描述。

## 建议的 JSON 模板
- **属性增益**
```json
{
  "effect_type": "DataGain",
  "chosen": "attribute",
  "effect_description": "寿元悠长，心智洞明",
  "attributes": { "hp_max": 30, "wisdom": 5, "lifespan": 20 },
  "max_stack": 3
}
```
- **特质获取**
```json
{
  "effect_type": "DataGain",
  "chosen": "trait",
  "effect_description": "服后体质脱胎换骨",
  "traits": [ "耐毒", "辟谷" ],
  "fallback_attribute": { "hp_max": 10 }
}
```
- **一次性操作**
```json
{
  "effect_type": "DataGain",
  "chosen": "one_time",
  "effect_description": "灵根初开，性命蜕变",
  "operations": [ "change_gender", "open_element_root" ],
  "operation_args": { "element_root": "wood" }
}
```

## 解析与落地要点
- 解析 `chosen` 分支，调用对应处理器：
  - attribute：按 `max_stack` 或递减倍率更新永久属性
  - trait：检查是否已拥有特质；有则跳过并执行 `fallback_attribute`（可选）
  - one_time：逐个执行 `operations`，记录已执行标记防重复
- 存档与展示：
  - 在角色信息中增加“长期数据增益”显示：属性/特质/一次性结果
  - 将已执行的一次性操作写入可序列化列表，避免重复触发
