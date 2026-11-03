# 安装与使用

## 系统要求

- Windows 10 / 11（x64）
- Steam 版《PC FISH》已启动并进入鱼缸 / 主界面
- 安装器无需 Python / Frida
- 绿色版脚本需要 Python 3.10+

## 方式一：EXE 安装器（推荐）

1. 从 Releases 下载 `PCFishTool-Setup-vX.Y.Z.exe`。
2. 关闭正在运行的《PC FISH》。
3. 双击安装器，确认游戏目录，点击“安装 / 更新”。
4. 启动游戏，进入主界面，再运行桌面 / 开始菜单的 **PCFish Tool**。
5. 合成界面先放一条样板鱼，再点“自动添加”。

静默安装 / 卸载：

```powershell
.\PCFishTool-Setup-v0.2.0-beta.1.exe --silent --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH" --no-launch
.\PCFishTool-Setup-v0.2.0-beta.1.exe --uninstall
```

安装位置：`%LOCALAPPDATA%\PCFishTool`。无需管理员权限。

## 方式二：绿色版（Frida）

1. 解压 `PCFish-Tool-vX.Y.Z.zip`。
2. 安装依赖：

```powershell
python -m pip install -r tools/requirements.txt
```

3. 启动游戏，进入主界面。
4. 运行：

```powershell
python tools/attach.py
```

或双击 `run.cmd`。

## 配置

编辑 `pcfish_autogroup.js` 顶部的 `CFG`：

| 字段 | 说明 |
| --- | --- |
| `targetLevel` | 0=自动；1..5 强制稀有度 |
| `targetGrade` | 0=自动；1..5 强制星级 |
| `modeWhenNoSample` | `largest` / `first` / `highest` / `lowest` |
| `requireTenWhenNoSample` | 没样板时优先 `>=10` 条的组 |
| `strictSample` | 有样板时严格使用样板组 |
| `fallbackWhenNotEnough` | 没有 `>=10` 的组时是否退回原版混选 |
| `skipResultFx` | 服务器响应后立即结束结果动画 |
| `log` | 控制台日志 |

## 存档分析

```powershell
python tools/pcfish_save.py
```

常用参数：

```powershell
python tools/pcfish_save.py --target 2 2
python tools/pcfish_save.py --sample FS00019_03_02_02
python tools/pcfish_save.py --save "D:\path\SaveFile.es3"
```

默认路径：

```text
%USERPROFILE%\AppData\LocalLow\NANOO COMPANY Inc_\PCFish\SaveFile.es3
%USERPROFILE%\AppData\LocalLow\NANOO COMPANY Inc_\PCFish\Setting.es3
```

## 常见问题

**安装器提示未知发布者**

beta 版未签名，SmartScreen 提示属正常现象；可核对 Release 中的 `SHA256SUMS.txt`。

**PCFish Tool 找不到 PCFish.exe**

- 确认游戏已启动，进程名为 `PCFish.exe`。
- 先启动游戏，再运行工具。
- 绿色版可用 `frida-ps | findstr -i fish` 查看 PID。

**点自动添加没变化**

- 确认当前是 Standard（普通合成），不是 Season。
- 查看控制台是否输出 `[AutoGroup] pick Lx Gy xN`。
- 游戏更新后 RVA / 偏移可能失效，需要重新校准 `docs/OFFSETS.md`。

**服务器返回后动画没有跳过**

- 仅在 `NetworkResult.Success` 时跳过；服务器维护 / 网络错误不会触发。
- 确认 `CFG.skipResultFx = true`。
- 查看控制台是否输出 `[AutoGroup] skip result FX (breed|merge)`。

**选中的组不足 10 条**

Merge 会保持禁用。换一个更大的组、关掉 `Unbreedable Fish Only`，或设置 `fallbackWhenNotEnough: true`。

**存档分析报缺少模块**

```powershell
python -m pip install pycryptodome
```
