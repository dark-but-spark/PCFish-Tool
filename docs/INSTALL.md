# 安装与使用

## 系统要求

- Windows 10 / 11（x64）
- Python 3.10+（加入 PATH）
- Steam 版《PC FISH》已启动并进入鱼缸 / 主界面

## 安装

1. 从 Releases 下载 `PCFish-Tool-vX.Y.Z.zip` 并解压。
2. 打开命令行，进入解压目录：
   ```powershell
   python -m pip install -r tools/requirements.txt
   ```

## 启动自动分组

1. 启动游戏，等待进入主界面（`GameAssembly.dll` 已加载）。
2. 双击 `run.cmd`，或：
   ```powershell
   python tools/attach.py
   ```
3. 看到 `[AutoGroup] loaded` 后回到游戏。
4. 合成界面：
   - **推荐**：先手动放 1 条样板鱼，再点“自动添加”。
   - 或直接点“自动添加”，默认选数量最多的同 `(Level, Grade)` 组。

## 配置

编辑 `src/pcfish_autogroup.js` 顶部 `CFG`：

| 字段 | 说明 |
| --- | --- |
| `targetLevel` | 0=自动；1..5 强制稀有度 |
| `targetGrade` | 0=自动；1..5 强制星级 |
| `modeWhenNoSample` | `largest` / `first` / `highest` / `lowest` |
| `requireTenWhenNoSample` | 没样板时优先 `>=10` 条的组 |
| `strictSample` | 有样板时严格使用样板组 |
| `fallbackWhenNotEnough` | 没有 `>=10` 的组时是否退回原版混选 |
| `log` | 控制台日志 |

## 存档分析

```powershell
python tools/pcfish_save.py
```

常用参数：

```powershell
# 预览指定稀有度/星级
python tools/pcfish_save.py --target 2 2

# 用样板鱼预览
python tools/pcfish_save.py --sample FS00019_03_02_02

# 自定义存档路径
python tools/pcfish_save.py --save "D:\path\SaveFile.es3"
```

默认路径：

```text
%USERPROFILE%\AppData\LocalLow\NANOO COMPANY Inc_\PCFish\SaveFile.es3
%USERPROFILE%\AppData\LocalLow\NANOO COMPANY Inc_\PCFish\Setting.es3
```

## 常见问题

**Frida 附加失败 / 找不到 PCFish.exe**

- 确认游戏已经启动，并且进程名是 `PCFish.exe`。
- 用 `frida-ps | findstr -i fish` 查看 PID，然后 `frida -p <PID> -l src/pcfish_autogroup.js`。
- 以管理员身份运行终端（部分环境需要）。

**脚本加载了但点自动添加没变化**

- 确认当前是 **Standard（普通合成）**，不是 Season。
- 查看 Frida 控制台是否输出 `[AutoGroup] pick Lx Gy xN`。
- 游戏更新后 RVA / 偏移可能失效，需要重新校准 `docs/OFFSETS.md`。

**选中的组不足 10 条**

- Merge 按钮会保持禁用（这是原版行为）。
- 换一个更大的组，或设置 `fallbackWhenNotEnough: true` 退回原版混选。

**存档分析报缺少模块**

```powershell
python -m pip install pycryptodome
```
