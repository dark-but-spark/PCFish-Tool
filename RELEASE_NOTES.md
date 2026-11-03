# PCFish Tool v0.1.0

首个公开版本：把《PC FISH》的繁育 / 合成机制分析和“标准合成自动分组”工具整理成可复现的发布包。

## 新增

- **标准合成自动分组**（Frida）
  - 先放 1 条样板鱼，再点“自动添加” → 自动补满同 `Level`（稀有度）+ 同 `Grade`（星级）的鱼。
  - 无样板鱼时默认选数量最多的 `(Level, Grade)` 分组；支持 `targetLevel / targetGrade`、`largest / first / highest / lowest` 模式。
  - 保留原版 `AutoParent` 的槽位、音效、计数和按钮状态，不修改游戏文件。
- **存档分析工具** `tools/pcfish_save.py`
  - 解密本机 `SaveFile.es3`，按 `(稀有度, 星级)` 统计鱼群，输出 `>=10` 的组和建议目标。
- **机制文档**
  - 繁育 / 合成 / 配方合成的云端接口与请求 / 响应字段。
  - `breed_level_v1.0` 权重表、平均等级预测、星级服务端来源。
  - 自动添加的候选池过滤条件、顺序、去重和 Season 分支逻辑。
  - 当前版本关键 RVA / 字段偏移表。
- **一键启动** `run.cmd`：检查 Python / Frida，自动查找并附加 `PCFish.exe`。

## 文件

发布包 `PCFish-Tool-v0.1.0.zip` 包含：

```text
PCFish-Tool-v0.1.0/
  README.md
  LICENSE
  RELEASE_NOTES.md
  CHANGELOG.md
  VERSION
  run.cmd
  src/pcfish_autogroup.js
  tools/attach.py
  tools/pcfish_save.py
  docs/ANALYSIS.md
  docs/OFFSETS.md
  docs/INSTALL.md
```

## 使用

1. 解压发布包。
2. 启动《PC FISH》，进入鱼缸 / 主界面。
3. 双击 `run.cmd`（或 `python tools/attach.py`）。
4. 合成界面先放一条样板鱼，再点“自动添加”。

## 注意

- 仅在 Windows x64 + Steam 版《PC FISH》上验证；游戏更新后 RVA / 偏移可能失效。
- 星级结果仍由服务器决定；本工具只控制材料选择。
- 季节配方合成保持原版逻辑，不做改动。
- 仓库和发布包不包含任何游戏文件或用户存档。

## 校验

发布资产附带 `SHA256SUMS.txt`，下载后可校验：

```powershell
Get-FileHash .\PCFish-Tool-v0.1.0.zip -Algorithm SHA256
```
