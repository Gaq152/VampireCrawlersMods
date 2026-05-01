# EvoTrackerMod - 进化配方追踪

为《吸血鬼爬行者》(Vampire Crawlers) 制作的 BepInEx IL2CPP 模组，实时追踪卡牌进化配方的收集进度。

## 功能特性

### 进化配方面板
- 自动扫描游戏内所有进化配方（支持 2、3 及以上组件的配方）
- 实时显示每个配方的部件收集状态和持有数量
- 进化完成的配方以金色标记，部件被合成消耗后仍正确显示"已完成"
- 支持滚轮滚动浏览
- 鼠标离开面板时自动降低透明度，减少遮挡

### 选卡标记
- 选卡界面自动标记与进化配方相关的卡牌，三种状态区分：
  - **★ 需要**（红底）：缺少该部件
  - **✓ 已有(xN)**（绿底）：已持有 N 张该部件
  - **★ 已进化(xN)**（金底）：配方已进化完成
- 标记始终显示，配方集齐后不会消失

### 配方标记（收藏）
- 右键点击面板中的配方行：全选/全取消该配方下所有原料的标记
- 右键点击原料行：单独切换该原料的标记
- 被标记的行右侧显示金色指示条
- 选卡时被标记的卡牌以紫色高亮 + `[!]` 前缀醒目提示，方便快速选择

### 操作方式
| 操作 | 功能 |
|------|------|
| 左键点击按钮 | 打开/关闭面板 |
| 右键拖拽按钮 | 调整按钮位置 |
| F8 | 快捷切换面板开关 |
| 滚轮（面板上） | 滚动浏览配方列表 |
| 右键点击配方行 | 标记/取消整个配方 |
| 右键点击原料行 | 标记/取消单个原料 |

## 安装

### 1. 安装 BepInEx 框架

本模组需要 BepInEx 6.0.0-be.755 IL2CPP 版本，请先下载对应平台的压缩包：

- **Windows x64**：[BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a.zip](https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755%2B3fab71a.zip)
- **macOS x64**：[BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.755+3fab71a.zip](https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.755%2B3fab71a.zip)
- **其他平台**：[BepInEx Bleeding Edge 下载总站](https://builds.bepinex.dev/projects/bepinex_be)

解压后将**包内所有文件和文件夹**复制到游戏根目录（不要把外层文件夹本身丢进去）。正确结果是游戏根目录同时有 `BepInEx/`、`dotnet/`、`winhttp.dll`、`doorstop_config.ini` 等文件。

> 首次启动游戏时，BepInEx 会自动下载 Unity 基础库并生成互操作文件，可能需要等待几十秒到几分钟。日志出现 `Downloading unity base libraries`、`Running Cpp2IL` 等信息都是正常现象，请耐心等待。

### 2. 安装模组

将本仓库 `plugins/` 目录下的 `EvoTrackerMod.dll` 复制到游戏目录的 `BepInEx/plugins/` 中。

> 如果下载后的 DLL 文件名被引号包裹或后缀异常，请先修正文件名再放入 plugins 目录。

### 3. 启动游戏

进入战斗后模组自动生效，左上角会出现"进化"按钮。

> 显示建议：推荐使用 `1920x1080` + `窗口化` 游玩，HUD 显示位置和观感更稳定。

## 构建（开发者）

构建需要游戏本体提供的 BepInEx interop DLL（Pancake.dll 等游戏程序集）。

### 配置游戏路径

复制 `GameDir.props.example` 为 `GameDir.props`，填入你的游戏安装目录：

```xml
<Project>
  <PropertyGroup>
    <GameDir>D:\steam\steamapps\common\Vampire Crawlers</GameDir>
  </PropertyGroup>
</Project>
```

如果没有安装游戏，也可以指向任意存放 BepInEx 运行时 DLL 的目录（需包含 `BepInEx/core/` 和 `BepInEx/interop/` 子目录）。

`GameDir.props` 已被 `.gitignore` 忽略，不会提交到仓库。

### 编译

```bash
dotnet build
```

构建产物位于 `bin/Debug/net6.0/EvoTrackerMod.dll`。

## 技术信息

- 目标框架：.NET 6.0
- 运行环境：BepInEx 6 IL2CPP + Unity 6000.x
- 核心依赖：Harmony（运行时补丁）、Il2CppInterop（IL2CPP 桥接）
- UI 方案：Unity IMGUI (OnGUI)
- 游戏程序集：Pancake.dll（命名空间 `Nosebleed.Pancake.*`）

详细的游戏数据类型和 API 说明见 [CLAUDE.md](CLAUDE.md)。
