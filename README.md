<div align="center">

# PCFish Tool

**《PC FISH》非官方研究 / 辅助工具：繁育与合成机制分析 + 标准合成自动分组**

先手动放一条“样板鱼”，再点自动添加：自动补满 **同稀有度 + 同星级** 的鱼。

[![Release](https://img.shields.io/github/v/release/dark-but-spark/PCFish-Tool?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=5682ff)](https://github.com/dark-but-spark/PCFish-Tool/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4.svg)](#系统要求)
[![Build](https://img.shields.io/github/actions/workflow/status/dark-but-spark/PCFish-Tool/build.yml?label=build)](https://github.com/dark-but-spark/PCFish-Tool/actions)

</div>

---

## 简介

本项目基于对 Unity IL2CPP 游戏《PC FISH》（NANOO COMPANY）的反编译分析，提供：

| 模块 | 说明 |
| --- | --- |
| **自动分组** `src/pcfish_autogroup.js` | Frida 脚本。标准合成点“自动添加”时，只选 **同 `Level`（稀有度）+ 同 `Grade`（星级）** 的鱼；支持样板鱼、目标值、数量优先等策略。 |
| **存档分析** `tools/pcfish_save.py` | 解密本机 `SaveFile.es3`，列出鱼、按 `(稀有度, 星级)` 分组统计，帮助选择目标或样板鱼。 |
| **机制文档** `docs/ANALYSIS.md` | 繁育 / 合成 / 配方合成是本地还是云、稀有度权重表、星级来源、自动添加的筛选与顺序。 |
| **偏移表** `docs/OFFSETS.md` | 当前版本的关键 RVA、字段偏移，便于游戏更新后重新校准。 |
| **一键启动** `run.cmd` | 自动检查 Python / Frida，找到 `PCFish.exe` 并附加脚本。 |

> 本项目不包含任何游戏文件（`GameAssembly.dll`、`global-metadata.dat`、存档、CSV 等），只包含自行编写的脚本和文档。

## 功能

- **同稀有度 + 同星级自动添加**
  - 先手动放 1 条目标鱼 → 自动补满 9 条同 `Level`、同 `Grade` 的鱼。
  - 没有样板鱼时，默认选数量最多的 `(Level, Grade)` 分组；也可以配置 `targetLevel / targetGrade` 或切换选组模式。
  - 只影响“投入哪些材料”，不修改游戏文件、不修改存档；合成结果仍由服务器返回。
- **保留原版 UI 流程**：仍然调用原版 `AutoParent`，槽位、音效、`UpdateMergeCount`、按钮状态全部复用。
- **Season 模式不处理**：季节配方合成保持原版按 `Type + Level + Grade` 匹配配方的逻辑。
- **筛选器仍然生效**：候选池先经过游戏自己的 `Unbreedable Fish Only` / `Exclude Placed Fish` / `Rarity Filter` 等过滤，再按 `(Level, Grade)` 分组。
- **可观测**：脚本会把最终选中的 `(Level, Grade)` 和数量打印到 Frida 控制台。

## 快速开始

### 系统要求

- Windows 10 / 11（x64）
- Python 3.10+（加入 PATH）
- 已安装并运行《PC FISH》
- 无需安装 BepInEx / MelonLoader；使用 Frida 动态附加

### 一键使用

1. 从 [Releases](https://github.com/dark-but-spark/PCFish-Tool/releases) 下载最新的 `PCFish-Tool-vX.Y.Z.zip` 并解压。
2. 启动游戏，进入鱼缸 / 主界面（确保 `GameAssembly.dll` 已加载）。
3. 双击 `run.cmd`：
   - 自动检查 Python 和 `frida`；缺少时尝试 `pip install frida`。
   - 找到 `PCFish.exe`，附加 `src/pcfish_autogroup.js`。
4. 回到游戏，在合成界面：
   - **推荐**：先手动放 1 条样板鱼，再点“自动添加” → 自动补满同稀有度 + 同星级。
   - 或者直接点“自动添加” → 默认选数量最多的同组。

手动方式：

```powershell
python -m pip install -r tools/requirements.txt
python tools/attach.py
```

### 配置

编辑 `src/pcfish_autogroup.js` 顶部的 `CFG`：

```js
const CFG = {
  targetLevel: 0,          // 0=自动；1..5 强制稀有度
  targetGrade: 0,          // 0=自动；1..5 强制星级
  modeWhenNoSample: 'largest', // largest | first | highest | lowest
  requireTenWhenNoSample: true,
  strictSample: true,
  fallbackWhenNotEnough: false,
  log: true,
};
```

- `targetLevel/targetGrade` 优先级最高。
- 有样板鱼时，`strictSample: true` 会严格使用样板鱼的 `(Level, Grade)`；不足 10 条时留空，Merge 保持禁用。
- 没样板鱼时，按 `modeWhenNoSample` 选组；`requireTenWhenNoSample` 优先选择 `>=10` 条的组。
- 如果当前筛选下没有 `>=10` 的组：在游戏里关掉 `Unbreedable Fish Only`，或用 `Rarity Filter` 只保留某个稀有度，或先手动选一条数量足够的样板鱼。

## 存档分析

```powershell
python tools/pcfish_save.py
```

输出：

- 当前设置下的候选鱼数量
- 按 `(Level, Grade)` 分组统计
- 所有 `>=10` 条的组
- 默认策略会选中的组
- `--target L G` / `--sample <Fish 或 uid>` 可以预览指定目标

示例：

```powershell
python tools/pcfish_save.py --target 2 2
python tools/pcfish_save.py --sample FS00019_03_02_02
```

## 机制结论（简版）

| 问题 | 结论 |
| --- | --- |
| 繁育 / 合成结果在本地还是云端 | **云端**。`/cs/pcfish/breed/run`、`/merge/run`、`/craft/run` 返回成品鱼；客户端只发鱼 `uid`。 |
| 稀有度（Level） | 繁育按亲本 `Level` 之和查 `breed_level_v1.0` 权重表；普通合成按平均 `Level` 预测 `Level+1`；季节配方直接由 CSV 决定。 |
| 星级（Grade） | **客户端没有计算逻辑**，由服务器返回；客户端只解析 `"grade"`。 |
| 自动添加顺序 | `GameUtil.GetFishList` 过滤后保持存档 `fishList` 顺序，`AutoParent` 从头部取，按 `Id` 去重，填满 10 格。 |
| 普通合成是否要求同种 / 同星 | **不要求**。标准合成可混选；只有季节配方合成按 `Type+Level+Grade` 精确匹配。 |

完整分析见 [`docs/ANALYSIS.md`](docs/ANALYSIS.md)，关键地址见 [`docs/OFFSETS.md`](docs/OFFSETS.md)。

## 构建与发布

版本号唯一来源是仓库根目录的 [`VERSION`](VERSION)。

```powershell
# 本地打包：生成 dist/PCFish-Tool-vX.Y.Z.zip + SHA256SUMS.txt
./scripts/pack-release.ps1
```

- Push 到 `main` / PR：运行 `build.yml`，校验打包产物。
- Push `v*` tag：运行 `release.yml`，自动创建 / 更新 GitHub Release，上传 zip 和 `SHA256SUMS.txt`。

发布流程参考了 [ColoringVixels-Tool](https://github.com/dark-but-spark/ColoringVixels-Tool) 的“VERSION 单一来源 + 打包脚本 + Actions 发布 + SHA256 校验”思路。

## 已知限制

- Frida 脚本依赖当前游戏版本的 RVA / 字段偏移；游戏更新后需要重新用 Il2CppDumper + Ghidra 校准。
- 星级结果仍然在服务端决定；本工具只控制材料选择，不保证星级结果。
- 季节合成不处理。
- 仅支持 Windows x64 + Steam 版《PC FISH》。

## 免责声明

本项目为个人逆向研究与单机辅助工具，与 NANOO COMPANY 及《PC FISH》官方无关。使用本工具可能违反游戏服务条款，请自行评估风险；请勿用于商业用途或破坏他人游戏体验。仓库不包含任何游戏本体文件或用户存档。
