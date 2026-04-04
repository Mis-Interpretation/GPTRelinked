# TurnCombat 架构

```
BattleBootstrap (MonoBehaviour, 场景入口)
  └─> BattleSystem (MonoBehaviour, 编排器)
        ├── BattleSessionData       (纯数据容器)
        ├── BattleRules             (静态纯函数)
        ├── BattleMoveExecutor      (招式执行，纯 C#)
        ├── BattleAIController      (AI 意图/行动，纯 C#)
        │     └── BattleLLMBrain    (MonoBehaviour, LLM 通信)
        │           ├── LLMAgent: aiAgent   → 招式选择 / 意图预测
        │           └── LLMAgent: chatAgent → 对话回应 + buff/捕获率
        ├── BattleUnit × 2          (MonoBehaviour, 精灵图显示)
        └── BattleUI (MonoBehaviour, UI 面板/事件路由)
              ├── BattleHUD × 2     (HP/EXP/状态/意图)
              ├── BattleDialogBox   (打字机对话框)
              ├── MoveButtonUI      (招式按钮 Prefab)
              └── PartyButtonUI     (队伍按钮 Prefab)
```

## 文件清单

| 文件 | 类型 | 职责 |
|---|---|---|
| `Enums.cs` | enum 集合 | ElementType / MoveCategory / StatusCondition / StatType / BattleState |
| `MonsterData.cs` | ScriptableObject | 怪兽基础配置（种族值、可学招式、捕获率、性格等），附带 BaseStats / LearnableMove 结构体 |
| `MoveData.cs` | ScriptableObject | 招式配置（威力、属性、命中、状态效果、能力变化、捕获率修正），附带 StatStageChange 结构体 |
| `TypeChart.cs` | ScriptableObject | 18×18 属性克制表 |
| `Monster.cs` | 运行时类 | 怪兽运行时实例：当前 HP、PP、能力等级、经验值、升级、学招 |
| `MoveSlot.cs` | 运行时类 | 单个招式槽，持有当前 PP |
| `BattleSessionData.cs` | 纯数据容器 | 一场战斗的全部可变状态（队伍、回合、对话历史、捕获奖励等），附带 ChatExchange 结构体 |
| `BattleRules.cs` | 静态工具类 | 先手判定、索引校验、随机 AI 招式、BattleContext 构建、状态/属性名称映射 |
| `DamageCalculator.cs` | 静态工具类 | 伤害公式（含暴击/STAB/属性克制/Vulnerable/烧伤减攻）+ 命中判定，附带 DamageResult 结构体 |
| `CatchCalculator.cs` | 静态工具类 | 捕获概率（含状态加成、对话奖励、摇晃次数），附带 CatchResult 结构体 |
| `ExpCalculator.cs` | 静态工具类 | 经验值获取公式 |
| `BattleMoveExecutor.cs` | 纯 C# 类 | 招式执行（伤害/状态/能力变化）、回合末状态结算，提供 LastCatchRateModifier |
| `BattleAIController.cs` | 纯 C# 类 | 封装 AI 意图预取与行动请求，桥接 BattleLLMBrain ↔ BattleSessionData |
| `BattleLLMBrain.cs` | MonoBehaviour | 管理两个 LLMAgent（aiAgent / chatAgent），构建 prompt、解析 JSON 回复，附带 BattleContext / AIActionResponse / ChatBuffResult |
| `BattleSystem.cs` | MonoBehaviour | 唯一编排入口：协程驱动回合流程、UI 事件绑定/路由、对话处理、捕获/切换/战败/胜利逻辑 |
| `BattleBootstrap.cs` | MonoBehaviour | 场景入口：从 Inspector 配置创建 Monster 实例并调用 BattleSystem.StartBattle，附带 MonsterEntry 结构体 |
| `BattleUI.cs` | MonoBehaviour | UI 总控：Action/Move/Party/Chat 四个面板的显示切换，向上暴露事件 |
| `BattleHUD.cs` | MonoBehaviour | 单个怪兽 HUD：名称、等级、HP 条动画、EXP 条、状态标签、意图文本 |
| `BattleDialogBox.cs` | MonoBehaviour | 打字机效果对话框 |
| `BattleUnit.cs` | MonoBehaviour | 怪兽精灵图设置（正面/背面） |
| `MoveButtonUI.cs` | MonoBehaviour | 招式按钮 Prefab（名称 + PP） |
| `PartyButtonUI.cs` | MonoBehaviour | 队伍成员按钮 Prefab |
| `LLMResponseFormat.ts` | 参考文件 | 记录 LLM 回复的 JSON 格式定义（ChatBuffResult / AIActionResponse） |

## 数据层
- **MonsterData / MoveData / TypeChart** — ScriptableObject，策划在 Inspector 中配置
- **BaseStats / LearnableMove / StatStageChange** — 值类型结构体，嵌在 SO 内
- **Monster / MoveSlot** — 运行时实例，持有当前 HP、PP、能力等级、经验等可变数据
- **BattleSessionData / ChatExchange** — 一场战斗的全部可变状态 + 对话记录

## 规则层
- **BattleRules** — 先手判定、索引校验、随机 AI 招式选择、BattleContext 构建、对话历史拼接、状态/属性名称映射
- **DamageCalculator** — 伤害公式（物理/特殊、暴击、STAB、属性克制、烧伤、Vulnerable、随机浮动）+ 命中判定
- **CatchCalculator** — 捕获概率（基础捕获率 + 对话奖励 + 状态乘数 → 摇晃判定）
- **ExpCalculator** — 经验值获取公式

## 执行层
- **BattleMoveExecutor** — 执行招式（异常状态检查 → PP 消耗 → 命中判定 → 伤害/状态/能力变化）、回合末异常结算（中毒/烧伤/Vulnerable 消退）
- **BattleAIController** — 意图预取（FetchEnemyIntent）、行动请求（RequestAIMove）、事件回调桥接

## LLM 通信层
- **BattleLLMBrain** — 管理 aiAgent（招式决策）和 chatAgent（对话回应）
  - `RequestAIAction` / `RequestIntent` → 构建战斗状态 prompt → 解析 `AIActionResponse { moveIndex }`
  - `RequestChatResponse` → 构建对话 prompt → 解析 `ChatBuffResult { response, buffTarget, statType, stages, catchRateModifier }`
  - `SetEnemyPersonality` → 为 chatAgent 设置怪兽性格 system prompt

## 编排层
- **BattleSystem** — 唯一 MonoBehaviour 入口
  - 驱动回合流程协程（Setup → PlayerAction → ExecuteTurn / Chat / Switch / Catch → BattleEnd）
  - 绑定/解绑 UI 事件（Fight / Talk / Switch / Catch / Move / Party / Chat / Back）
  - 对话结果处理（buff/debuff + 捕获率修正）
  - 战斗结束（经验结算、升级、学招、胜/败判定）

## UI 层
- **BattleUI** — 面板切换（Action / Move / Party / Chat）、事件聚合向上暴露
- **BattleHUD** — HP 条动画、EXP 条、等级、状态图标、意图预览文本
- **BattleDialogBox** — 打字机逐字显示对话
- **BattleUnit** — 精灵图正面/背面切换
- **MoveButtonUI / PartyButtonUI** — 可复用按钮 Prefab

## 回合流程概览

```
PlayerActionPhase（显示行动面板，预取敌方意图）
  │
  ├─ Fight → 选择招式 → ExecuteTurn
  │    ├─ 先手判定（优先度 > 速度）
  │    ├─ 双方依次出招（含命中、伤害、状态、能力变化）
  │    ├─ 招式捕获率修正累积
  │    └─ 回合末异常结算 → 检查战斗结束
  │
  ├─ Talk → 输入对话 → ChatCoroutine
  │    ├─ LLM 生成对话回应
  │    ├─ 应用 buff/debuff + 捕获率修正
  │    └─ 返回行动阶段（不消耗回合）
  │
  ├─ Switch → 选择队员 → SwitchMonsterCoroutine
  │    ├─ 切换精灵、重置能力等级
  │    ├─ 敌方获得免费出招回合
  │    └─ 异常结算 → 检查战斗结束
  │
  └─ Catch → PlayerCatchCoroutine
       ├─ 捕获判定（摇晃动画）
       ├─ 成功 → 战斗胜利
       └─ 失败 → 敌方出招 → 异常结算 → 检查战斗结束
```
