# PCFish Tool v0.2.0-beta.1

公测版：在 v0.1.0 的基础上增加 **EXE 安装器**、**结果动画跳过**，并补齐繁育/合成动画分析。

> 这是 beta 版本。游戏更新后 RVA / 偏移可能失效；请以游戏内实际表现和 Frida 控制台日志为准。

## 新增

- **EXE 安装器** `PCFishTool-Setup-v0.2.0-beta.1.exe`
  - 自动检测 Steam 库中的《PC FISH》目录（可手动浏览）。
  - 安装到 `%LOCALAPPDATA%\PCFishTool`，无需管理员权限。
  - 创建桌面 / 开始菜单快捷方式，支持静默安装与卸载。
  - 安装包内已包含独立运行所需的运行时，不需要用户自己安装 Python/Frida。
- **结果动画跳过** `skipResultFx`
  - 繁育 / 合成在服务器响应返回后，立即调用 `UIFishResultFx.FinishFx` 显示结果。
  - 同时把 `GatchaResultFX.effectDuration` 置 0，尽快结束洗牌协程。
  - 动画时长是 prefab 上的固定值，不等待网络；响应只负责触发。
  - 可在 `pcfish_autogroup.js` 的 `CFG.skipResultFx` 关闭。
- **标准合成自动分组**（v0.1.0 已有，beta 继续保留）
  - 先放一条样板鱼，再点自动添加 → 自动补满同 `Level`（稀有度）+ 同 `Grade`（星级）。
  - 无样板时按数量最多的同组选择；支持 `targetLevel / targetGrade` 和多种选组模式。
- **存档分析** `tools/pcfish_save.py`
  - 解密 `SaveFile.es3`，按 `(Level, Grade)` 分组统计，预览自动选择结果。
- **文档**
  - `docs/ANALYSIS.md` 增加“结果动画 / 跳过”流程。
  - `docs/OFFSETS.md` 增加响应回调、FX、onFinishFx 的 RVA / 偏移。

## 安装

1. 下载 `PCFishTool-Setup-v0.2.0-beta.1.exe`。
2. 关闭正在运行的《PC FISH》。
3. 双击安装器，确认游戏目录，点击“安装 / 更新”。
4. 启动游戏，进入鱼缸/主界面，再运行桌面或开始菜单里的 **PCFish Tool**。
5. 合成界面先放一条样板鱼，再点“自动添加”；点“合成/繁育”后结果界面会立即出现。

静默安装：

```powershell
.\PCFishTool-Setup-v0.2.0-beta.1.exe --silent --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH" --no-launch
```

卸载：

```powershell
.\PCFishTool-Setup-v0.2.0-beta.1.exe --uninstall
```

## 文件

| 资产 | 说明 |
| --- | --- |
| `PCFishTool-Setup-v0.2.0-beta.1.exe` | 一键安装器（推荐） |
| `PCFish-Tool-v0.2.0-beta.1.zip` | 绿色版：脚本 + 文档 + 存档分析 |
| `SHA256SUMS.txt` | 校验 |

## 动画分析（简版）

- 结果动画不是“等服务器”：服务器响应回调只负责设置 `UIFishResultFx.onFinishFx` 并激活 FX。
- `GatchaResultFX` 按 prefab 上的固定 `effectDuration`（`+0x30`）播放洗牌，结束时通过 UnityEvent / 动画事件调用 `UIFishResultFx.FinishFx`，再触发 `UIFishResult.Show`。
- 因此响应到达后可以立即结束动画：本版本调用 `FinishFx` 并清空回调，避免动画结束时二次触发。
- 观察点：Frida 控制台出现 `[AutoGroup] skip result FX (breed|merge)`。

## 已知问题

- 安装器未签名，Windows SmartScreen 可能提示“未知发布者”；这是 beta 版的预期行为。
- 游戏启动时可能遇到服务器错误 / 维护；繁育、合成需要 `status == success` 才会触发跳过逻辑，服务器异常时无法测试。
- 星级结果仍由服务器决定；本工具只控制材料选择和结果展示时机。
- 游戏更新后 RVA / 字段偏移可能变化；需要重新校准 `docs/OFFSETS.md`。
- 季节配方合成保持原版逻辑，未做跳过/自动分组之外的改动。

## 校验

发布资产附带 `SHA256SUMS.txt`：

```powershell
Get-FileHash .\PCFishTool-Setup-v0.2.0-beta.1.exe -Algorithm SHA256
Get-FileHash .\PCFish-Tool-v0.2.0-beta.1.zip -Algorithm SHA256
```

## 说明

本项目为个人逆向研究与单机辅助工具，与 NANOO COMPANY 及《PC FISH》官方无关。发布包不包含游戏本体、`GameAssembly.dll`、`global-metadata.dat` 或用户存档。
