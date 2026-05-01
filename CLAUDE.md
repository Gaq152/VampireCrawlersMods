# EvoTrackerMod 开发指南

## 项目概述

本模组是为《吸血鬼爬行者》(Vampire Crawlers) 开发的 BepInEx IL2CPP 模组，用于追踪卡牌进化配方。
游戏基于 Unity 6000.0.62f1 + IL2CPP 构建，BepInEx 版本为 6.0.0-be.755。

## 游戏核心命名空间

游戏主程序集为 `Pancake.dll`，所有游戏类型位于以下命名空间：

| 命名空间 | 用途 |
|---------|------|
| `Nosebleed.Pancake.GameConfig` | 卡牌配置、卡组定义（CardConfig, CardGroup 等） |
| `Nosebleed.Pancake.Models` | 游戏模型层（PlayerModel, CardModel 等） |
| `Nosebleed.Pancake.Modal` | 弹窗/模态框（ChooseCardModal 等） |
| `Nosebleed.Pancake.View` | 视图层（CardView, HandPileView 等） |

## 核心类型详解

### CardGroup（卡组）

定义一组同类卡牌，是进化配方的基本单位。

```
命名空间：Nosebleed.Pancake.GameConfig
继承：UnityEngine.ScriptableObject
```

**关键属性：**
- `string AssetId` — 卡组唯一标识（如 `"CardGroup_Axe"`）
- `string groupName` — 内部名称（如 `"Axe"`）
- `bool HasEvolution` — 是否参与进化配方
- `CardConfig EvolvedCardConfig` — 进化后的卡牌配置（多个卡组可指向同一张进化卡）
- `CardGroupStatus Status` — 卡组状态（Level, Count, TimeEnteredDeck）

**获取方式：**
```csharp
var allGroups = Resources.FindObjectsOfTypeAll<CardGroup>();
```

**进化配方构建逻辑：** 遍历所有 CardGroup，筛选 `HasEvolution == true` 的，按 `EvolvedCardConfig` 的 InstanceID 分组，每组构成一个进化配方。共享同一个 EvolvedCardConfig 的卡组就是该配方的组件。

### CardConfig（卡牌配置）

定义单张卡牌的静态配置。

```
命名空间：Nosebleed.Pancake.GameConfig
继承：UnityEngine.ScriptableObject
```

**关键属性：**
- `CardGroup cardGroup` — 所属卡组
- `LocalizedString NameLoc` — 本地化名称（通过 Unity.Localization）
- `int manaCost` — 法力费用

**获取本地化名称：**
```csharp
var text = config.NameLoc?.GetLocalizedString();
// 注意：部分卡牌返回 "No translation found for 'DEBUG_NOT_USED' in Common"
// 需要用 IsValidLocalizedText() 过滤
```

**获取方式：**
```csharp
var allConfigs = Resources.FindObjectsOfTypeAll<CardConfig>();
```

### PlayerModel（玩家模型）

当前战斗中的玩家状态，核心数据源。

```
命名空间：Nosebleed.Pancake.Models
```

**关键方法：**
- `int GetOwnedCardCount(CardGroup group)` — 获取玩家当前持有某卡组的卡牌数量（实时）
- `int GetOwnedCardCount(CardConfig config)` — 获取玩家当前持有某张具体卡牌的数量（用于判断进化卡是否存在）
- `void Update()` — 每帧调用，可用于 Harmony Postfix 捕获 PlayerModel 实例

**关键字段：**
- `Dictionary<CardGroup, CardGroupStatus> _cardGroupStatuses` — 卡组历史交互记录（注意：这是历史记录不是实时拥有，不适合判断当前持有）

**获取方式：** 通过 Harmony Patch `PlayerModel.Update` 的 Postfix 捕获 `__instance`。

**重要提醒：** 判断卡牌是否拥有必须使用 `GetOwnedCardCount()`，不能依赖 `_cardGroupStatuses`，因为后者记录的是历史交互（曾经拥有过就会存在），不反映消耗/销毁后的实时状态。

### CardGroupStatus（卡组状态结构体）

```
命名空间：Nosebleed.Pancake.GameConfig
类型：struct
```

**字段：**
- `int Level` — 等级
- `int Count` — 数量（历史记录值，非实时）
- `float TimeEnteredDeck` — 进入牌组的时间

### ChooseCardModal（选卡弹窗）

升级时弹出的卡牌选择界面。

```
命名空间：Nosebleed.Pancake.Modal
```

**关键方法/属性：**
- `void OnOpened()` — 弹窗打开时调用
- `void OnClosed()` — 弹窗关闭时调用
- `void PopulateCardRewardChoices()` — 填充可选卡牌列表
- `Il2CppSystem.Collections.Generic.List<CardChoiceView> _cardChoiceViews` — 可选卡牌的视图列表
- `PlayerModel _playerModel` — 当前玩家模型引用

### CardChoiceView（选卡视图）

选卡弹窗中单张卡牌的视图。

```
命名空间：Nosebleed.Pancake.View（推测）
```

**关键属性：**
- `CardConfig CardConfig` — 该选项对应的卡牌配置
- 继承自 `UnityEngine.Component`，可通过 `transform` 获取世界坐标

## Harmony Patch 要点

### 已使用的 Patch 点

| 目标方法 | Patch 类型 | 用途 |
|---------|-----------|------|
| `PlayerModel.Update` | Postfix | 首次捕获 PlayerModel 实例；延迟重试构建配方 |
| `PlayerModel.OnEncounterStarted` | Postfix | 战斗开始时刷新 PlayerModel 引用和配方 |
| `ChooseCardModal.OnOpened` | Postfix | 标记选卡面板打开状态；获取 PlayerModel 引用 |
| `ChooseCardModal.OnClosed` | Postfix | 标记选卡面板关闭；清空选项列表 |
| `ChooseCardModal.PopulateCardRewardChoices` | Postfix | 捕获 3 张可选卡牌的 Config 和 Transform |

### IL2CPP 注意事项

- 所有 IL2CPP 对象访问必须 try-catch，对象可能随时被 GC 销毁
- `ClassInjector.RegisterTypeInIl2Cpp<T>()` 注册自定义 MonoBehaviour 时，带有非 IL2CPP 类型参数的方法会产生 Warning（如 `unsupported parameter EvoTrackerPlugin`），但不影响功能
- `Resources.FindObjectsOfTypeAll<T>()` 可获取所有已加载的 ScriptableObject（包括 CardGroup、CardConfig）
- IL2CPP List 使用 `Count` 和索引器 `[i]` 访问，不支持 LINQ 直接操作

## UI 方案（Unity IMGUI）

使用 `MonoBehaviour.OnGUI()` 绘制覆盖层 UI，这是 BepInEx IL2CPP 模组中最兼容的 UI 方案。

**关键约束：**
- 所有 GUIStyle 只能在 `OnGUI` 内首次初始化（需要 `GUI.skin` 上下文）
- `Texture2D` 用于创建纯色背景，需设置 `hideFlags = HideFlags.HideAndDontSave` 防止被 Unity 回收
- 分辨率适配通过 `_scale = Screen.height / 1080f` 缩放所有尺寸
- 字体支持有限：★ ✓ ✗ → 等基本 Unicode 可用，⚑ 等扩展符号显示为方块
- `GUI.color` 可全局调整透明度，影响后续所有绘制，用完需恢复

## DLL 解析工具

之前通过 `System.Reflection.MetadataLoadContext` 创建了一个控制台工具解析游戏 DLL，用于在没有游戏运行的环境下获取类型信息。

**方式：**
```csharp
// 需要 NuGet 包：System.Reflection.MetadataLoadContext
var resolver = new PathAssemblyResolver(dllPaths);
using var mlc = new MetadataLoadContext(resolver);
var asm = mlc.LoadFromAssemblyPath("Pancake.dll");
// 然后遍历类型、方法、属性等
```

**DLL 位置：** `<游戏目录>/BepInEx/interop/Pancake.dll`（BepInEx 首次启动时从 IL2CPP 生成）

**常用 interop DLL：**
- `Pancake.dll` — 游戏主逻辑
- `UnityEngine.CoreModule.dll` — Unity 核心
- `UnityEngine.IMGUIModule.dll` — IMGUI 系统
- `Unity.Localization.dll` — 本地化系统
- `Il2Cppmscorlib.dll` — IL2CPP 标准库

## 已知的进化配方（v1.6.0 实机验证）

共 19 个配方，来自 110 个卡组：

| 组件 | 进化结果 |
|------|---------|
| 斧头 + 蜡烛 | 死亡旋风 |
| 十字飞镖 + 四叶草 | 天堂之剑 |
| 黑翼 + 白鸽 | 摧锋 |
| 麻雀小八 + 图菲罗街市 + 提拉吉苏 | 菲拉吉 |
| 火焰魔杖 + 菠菜 | 地狱火 |
| 大蒜 + 愈伤番茄 | 噬魂纹章 |
| 彪悍猫 + 石面具 | 饕恶魔眼 |
| 国王圣经 + 咒缚盒 | 渎神祷书 |
| 飞刀 + 护腕 | 千刃 |
| 闪电戒指 + 双鸾宝镯 | 雷环 |
| 强能魔杖 + 空白之书 | 神授魔杖 |
| 五芒星 + 王冠 | 华月纹章 |
| 痕印石 + 防御 | 爆裂 |
| 圣徒水 + 念力法球 | 圣徒精华 |
| 暗影螺钉 + 翅膀 | 瓦尔基里喷枪 |
| 神鞭 + 空虚之心 | 血笞 |
| 时钟柳叶刀 + Gold Ring + Silver Ring | InfiniteCorridor（未解锁） |
| 魔力歌谣 + 疯狂骷髅 | 黑乐谱 |
| Vento Sacro + 血笞 | Fuwalafuwaloo（未解锁） |

> 注意：最后两个配方的进化卡名称未翻译（NameLoc 返回 DEBUG_NOT_USED），显示为内部名称 + "(未解锁)"。

## 构建环境

- .NET SDK 6.0+ （当前机器有 8.0.404 和 9.0.305）
- 目标框架：`net6.0`
- 游戏目录（含 BepInEx interop DLL）：默认 `D:\steam\steamapps\common\Vampire Crawlers`
- csproj 支持自动回退：游戏目录不存在时使用本地 `BepInEx/` 下的 DLL
