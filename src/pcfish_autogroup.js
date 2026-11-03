'use strict';

/*
 * PCFish Tool - 标准合成自动分组
 *
 * 作用：
 *   点“自动添加”时，只选同 Level（稀有度）+ 同 Grade（星级）的鱼。
 *   如果槽位里已经有鱼，则用第一条作为“样板鱼”，自动补齐同组。
 *
 * 版本适配：
 *   Unity 6000.0.76f1 / IL2CPP metadata 31.1 / Steam 版 PCFish.exe
 *   游戏更新后请重新校准 docs/OFFSETS.md 中的 RVA / 偏移。
 */

const CFG = {
  // 0 = 自动；1..5 强制指定（优先级最高）
  targetLevel: 0,          // 1 Common / 2 Uncommon / 3 Rare / 4 Legendary / 5 Mythic
  targetGrade: 0,          // 1..5 星

  // 没有样板鱼时的选组方式：
  // 'largest' = 数量最多的组（默认）
  // 'first'   = 候选池第一条鱼所在的组
  // 'highest' = 稀有度/星级最高的组
  // 'lowest'  = 稀有度/星级最低的组
  modeWhenNoSample: 'largest',

  requireTenWhenNoSample: true, // 没样板时优先只考虑 >=10 条的组
  strictSample: true,           // 有样板时严格使用样板组（不足10条也只用这组）
  fallbackWhenNotEnough: false, // 没有任何 >=10 的组时：true=退回原版混选；false=仍用最大组（可能不足10条）
  log: true,
};

function start() {
  const mod = Process.findModuleByName('GameAssembly.dll');
  if (!mod) {
    setTimeout(start, 1000);
    return;
  }

  const base = mod.base;
  const RVA_GETFISHLIST = 0x61d5b0;
  const RVA_AUTOPARENT = 0x5f6290;
  const RVA_GETFISHLIST_CALLSITE_RET = 0x5f63ad; // AutoParent 内 call GetFishList 的返回地址

  let inStandardAuto = false;
  let sampleId = null;

  function readIl2CppString(p) {
    try {
      if (p.isNull()) return null;
      const len = p.add(0x10).readS32();
      if (len < 0 || len > 512) return null;
      let s = '';
      for (let i = 0; i < len; i++) {
        s += String.fromCharCode(p.add(0x14 + i * 2).readU16());
      }
      return s;
    } catch (e) {
      return null;
    }
  }

  // UIMerge.AutoParent(UIMerge* this)
  Interceptor.attach(base.add(RVA_AUTOPARENT), {
    onEnter(args) {
      inStandardAuto = false;
      sampleId = null;
      try {
        const self = args[0];
        const mergeType = self.add(0xb8).readS32(); // 0 = Standard, 1 = Season
        if (mergeType !== 0) {
          return; // Season 保持原版
        }
        inStandardAuto = true;

        // 读取当前槽位里第一条已选的鱼，作为样板
        const arr = self.add(0x78).readPointer(); // mergeFishList: string[10]
        if (!arr.isNull()) {
          const n = arr.add(0x18).readS32();
          for (let i = 0; i < Math.min(n, 10); i++) {
            const sp = arr.add(0x20 + i * 8).readPointer();
            if (!sp.isNull()) {
              const s = readIl2CppString(sp);
              if (s && s.length) {
                sampleId = s;
                break;
              }
            }
          }
        }

        if (CFG.log) {
          console.log('[AutoGroup] Standard, sample = ' + (sampleId || 'none'));
        }
      } catch (e) {
        console.log('[AutoGroup] onEnter error: ' + e);
      }
    },
    onLeave() {
      inStandardAuto = false;
      sampleId = null;
    },
  });

  // GameUtil.GetFishList() -> List<FishModel>
  Interceptor.attach(base.add(RVA_GETFISHLIST), {
    onEnter() {
      // 只处理 AutoParent 内部这一次调用；其他调用（UIReddot 等）保持原样
      this.isAutoParentCall = false;
      try {
        this.isAutoParentCall =
          this.returnAddress.equals(base.add(RVA_GETFISHLIST_CALLSITE_RET));
      } catch (e) {
        // 忽略
      }
    },
    onLeave(retval) {
      if (!inStandardAuto || !this.isAutoParentCall) return;
      try {
        const list = retval;
        if (list.isNull()) return;

        const items = list.add(0x10).readPointer(); // List<FishModel>._items
        const size = list.add(0x18).readS32();      // List<FishModel>._size
        if (items.isNull() || size <= 0) return;

        const cap = items.add(0x18).readS32();       // T[] length
        const n = Math.min(size, cap);

        const groups = new Map();
        let sampleGroup = null;

        for (let i = 0; i < n; i++) {
          const fish = items.add(0x20 + i * 8).readPointer();
          if (fish.isNull()) continue;

          const grade = fish.add(0x28).readS32(); // FishModel.Grade
          const level = fish.add(0x2c).readS32(); // FishModel.Level
          if (level <= 0 || level > 5 || grade < 0 || grade > 5) continue;

          const key = level * 10 + grade;
          let g = groups.get(key);
          if (!g) {
            g = { level: level, grade: grade, fish: [] };
            groups.set(key, g);
          }
          g.fish.push(fish);

          if (sampleId) {
            const id = readIl2CppString(fish.add(0x10).readPointer()); // FishModel.Id
            if (id === sampleId) sampleGroup = g;
          }
        }

        if (groups.size === 0) return;

        let chosen = null;

        // 1) 配置强制指定
        if (CFG.targetLevel > 0 && CFG.targetGrade > 0) {
          chosen = groups.get(CFG.targetLevel * 10 + CFG.targetGrade) || null;
          if (!chosen && CFG.log) {
            console.log('[AutoGroup] target L' + CFG.targetLevel + ' G' + CFG.targetGrade + ' not found');
          }
        }

        // 2) 样板鱼所在组
        if (!chosen && sampleGroup && CFG.strictSample) {
          chosen = sampleGroup;
        }
        if (!chosen && sampleId && !sampleGroup && CFG.log) {
          console.log('[AutoGroup] sample not found in filtered pool');
        }

        // 3) 自动选组
        if (!chosen) {
          let cands = Array.from(groups.values());
          if (CFG.requireTenWhenNoSample) {
            const ten = cands.filter((g) => g.fish.length >= 10);
            if (ten.length > 0) {
              cands = ten;
            } else if (CFG.fallbackWhenNotEnough) {
              if (CFG.log) console.log('[AutoGroup] no group with >=10, fallback to original');
              return;
            }
          }

          if (CFG.modeWhenNoSample !== 'first') {
            cands.sort((a, b) => {
              if (CFG.modeWhenNoSample === 'highest') {
                return (b.level - a.level) || (b.grade - a.grade) || (b.fish.length - a.fish.length);
              }
              if (CFG.modeWhenNoSample === 'lowest') {
                return (a.level - b.level) || (a.grade - b.grade) || (b.fish.length - a.fish.length);
              }
              // largest
              return (b.fish.length - a.fish.length) || (b.level - a.level) || (b.grade - a.grade);
            });
          }
          chosen = cands[0];
        }

        if (!chosen) return;

        // 原地重排：只保留选中的组，原版 AutoParent 会从头部取 10 条
        for (let i = 0; i < chosen.fish.length; i++) {
          items.add(0x20 + i * 8).writePointer(chosen.fish[i]);
        }
        list.add(0x18).writeS32(chosen.fish.length);

        if (CFG.log) {
          console.log(
            '[AutoGroup] pick L' + chosen.level + ' G' + chosen.grade + ' x' + chosen.fish.length +
              (chosen.fish.length < 10 ? '  (不足10条，Merge 会保持禁用)' : '')
          );
        }
        send({
          type: 'autogroup',
          level: chosen.level,
          grade: chosen.grade,
          count: chosen.fish.length,
        });
      } catch (e) {
        console.log('[AutoGroup] onLeave error: ' + e);
      }
    },
  });

  console.log('[AutoGroup] loaded, GameAssembly base = ' + base);
}

start();
