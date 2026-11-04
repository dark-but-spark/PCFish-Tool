# PCFish Tool v0.3.0-beta.2

插件版公测：从 Frida 外部注入切换为 **MelonLoader 0.7.3 + C# 插件**。安装器直接写入游戏目录，游戏启动时自动加载，不再需要额外运行工具。

> Beta 版本。安装器包含针对 PC FISH **1.6.2 / Unity 6000.0.76f1** 预生成的 interop 程序集；游戏更新后需要更新安装器。


## 本版本重点

- **默认安静启动**：安装器默认隐藏 MelonLoader 控制台、关闭插件日志，不再弹调试信息。
- **安装器新增“显示 Debug 日志”复选框**（默认不勾选）：勾选后保留 MelonLoader 控制台并开启 `debug_log=true`，方便排查。
- 修复了“每次启动都看到调试控制台”的体验问题。

## 为什么不是 BepInEx

- PC FISH 是 **IL2CPP**，BepInEx 5（参考项目使用的加载器）只支持 Mono，插件不会被加载。
- BepInEx 6 IL2CPP 仍是 pre-release，对 Unity 6000 / metadata 31.1 支持不稳定。
- MelonLoader 0.7.3 明确包含 Unity 6000+ 修复，已在本文游戏上验证可加载插件。

## 新增

- **EXE 安装器（插件版）**
  - 自动检测 Steam 库中的《PC FISH》目录。
  - 安装 MelonLoader 0.7.3、.NET 6 portable runtime、预生成的 Il2CppInterop 程序集、`Mods/PCFishTool.dll`。
  - 自动写入 `GameAssemblyHash`，跳过 MelonLoader 首次运行时容易卡住的 interop 生成步骤。
  - 支持 `--silent`、`--uninstall`、`--force`；需要管理员权限。
  - 已有 `version.dll` / `MelonLoader` 时提示并备份到 `PCFishTool_Backup`。
- **MelonLoader 插件**
  - 游戏启动时自动加载；控制台日志显示 `PCFish Tool loaded`。
  - 标准合成自动分组：先放样板鱼，再点自动添加 → 自动补满同 `Level` + 同 `Grade`。
  - 结果动画跳过：服务器响应成功后立即 `FinishFx`，结果界面立刻出现。
- **interop 生成工具**
  - `tools/interopgen`：独立 C# 工具，用于在开发机生成 `MelonLoader/Il2CppAssemblies`，绕过 MelonLoader 自带生成器在本游戏上的卡死问题。
- 文档、偏移表、构建脚本同步更新。

## 安装

1. 下载 `PCFishTool-Setup-v0.3.0-beta.2.exe`。
2. 关闭正在运行的《PC FISH》。
3. 双击安装器，确认游戏目录，点击“安装 / 更新”（需要管理员权限）。
4. 启动游戏；插件自动加载。
5. 合成界面先放一条样板鱼，再点“自动添加”。

静默安装 / 卸载：

```powershell
.\PCFishTool-Setup-v0.3.0-beta.2.exe --silent --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
.\PCFishTool-Setup-v0.3.0-beta.2.exe --uninstall --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
```

## 文件

| 资产 | 说明 |
| --- | --- |
| `PCFishTool-Setup-v0.3.0-beta.2.exe` | 插件版安装器（推荐） |
| `PCFish-Tool-v0.3.0-beta.2.zip` | 机制文档 / 存档分析 / 旧 Frida 脚本 |
| `SHA256SUMS.txt` | 校验 |

安装后游戏目录新增：

```text
version.dll
MelonLoader/        # 含 net6、Dependencies、Il2CppAssemblies
dotnet/             # .NET 6 portable runtime
Mods/PCFishTool.dll
```

## 已验证

- MelonLoader 0.7.3 在 PC FISH 1.6.2 / Unity 6000.0.76f1 上成功加载。
- Il2CppInterop 程序集生成成功（117 个，含 `Assembly-CSharp.dll`）。
- `PCFishTool.dll` 被 MelonLoader 加载，日志输出 `PCFish Tool loaded`。
- 安装器静默安装 / 卸载在测试副本和 Steam 游戏目录上验证通过。
- 自动分组 / 动画跳过需要在游戏内实际点一次合成/繁育验证；服务器返回非 `Success` 时不会跳过动画。

## 已知问题

- 安装器约 73 MB，包含 .NET 6 运行时和预生成 interop；未签名，SmartScreen 可能提示。
- 预生成 interop 只适用于当前游戏构建；更新游戏后需要重新生成。
- 会写入游戏目录，可能与其它 MelonLoader 插件冲突；安装前会检测并备份。
- 如果游戏启动时服务器维护，合成/繁育无法成功，动画跳过不会触发；自动分组仍可打开。
- 星级结果仍由服务器决定。

## 校验

```powershell
Get-FileHash .\PCFishTool-Setup-v0.3.0-beta.2.exe -Algorithm SHA256
Get-FileHash .\PCFish-Tool-v0.3.0-beta.2.zip -Algorithm SHA256
```

## 说明

本项目为个人逆向研究与单机辅助工具，与 NANOO COMPANY 及《PC FISH》官方无关。发布包不包含游戏本体、`GameAssembly.dll`、`global-metadata.dat` 或用户存档。
