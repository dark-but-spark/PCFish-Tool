# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。
版本号唯一来源是仓库根目录的 [`VERSION`](VERSION) 文件。

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
