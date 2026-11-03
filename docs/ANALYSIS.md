# 《PC FISH》繁育 / 合成机制分析

> 目标版本：Unity 6000.0.76f1，IL2CPP metadata 31.1，Steam 版 `PCFish.exe`。
> 方法：Il2CppDumper 6.7.46 + Cpp2IL 2022.1.0-pre-release.21 + ILSpy 11.2 + Ghidra 11.0.3。
> 以下结论来自客户端静态分析；最终结果仍以服务端为准。

## 1. 结论速览

| 问题 | 结论 |
| --- | --- |
| 繁育 / 合成结果在本地还是云端 | **云端**。客户端只发送鱼的 `uid`，服务器返回成品鱼。 |
| 稀有度（Level） | 繁育：按亲本 `Level` 之和查 `breed_level_v1.0` 权重表；普通合成：客户端按平均 `Level` 预测 `Level+1`；季节配方：由 CSV 直接确定。 |
| 星级（Grade） | **客户端没有计算逻辑**，由服务器返回；客户端只解析 `"grade"`。 |
| 普通合成材料限制 | **不要求同种 / 同稀有度 / 同星级**；任意 10 条满足筛选的鱼都可以。 |
| 季节配方合成材料限制 | 必须按配方精确匹配 `Type + Level + Grade`，并满足 `RecipeAmount`。 |
| 自动添加顺序 | 候选池过滤后保持存档 `fishList` 顺序；按 `Id` 去重；填入前 10 个空槽。 |

## 2. 网络接口

| 功能 | 客户端入口 | URL | 请求核心字段 |
| --- | --- | --- | --- |
| 繁育 | `UIBreed.Breed` | `https://gapi.nanoocompany.com/cs/pcfish/breed/run` | `parentFishId[]` |
| 普通合成 | `UIMerge.Merge`（Standard） | `/cs/pcfish/merge/run` | `mergeFishId[]` |
| 季节配方合成 | `UIMerge.Merge`（Season） | `/cs/pcfish/craft/run` | 目标类型 / 星级 + `mergeFishId[]` |
| 服务器恢复 | `NetworkManager.ManageRestore` | `/cs/pcfish/manage/search` | 返回 `exp` + `items` |
| 交易 | `NetworkManager.FishTrade*` | `/cs/pcfish/market/*` | 鱼 `uid` |

请求带 `Authorization: Bearer <Steam 登录 token>` 与 `X-NanooGame-*` 头。响应结构：

```json
{
  "status": "success|maintenance|update|check",
  "data": {
    "uid": "...", "fish": "...", "type": "...",
    "grade": 1, "level": 1, "growth": 10,
    "breedCount": 0, "breedMaxCount": 1,
    "breedDate": "...", "breedNextDate": "...", "timestamp": 0
  }
}
```

客户端 `NetworkManager.NewFish` 只做反序列化并写入 `FishModel`（`Grade` 在 `+0x28`，`Level` 在 `+0x2c`），不计算星级。

## 3. 稀有度（Level）

### 3.1 繁育（2 条亲本）

`UIBreedForecast.OnPointerEnter`：

```text
total = ParentA.Level + ParentB.Level      // 2..10
row   = breed_level_v1.0[total]
P(后代稀有度 = k) = row[k] / 10000
```

权重表：

```csv
total,level1,level2,level3,level4,level5
0,10000,0,0,0,0
1,9000,1000,0,0,0
2,7600,2000,400,0,0
3,4887,4163,950,0,0
4,3772,4428,1500,300,0
5,0,5049,4301,650,0
6,0,4048,4752,1000,200
7,0,0,5211,4439,350
8,0,0,4370,5130,500
9,0,0,0,7000,3000
10,0,0,0,4500,5500
```

`GameUtil.Probability` 是标准累积权重轮盘，但客户端 `CallerCount = 0`（死代码），实际 roll 在服务端。

### 3.2 普通合成（10 条）

`UIMergeForecast.OnPointerEnter`：

```text
avg = sum(Level) / count
lo = clamp(floor(avg) + 1, 0, 5)
hi = clamp(ceil(avg) + 1, 0, 5)
if lo == hi: P(lo) = 100%
else:
    p = round((avg - floor(avg)) * 100)
    P(lo) = 100 - p
    P(hi) = p
```

材料同 `Level` 时（例如 10 条同组），结果稀有度预测为 `Level+1`（封顶 5）。

### 3.3 季节配方合成

由 `fish_season_recipe_v1.1` + `fish_type_v1.5` 直接决定：

```text
结果 Type   = seasonFishType
结果星级     = seasonFishGrade
结果稀有度   = fish_type[seasonFishType].level
材料         = 同 (seasonFishType, seasonFishGrade) 行的
               recipeFishType @ recipeFishGrade × recipeAmount
```

## 4. 星级（Grade）

确定事实：

- 客户端没有任何“计算星级”的代码；`NewFish` 只把服务器 JSON 的 `"grade"` 写到 `FishModel+0x28`。
- 请求只发送鱼的 `uid`；服务器知道每条鱼的 `Level / Grade`。
- 本地没有星级权重表；`UIForecast` 只显示稀有度。

因此星级规则只能通过抓包采样或逆服务端得到。可测试的假设：

1. 普通合成保留材料星级（合成只升稀有度）。
2. 繁育复用同一张权重表，但按 `GradeA + GradeB` 查表。
3. 服务端另有星级表 / 随机规则。

## 5. 自动添加逻辑

### 5.1 入口

`UIMerge.AutoParent`（`0x5F6290`）：

```text
pool = GameUtil.GetFishList()
if pool.Count == 0: toast("msg_noUsableFish"); return

if mergeType == Standard:
    FishBoxInit(); MergeInit();        // 清空，mergeFishList = new string[10]
    slot = 0
    foreach fish in pool:              // 存档顺序
        if mergeFishList.Contains(fish.Id): continue
        slot = first empty slot
        if slot >= 10: break
        mergeFishList[slot] = fish.Id
        listViewItem[slot].Show(fish.Id)
        PlayUI(3)
        slot++
else: // Season
    MergeInit(); uiCraft.Init(); uiCraft.SetRecipe(pool)
UpdateMergeCount(); toast("msg_autoFillComplete")
```

要点：

- **会清空当前选择**。
- 普通合成从候选池头部取，**不按稀有度 / 星级排序**。
- 按 `FishModel.Id`（uid）去重；同一 `Fish` 变体的不同 uid 可以同时入选。
- 最多 10 条。

### 5.2 候选池 `GameUtil.GetFishList`

无条件排除：

- `Type == "FS00000"`（占位鱼）
- `IsLocked == true`
- 繁育冷却中的鱼

筛选开关：

| 开关 | 字段 | 保留条件 |
| --- | --- | --- |
| Unbreedable Fish Only | `IsFilterBreedCount` | `BreedCount == 0` |
| Exclude Placed Fish | `IsFilterPlaced` | `!IsPlaced` |
| Exclude Seasonal Fish | `IsFilterSeasonFish` | `fishType.Season == 0` |
| Exclude Seasonal Fish Material | `IsFilterSeasonFishMaterial` | 不在当前赛季配方材料 `(Type, Grade)` 集合 |
| Rarity Filter | `IsFilterLevel1..5` | `Level` 在勾选集合 |

过滤后的顺序 = `GameDataManager.fishList` 原始顺序（约等于获得顺序，旧的在前）。

### 5.3 Season 分支

`UICraft.Init` 按当前赛季 + 目标 `FishType` + 目标 `Grade` 展开配方槽位；`UICraft.SetRecipe` 对每个槽位找 `Type + Level + Grade` 全匹配的鱼，按 `Id` 去重填充。候选不足时槽位留空，Merge 保持禁用。

## 6. 补充

- 本地存档是 EasySave3 加密（AES-128-CBC，PBKDF2-HMAC-SHA1 100 次），密码硬编码在 `GameConfig`。
- 服务器还提供 `/manage/search` 恢复鱼列表，说明鱼的最终记录在服务端。
- 本文档不包含游戏二进制或完整 CSV；仅保留必要的概率表和偏移。
