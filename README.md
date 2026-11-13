# GoToHeaven

一个基于 **团结引擎 / Tuanjie 2022.3.62t7（Unity 2022.3 LTS 分支）** 与 C# 的 2D 平台跳跃游戏框架。
所有美术来自工程内的 `Assets/Scenes/UI/Material`，游戏场景、UI、关卡和音效全部在运行时由代码构建，
不依赖任何预制体或场景序列化引用，因此可以稳定运行在当前这个 GUID 迁移后的工程里。

## 快速开始

1. 用 **Tuanjie Hub** 打开项目目录 `GoToHeaven/GoToHeaven`（`ProjectVersion.txt` 对应 Tuanjie 2022.3.62t7）。
2. 打开 `Assets/Scenes/Boot.scene`（原 `SampleScene` 已重命名并清理）。
3. 点击 **Play**。

> 如果你打开工程时编辑器还停留在旧的 `SampleScene` 上，请重新打开一次 `Assets/Scenes/Boot.scene`。
> 游戏入口由 `GameBootstrap` 在场景加载后自动创建，因此在任何场景按 Play 都能启动，但 Boot 是正式入口。

## 操作

| 按键 | 功能 |
| --- | --- |
| `A` / `D` 或方向键 | 左右移动 |
| `Space` / `W` / `↑` | 跳跃（按住跳得更高；空中可二段跳） |
| `Shift` / `J` | 冲刺（地面或空中一次，带冷却与残影） |
| `S` / `↓` + `Space` | 从单向平台落下 |
| `Esc` | 暂停 / 继续 |
| `R` | 重新开始当前关卡 |
| `Enter` | 主菜单开始 / 结算后继续 |

## 已实现内容

- 角色：加速/减速、土狼时间、跳跃缓冲、可变跳跃高度、二段跳、贴墙滑落、蹬墙跳、冲刺、残影。
- 关卡：草地/石块/砖块主题的地形自动拼块，三个手工关卡，检查点、金币、尖刺、旋转锯、移动平台、跳床、终点门。
- 相机：平滑跟随、朝向前瞻、关卡边界钳制、屏幕震动。
- UI：主菜单、选关（进度、最佳时间、金币、锁定状态）、HUD、暂停、设置、失败/胜利结算、淡入淡出、Toast。
- 音频：全部程序化生成，包含跳跃、金币、受伤、检查点、终点、弹跳、冲刺、点击和循环芯片音乐。
- 存档：`PlayerPrefs` + JSON，保存关卡解锁、最佳时间/金币、总金币、音量与设置。
- 场景：`Boot.scene` 仅包含相机，其余内容在运行时生成。

## 目录结构

```
Assets/
  Editor/
    GoToHeavenEditor.cs          # 导入设置、验证、同步美术、重置存档
    PlayModeSmokeTest.cs         # 批处理 Play Mode 冒烟测试
  Resources/GoToHeaven/
    Art/                          # 从 Material 同步来的运行时可加载美术
    Fonts/kaph_regular.ttf        # Kaph 像素字体（TMP 运行时动态生成字体资源）
    Levels/level01..03.json       # 关卡数据
  Scenes/
    Boot.scene                   # 唯一启动场景
  Scripts/GoToHeaven/
    Core/                        # 启动、状态机、图层
    Data/                        # 关卡数据/加载、存档
    Art/                         # 精灵加载/切帧、程序化美术、动画、特效
    Player/                      # 控制器、生命、动画
    Level/                       # 关卡构建、实体、相机、视差背景
    UI/                          # 运行时 uGUI 界面
    Audio/                       # 程序化音频
  Scenes/UI/Material/            # 原始素材（保留，未修改）
```

## 关卡 JSON 格式

新增关卡只需要把 JSON 放到 `Assets/Resources/GoToHeaven/Levels/`，文件名决定顺序（如 `level04.json`），
无需改代码或场景。字段如下（坐标单位为世界单位，1 单位 = 16 像素）：

```json
{
  "id": "level04",
  "displayName": "New Peak",
  "subtitle": "Short description",
  "theme": "day",                 // day | dusk | night
  "parTime": 60,
  "killY": -16,
  "spawn": { "x": 0, "y": 0.1 },
  "goal": { "x": 40, "y": 0 },
  "platforms": [
    { "x": -8, "y": -3, "width": 20, "height": 3, "type": "grass" },
    { "x": 16, "y": 2, "width": 4, "height": 1, "type": "stone", "oneWay": true }
  ],
  "coins": [ { "x": 2, "y": 2 } ],
  "spikes": [ { "x": 8, "y": 0, "width": 2 } ],
  "saws": [ { "x": 20, "y": 4, "moveX": 4, "moveY": 0, "speed": 1.5, "phase": 0 } ],
  "movers": [ { "x": 24, "y": 0, "width": 3, "height": 1, "moveX": 5, "moveY": 0, "speed": 1.3, "phase": 0, "type": "stone" } ],
  "trampolines": [ { "x": 30, "y": 0, "power": 27 } ],
  "checkpoints": [ { "x": 26, "y": 0 } ]
}
```

约定：

- `platforms` / `movers` 的 `x, y` 是**左下角**；`spawn` / `checkpoints` / `goal` / `trampolines` 是**脚底中心**。
- `height` 建议使用整数；地形按 1 单位一格拼接。
- 单向平台 `oneWay: true` 支持 `S + Space` 下落。
- `saws`/`movers` 的 `moveX, moveY` 是正弦往返的位移，`phase` 是相位（0 表示从起点出发）。
- 三个示例关卡都经过跳跃距离/高度校核，可作为数值参考。

## 添加/替换美术

运行时可加载的副本位于 `Assets/Resources/GoToHeaven/Art/`，原始素材仍保留在
`Assets/Scenes/UI/Material/Pixel Adventure 1/Free/`。如果替换了原始素材，使用编辑器菜单：

- `GoToHeaven/Sync Art From Material`：重新同步所需素材到 Resources。
- `GoToHeaven/Fix Art Import Settings`：把资源设为 Sprite、Point 过滤、16 PPU、无压缩。
- `GoToHeaven/Validate Project`：检查图层、场景、关卡 JSON 与贴图导入。
- `GoToHeaven/Reset Save Data`：清除解锁与设置。
- 批处理 Play Mode 冒烟测试：见 `Assets/Editor/PlayModeSmokeTest.cs` 顶部注释。

美术包与字体的授权文件：`Assets/ThirdParty/Kaph_Font/LICENSE.txt`（Kaph 字体）。
Pixel Adventure 素材包由用户提供，原始文件与说明保留在 `Assets/Scenes/UI/Material`。

## 设计说明

- **无场景依赖**：所有 GameObject、UI、Canvas、EventSystem、相机跟随与音频源都在运行时创建。
  这是为了避开当前工程中预制体 GUID 失效的问题（见下）。
- **图层**：`Ground`、`Player`、`Hazard`、`Collectible`、`OneWay` 已写入 `TagManager`；
  若图层尚未重载，`GameLayers` 会用数字索引兜底。
- **TMP 字体**：运行时读取 `Kaph-Regular.ttf` 并通过 `TMP_FontAsset.CreateFontAsset` 生成动态字体；
  失败时回退到项目自带的 LiberationSans SDF。
- **存档 key**：`GoToHeaven.Save.v1`。

## 关于被删除的起始模块

原工程里 `Assets/Scenes/UI/Material/startUI.prefab` 引用的 5 个 GUID（按钮精灵、自身 Prefab GUID 等）
在工程中全部不存在，`SampleScene` 中的 PrefabInstance 也因此是断的；它无法作为可用的启动界面。
该预制体及其场景实例已删除，`SampleScene.scene` 重命名为 `Assets/Scenes/Boot.scene` 并清理为纯相机启动场景。
`Assets/Scenes/UI/Material` 中的原始美术全部保留。

## 验证

- 运行时代码通过独立 Roslyn 编译（引用 Tuanjie 的 UnityEngine/UI/TextMeshPro 程序集），无错误。
- 关卡 JSON 通过结构化校验（3 个关卡、平台尺寸、出生点支撑面、ID 唯一性）。
- 在隔离副本上用团结引擎批处理跑通 **Play Mode 冒烟测试**：
  启动 → 主菜单 → 加载 `level01` → 验证玩家落在生成地形上（`grounded=True`）、
  生命值为 3、计时器推进、TMP 字体/精灵/地形图集加载成功 → 触发通关 → 进入 `Victory` 状态。
  实测输出：`GOTOTHEAVEN_SMOKE_PASS frames=80718 level=0 coins=0 time=1.25 grounded=True health=3`。
  命令见 `Assets/Editor/PlayModeSmokeTest.cs` 顶部注释。
- 冒烟测试在项目副本中运行，不会影响你正在打开的工程；副本与临时分析文件已清理。
