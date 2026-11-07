# 安装与使用

## 系统要求

- Windows 10 / 11（x64）
- Steam 版《PC FISH》1.6.2（Unity 6000.0.76f1）
- 管理员权限（安装器写入游戏目录）

## 安装（推荐）

1. 从 Releases 下载 `PCFishTool-Setup-vX.Y.Z.exe`。
2. 关闭正在运行的《PC FISH》。
3. 双击安装器，确认游戏目录（默认自动检测 Steam 库），点击“安装 / 更新”。
4. 启动游戏；MelonLoader 会自动加载 `Mods/PCFishTool.dll`。
5. 合成界面先放一条样板鱼，再点“自动添加”。

安装器会写入：

```text
<游戏目录>/
  version.dll
  MelonLoader/        # 含 net6、Dependencies、Il2CppAssemblies
  dotnet/             # .NET 6 portable runtime
  Mods/PCFishTool.dll
  PCFishTool.installed
  PCFishTool_README.txt
```


> 安装器默认隐藏 MelonLoader 控制台并关闭插件日志。需要排查时，安装时勾选“显示 Debug 日志”，或修改 `UserData\Loader.cfg` 的 `hide_console=false` 和 `UserData\PCFishTool.cfg` 的 `debug_log=true`。


### 自动繁育

安装器默认勾选“启用自动繁育”，并写入：

```ini
auto_breed=true
auto_breed_first_delay_seconds=60
auto_breed_mean_minutes=20
auto_breed_sigma_minutes=4
auto_breed_min_minutes=10
auto_breed_max_minutes=30
auto_breed_interval_seconds=10
auto_breed_interval_sigma_seconds=2
auto_breed_interval_min_seconds=5
auto_breed_interval_max_seconds=20
```

- 首次检查在 60 秒后，之后按正态分布（均值 20、σ=4）随机，限制 10–30 分钟，最长不超过 30 分钟。
- 有繁育次数时自动使用；只选同 `Level` + 同 `Grade`，优先高 `Grade`，其次高 `Level`；组内选繁育次数最多和最少的两条鱼。
- 连续繁育间隔 10 秒（正态，σ=2，限制 5–20 秒），用完次数后等待下一次检查。
- 关闭：取消安装器复选框，或把 `UserData\PCFishTool.cfg` 的 `auto_breed` 改为 `false`。

### 静默安装 / 卸载

请在管理员 PowerShell 中运行：

```powershell
.\PCFishTool-Setup-v0.3.0-beta.3.exe --silent --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
.\PCFishTool-Setup-v0.3.0-beta.3.exe --uninstall --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
```

如果已有 `version.dll` / `MelonLoader`，安装器会提示；确认后备份到 `PCFishTool_Backup`。

## 使用

- 自动分组：先手动放 1 条目标鱼，再点“自动添加” → 补满同 `Level` + 同 `Grade`。
- 没有样板鱼：选数量最多的同组；没有 `>=10` 的组时使用最大组，Merge 可能保持禁用。
- 结果动画：服务器成功返回后立即显示结果；日志出现 `[PCFishTool] skip result FX (breed|merge)`。
- Season 配方合成保持原版逻辑。

## 日志

```text
<游戏目录>\MelonLoader\Latest.log
```

看到以下内容表示插件加载成功：

```text
[PCFish_Tool] PCFish Tool loaded.
[PCFish_Tool] Auto-group: sample fish first, then Auto Add.
[PCFish_Tool] Result FX skip: enabled.
```

## 常见问题

**安装器提示未知发布者**

Beta 版未签名，SmartScreen 提示属正常现象；可核对 Release 中的 `SHA256SUMS.txt`。

**游戏启动时 MelonLoader 控制台一闪而过 / 没有 Mod**

- 确认游戏目录下存在 `version.dll`、`MelonLoader\`、`dotnet\`、`Mods\PCFishTool.dll`。
- 查看 `MelonLoader\Latest.log` 中的错误。
- 确认没有其它 MelonLoader 版本冲突；安装器会备份旧的 `MelonLoader`。

**点自动添加没变化**

- 确认当前是 Standard（普通合成），不是 Season。
- 查看 `Latest.log` 是否有 `[PCFishTool] pick Lx Gy xN`。
- 游戏更新后 interop 可能失效，需要重新生成安装器。

**服务器返回后动画没有跳过**

- 仅在 `NetworkResult.Success` 时跳过；服务器维护 / 网络错误不会触发。
- 查看日志是否有 `[PCFishTool] skip result FX`。

**想卸载**

使用安装器的“卸载”，或：

```powershell
.\PCFishTool-Setup-v0.3.0-beta.3.exe --uninstall --dir="C:\Program Files (x86)\Steam\steamapps\common\PC FISH"
```

## 开发构建

```powershell
dotnet build mods/PCFishTool -c Release -p:GameDir="D:\path\to\PC FISH"
./scripts/build-installer.ps1 -GameDir "D:\path\to\PC FISH"
```

安装器需要游戏目录里已有 MelonLoader + 生成的 `Il2CppAssemblies` + `dotnet`（可用 `tools/interopgen` 生成）。
