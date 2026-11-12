# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。
版本号唯一来源是仓库根目录的 [`VERSION`](VERSION) 文件。

## [0.3.0-beta.4] - 2026-11-04

### 新增

- 自动繁育：每 ~20 分钟按正态分布检查繁育次数（最长 30 分钟），自动打开目录/繁育界面并点击繁育。
- 选择规则：同稀有度 + 同星级；优先高星级，其次高稀有度；组内取繁育次数最多和最少的鱼。
- 连续繁育间隔改为 10 秒（正态，σ=2，5–20 秒）。
- 安装器新增“启用自动繁育”复选框（默认勾选）；`UserData/PCFishTool.cfg` 新增 `auto_breed` 等配置。

## [0.3.0-beta.2] - 2026-11-04

### 变更

- 默认隐藏 MelonLoader 控制台、关闭插件日志，启动不再显示调试信息。
- 安装器新增“显示 Debug 日志”复选框（默认不勾选）；勾选后 `hide_console=false`、`debug_log=true`。
- 支持 `--debug-log` 静默参数。
- 安装器写入 `UserData/Loader.cfg` 与 `UserData/PCFishTool.cfg`，控制控制台和插件日志。

## [0.3.0-beta.1] - 2026-11-04

### 变更

- 从 Frida 外部注入切换为 **MelonLoader 0.7.3 + C# 插件**。
- 安装器改为写入游戏目录：MelonLoader、.NET 6 portable runtime、预生成 Il2CppInterop、`Mods/PCFishTool.dll`。
- 插件随游戏启动自动加载，不再需要运行额外工具或监视器。
- 新增 `tools/interopgen`，用于生成 MelonLoader 的 Il2CppInterop 程序集。
- 安装器自动写入 `GameAssemblyHash`，跳过 MelonLoader 自带生成器在本游戏上的卡死步骤。
- 保留自动分组、结果动画跳过、机制文档、偏移表和存档分析。

## [0.2.0-beta.1] - 2026-11-04

### 新增

- EXE 安装器：自动检测 Steam 库、安装到 LocalAppData、创建快捷方式、支持静默安装 / 卸载。
- 结果动画跳过：服务器响应后立即调用 `UIFishResultFx.FinishFx`，并将 `GatchaResultFX.effectDuration` 置 0。
- 文档补充繁育 / 合成结果动画流程与跳过原理。
- 偏移表补充响应回调、FX、`onFinishFx` 的 RVA / 偏移。
- 构建脚本 `scripts/build-installer.ps1`；Actions 同时构建 zip 与安装器。

## [0.1.0] - 2026-11-04

### 新增

- 标准合成自动分组 Frida 脚本：样板鱼优先，自动补满同稀有度 + 同星级。
- 无样板鱼时的选组策略：`largest` / `first` / `highest` / `lowest`。
- 指定目标 `targetLevel` / `targetGrade`；不足 10 条时的严格 / 回退策略。
- 存档分析工具：解密 ES3、按 `(Level, Grade)` 分组统计、预览自动选择结果。
- 机制文档：云端接口、稀有度权重表、星级来源、自动添加过滤与顺序。
- 偏移表、安装 / 使用文档、一键启动脚本。
- GitHub Actions 打包 / 发布流程、`SHA256SUMS.txt` 校验。

## [0.3.0-beta.4] - 2026-11-13

### 修复

- 适配 PC FISH 1.7.0（Unity 6000.0.76f1）：重新生成 Il2CppInterop 程序集。
- 修复游戏小更新后繁育闪退：旧 1.6.2 interop 的 UIBreed 方法 token 已偏移，调用 `Breed`/`SetParent` 会落到错误方法。
- 安装器新增 GameAssembly SHA512 校验；游戏版本不匹配时拒绝安装并提示下载对应版本，避免装入过期 interop。

