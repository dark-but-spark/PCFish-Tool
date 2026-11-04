<div align="center">

# PCFish Tool

**《PC FISH》非官方研究 / 辅助工具：MelonLoader 插件版**

安装到游戏目录，启动游戏自动加载：标准合成自动分组（同稀有度 + 同星级）+ 服务器响应后立即显示结果。

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
| **EXE 安装器** | 安装 MelonLoader 0.7.3 + .NET 6 运行时 + 预生成的 Il2CppInterop 程序集 + `Mods/PCFishTool.dll` 到游戏目录。需要管理员权限。 |
| **MelonLoader 插件** | 游戏启动时自动加载，不需要再运行额外工具；自动分组和结果动画跳过都在插件里。 |
| **自动分组** | 标准合成点“自动添加”时，只选 **同 `Level`（稀有度）+ 同 `Grade`（星级）** 的鱼；先放一条样板鱼即可锁定它的星级/稀有度。 |
| **结果动画跳过** | 服务器响应成功后立即调用 `UIFishResultFx.FinishFx`，不再等固定时长的结果动画。 |
| **机制文档** | `docs/ANALYSIS.md`：繁育 / 合成是本地还是云、稀有度权重表、星级来源、自动添加与动画流程。 |
| **偏移表** | `docs/OFFSETS.md`：当前版本关键 RVA / 字段偏移，便于游戏更新后校准。 |

> 本项目不包含游戏本体、`GameAssembly.dll`、`global-metadata.dat` 或用户存档。安装器会把 MelonLoader 和插件写入游戏目录，请自行备份。

## 功能

- **同稀有度 + 同星级自动添加**
  - 先手动放 1 条目标鱼 → 自动补满同 `Level`、同 `Grade` 的鱼。
  - 没有样板鱼时，选数量最多的 `(Level, Grade)` 分组；没有 `>=10` 的组时使用最大组（Merge 可能保持禁用）。
  - 只影响材料选择，不修改存档；合成结果仍由服务器返回。
- **结果动画跳过**
  - 服务器响应成功后直接结束结果动画，结果界面立即出现。
  - 仅在 `NetworkResult.Success` 时生效；服务器维护 / 网络错误不会跳过。
- **随游戏启动加载**
  - 通过 MelonLoader 加载，不需要 Frida、Python 或后台监视器。
- **保留原版 UI 流程**：仍然调用原版 `AutoParent`，槽位、音效、计数和按钮状态全部复用。
- **Season 模式不处理**：季节配方合成保持原版按 `Type + Level + Grade` 匹配配方的逻辑。

## 为什么用 MelonLoader 而不是 BepInEx

| 方案 | 是否可行 | 原因 |
| --- | --- | --- |
| BepInEx 5 | **不可行** | 只支持 Mono 游戏；PC FISH 是 **IL2CPP**，插件 DLL 不会被加载。 |
| BepInEx 6 IL2CPP | **不建议** | 仍是 pre-release，Il2CppInterop 对 **Unity 6000 / metadata 31.1** 支持不稳定。 |
| **MelonLoader 0.7.3** | **可行（本版本采用）** | 明确包含 Unity 6000+ 修复，使用 Il2CppInterop 1.5.1；已在本游戏上验证可加载插件。 |
| Frida 外部注入（v0.2.x） | 可行但不方便 | 不修改游戏目录，但必须额外运行工具，不能随游戏启动。 |

## 快速开始

### 系统要求

- Windows 10 / 11（x64）
- Steam 版《PC FISH》1.6.2（Unity 6000.0.76f1）
- 管理员权限（安装器要写入 `C:\Program Files (x86)\Steam\steamapps\common\PC FISH`）

### 安装

1. 从 [Releases](https://github.com/dark-but-spark/PCFish-Tool/releases) 下载 `PCFishTool-Setup-vX.Y.Z.exe`。
2. 关闭正在运行的《PC FISH》。
3. 双击安装器，确认游戏目录，点击“安装 / 更新”。
4. 启动游戏；MelonLoader 会自动加载插件。
5. 合成界面先放一条样板鱼，再点“自动添加”；点“合成/繁育”后结果界面会立即出现。

静默安装 / 卸载（管理员终端）：

```powershell
.\PCFishTool-Setup-v0.3.0-beta.1.exe --silent --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
.\PCFishTool-Setup-v0.3.0-beta.1.exe --uninstall --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
```

安装内容：

```text
<游戏目录>/
  version.dll
  MelonLoader/
    net6/
    Dependencies/
    Il2CppAssemblies/      # 预生成的 Il2CppInterop 程序集
  dotnet/                  # .NET 6 portable runtime
  Mods/PCFishTool.dll
```

如果游戏目录已有 `MelonLoader` / `version.dll`，安装器会提示；确认覆盖会先备份到 `PCFishTool_Backup`。

## 开发 / 构建

版本号唯一来源是 [`VERSION`](VERSION)。

```powershell
# 1) 在已安装 MelonLoader 的游戏目录上生成 interop（仅开发机需要一次）
#    本仓库的 tools/interopgen 是独立生成器

# 2) 编译插件
dotnet build mods/PCFishTool -c Release -p:GameDir="D:\path\to\PC FISH"

# 3) 打包 EXE 安装器（需要游戏目录里已有 MelonLoader + interop + dotnet）
./scripts/build-installer.ps1 -GameDir "D:\path\to\PC FISH"
```

发布：

- Push 到 `main` / PR：运行 `build.yml`，打包绿色版 zip。
- Push `v*` tag：运行 `release.yml`，创建 / 更新 Release。
- EXE 安装器包含游戏相关的 interop，需在本地构建后手动上传到 Release；CI 不构建安装器。

发布流程参考了 [ColoringVixels-Tool](https://github.com/dark-but-spark/ColoringVixels-Tool) 的“VERSION 单一来源 + 打包脚本 + Actions 发布 + SHA256 校验”思路。

## 机制结论（简版）

| 问题 | 结论 |
| --- | --- |
| 繁育 / 合成结果在本地还是云端 | **云端**。`/cs/pcfish/breed/run`、`/merge/run`、`/craft/run` 返回成品鱼；客户端只发鱼 `uid`。 |
| 稀有度（Level） | 繁育按亲本 `Level` 之和查 `breed_level_v1.0` 权重表；普通合成按平均 `Level` 预测 `Level+1`；季节配方直接由 CSV 决定。 |
| 星级（Grade） | **客户端没有计算逻辑**，由服务器返回；客户端只解析 `"grade"`。 |
| 自动添加顺序 | `GameUtil.GetFishList` 过滤后保持存档 `fishList` 顺序；插件按 `(Level, Grade)` 分组后重排。 |
| 普通合成是否要求同种 / 同星 | **不要求**。标准合成可混选；只有季节配方合成按 `Type+Level+Grade` 精确匹配。 |
| 结果动画 | 固定时长（prefab `effectDuration` + UnityEvent/动画事件），不等待网络；响应后可立即 `FinishFx` 跳过。 |

完整分析见 [`docs/ANALYSIS.md`](docs/ANALYSIS.md)，关键地址见 [`docs/OFFSETS.md`](docs/OFFSETS.md)。

## 已知限制

- 安装器包含针对 **PC FISH 1.6.2 / Unity 6000.0.76f1** 预生成的 interop 程序集；游戏更新后需要重新生成并更新安装器。
- 安装器未签名，Windows SmartScreen 可能提示“未知发布者”。
- 安装器需要管理员权限；会写入游戏目录。
- 如果游戏目录已有其它 MelonLoader 插件，覆盖前会备份，但不同插件可能冲突。
- 星级结果仍由服务器决定；本工具只控制材料选择和结果展示时机。
- 仅支持 Windows x64 + Steam 版《PC FISH》。

## 免责声明

本项目为个人逆向研究与单机辅助工具，与 NANOO COMPANY 及《PC FISH》官方无关。使用本工具可能违反游戏服务条款，请自行评估风险；请勿用于商业用途或破坏他人游戏体验。
