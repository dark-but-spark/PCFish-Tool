# 版本与偏移

> 适用：Unity 6000.0.76f1 / IL2CPP metadata 31.1 / Steam 版 `PCFish.exe`。
> 游戏更新后，以下 RVA / 偏移可能变化；请用 Il2CppDumper + Ghidra 重新校准。

## 关键函数

| 功能 | 方法 | RVA |
| --- | --- | --- |
| 标准合成自动添加 | `NN.PF.UI.Merge.UIMerge.AutoParent` | `0x5F6290` |
| 候选池 | `NN.PF.Core.Util.GameUtil.GetFishList` | `0x61D5B0` |
| 繁育稀有度预测 | `NN.PF.UI.Breed.UIBreedForecast.OnPointerEnter` | `0x60CA10` |
| 合成稀有度预测 | `NN.PF.UI.Merge.UIMergeForecast.OnPointerEnter` | `0x5F4DB0` |
| 累积权重轮盘（死代码） | `NN.PF.Core.Util.GameUtil.Probability` | `0x61EF60` |
| 服务器鱼解析 | `NN.PF.Core.Network.NetworkManager.NewFish` | `0x621320` |
| 繁育请求 | `NetworkManager.FishBreed` | `0x61FA10` |
| 合成请求 | `NetworkManager.FishMerge` | `0x620080` |
| 配方合成请求 | `NetworkManager.FishCraft` | `0x61FD40` |
| 繁殖次数恢复 | `GameDataManager.CheckBreedTimestamp` | `0x6158B0` |
| 繁殖次数上限 | `GameDataManager.AddBreedCount` | `0x615490` |
| 合成槽位校验 | `UIMerge.SetParent` | `0x5F8080` |
| 配方自动填充 | `UICraft.SetRecipe` | `0x5D5EC0` |

`AutoParent` 内调用 `GetFishList` 的指令在 `0x5F63A8`，返回地址 `0x5F63AD`。

## 字段偏移

### `UIMerge`

| 字段 | 偏移 |
| --- | --- |
| `mergeFishList`（`string[10]`） | `0x78` |
| `mergeType`（0=Standard, 1=Season） | `0xB8` |
| `fishType` | `0xC0` |
| `grade` | `0xC8` |

### `FishModel`

| 字段 | 偏移 |
| --- | --- |
| `Id`（Il2CppString*） | `0x10` |
| `Fish` | `0x18` |
| `Type` | `0x20` |
| `Grade`（星级） | `0x28` |
| `Level`（稀有度） | `0x2C` |
| `Growth` | `0x30` |
| `BreedCount` | `0x34` |
| `BreedMaxCount` | `0x38` |
| `IsPlaced` | `0x3C` |
| `IsLocked` | `0x3D` |
| `BreedDatetime` | `0x48` |
| `BreedNextDatetime` | `0x50` |

### `List<T>`

| 字段 | 偏移 |
| --- | --- |
| `_items`（T[]） | `0x10` |
| `_size`（int） | `0x18` |
| `_version`（int） | `0x1C` |
| 数组元素起始 | `0x20` |
| 数组长度 | `items + 0x18` |

### `Il2CppString`

| 字段 | 偏移 |
| --- | --- |
| `length`（int） | `0x10` |
| UTF-16 字符 | `0x14` |

## 常量

| 常量 | 值 |
| --- | --- |
| `CF_BREED_MAX_COUNT` | 5 |
| `CF_BREED_COUNT_INTERVAL` | 420.0 s |
| `CF_FISH_PARENT_COOLTIME` | 3.0 s |
| `CF_FISH_CHILD_COOLTIME` | 7.0 s |
| `CF_FISH_MAX_COUNT` | 999 |
| `CF_FISH_GROWTH_MAX` | 10 |
| `MAX_MERGE_COUNT` | 10 |
