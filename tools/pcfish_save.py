#!/usr/bin/env python3
"""Analyze a local PCFish SaveFile.es3 and preview auto-group selection.

The save is Easy Save 3 (AES-128-CBC, PBKDF2-HMAC-SHA1 x100).
The password is a constant embedded in the game build; it is not a user secret.
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import hashlib
import json
import os
import re
import sys
from typing import Any, Dict, Iterable, List, Optional, Tuple

DEFAULT_DIR = os.path.expanduser(
    r"~\AppData\LocalLow\NANOO COMPANY Inc_\PCFish"
)
DEFAULT_SAVE = os.path.join(DEFAULT_DIR, "SaveFile.es3")
DEFAULT_SETTING = os.path.join(DEFAULT_DIR, "Setting.es3")
DEFAULT_PASSWORD = "sksnrpdl#eo#kr"  # GameConfig.CF_ES3_PASSWORD

NUM_KEY_RE = re.compile(r"([\{,])(\d+):")


def die(msg: str, code: int = 1) -> None:
    print(f"[x] {msg}")
    raise SystemExit(code)


def aes_cbc_decrypt(data: bytes, password: str) -> bytes:
    try:
        from Crypto.Cipher import AES  # type: ignore
    except ImportError:
        die("缺少 pycryptodome。请先运行: python -m pip install pycryptodome")

    if len(data) <= 16:
        die("存档长度异常")

    iv, ciphertext = data[:16], data[16:]
    key = hashlib.pbkdf2_hmac("sha1", password.encode("utf-8"), iv, 100, 16)
    plain = AES.new(key, AES.MODE_CBC, iv).decrypt(ciphertext)

    pad = plain[-1]
    if 1 <= pad <= 16:
        plain = plain[:-pad]
    return plain


def load_es3_json(path: str, password: str) -> Dict[str, Any]:
    if not os.path.isfile(path):
        die(f"找不到文件: {path}")
    raw = open(path, "rb").read()
    text = aes_cbc_decrypt(raw, password).decode("utf-8-sig", errors="replace")
    # ES3 serializes Dictionary<enum, ...> keys as bare numbers.
    text = NUM_KEY_RE.sub(r'\1"\2":', text)
    try:
        return json.loads(text)
    except json.JSONDecodeError as exc:
        die(f"JSON 解析失败: {exc}")


def unwrap(obj: Dict[str, Any]) -> Dict[str, Any]:
    basic = obj.get("Basic")
    if not isinstance(basic, dict):
        die("存档结构异常：缺少 Basic")
    value = basic.get("value")
    if not isinstance(value, dict):
        die("存档结构异常：缺少 Basic.value")
    return value


def parse_dt(value: Any) -> Optional[dt.datetime]:
    if not isinstance(value, str) or not value or value == "unknown":
        return None
    text = value.strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        out = dt.datetime.fromisoformat(text)
    except ValueError:
        return None
    if out.tzinfo is None:
        out = out.replace(tzinfo=dt.timezone.utc)
    return out.astimezone(dt.timezone.utc)


def is_cooling(fish: Dict[str, Any], now: dt.datetime) -> bool:
    for key in ("BreedNextDatetime", "BreedDatetime"):
        value = parse_dt(fish.get(key))
        if value is not None and value > now:
            return True
    return False


def eligible(
    fish: Dict[str, Any],
    settings: Dict[str, Any],
    now: dt.datetime,
    ignore_cooling: bool,
) -> bool:
    if fish.get("Type") == "FS00000":
        return False
    if fish.get("IsLocked"):
        return False
    if not ignore_cooling and is_cooling(fish, now):
        return False

    if settings.get("IsFilterBreedCount") and int(fish.get("BreedCount", 0)) != 0:
        return False
    if settings.get("IsFilterPlaced") and fish.get("IsPlaced"):
        return False

    selected_levels = {
        level
        for level in range(1, 6)
        if settings.get(f"IsFilterLevel{level}")
    }
    if selected_levels and int(fish.get("Level", 0)) not in selected_levels:
        return False

    return True


def group_fish(fish_list: Iterable[Dict[str, Any]]) -> Dict[Tuple[int, int], List[Dict[str, Any]]]:
    groups: Dict[Tuple[int, int], List[Dict[str, Any]]] = collections.OrderedDict()
    for fish in fish_list:
        key = (int(fish.get("Level", 0)), int(fish.get("Grade", 0)))
        groups.setdefault(key, []).append(fish)
    return groups


def print_groups(groups: Dict[Tuple[int, int], List[Dict[str, Any]]], top: int = 40) -> None:
    print("\n== 按 (稀有度, 星级) 分组 ==")
    print(f"{'Level':>5} {'Grade':>5} {'Count':>6}  >=10")
    for (level, grade), fishes in sorted(groups.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        mark = "yes" if len(fishes) >= 10 else ""
        print(f"{level:>5} {grade:>5} {len(fishes):>6}  {mark}")
        top -= 1
        if top <= 0:
            break


def find_sample(
    fish_list: Iterable[Dict[str, Any]], sample: str
) -> Optional[Dict[str, Any]]:
    for fish in fish_list:
        if fish.get("Fish") == sample or fish.get("Id") == sample:
            return fish
    return None


def main() -> None:
    parser = argparse.ArgumentParser(description="PCFish 存档分析 / 自动分组预览")
    parser.add_argument("--save", default=DEFAULT_SAVE, help="SaveFile.es3 路径")
    parser.add_argument("--setting", default=DEFAULT_SETTING, help="Setting.es3 路径")
    parser.add_argument("--password", default=DEFAULT_PASSWORD, help="ES3 密码（默认使用内置常量）")
    parser.add_argument("--target", nargs=2, type=int, metavar=("LEVEL", "GRADE"), help="指定目标稀有度/星级")
    parser.add_argument("--sample", help="样板鱼：Fish 变体 id 或 uid")
    parser.add_argument("--ignore-cooling", action="store_true", help="忽略繁育冷却过滤")
    parser.add_argument("--top", type=int, default=40, help="最多显示多少组")
    args = parser.parse_args()

    save = unwrap(load_es3_json(args.save, args.password))
    setting_obj = unwrap(load_es3_json(args.setting, args.password))
    fish_list: List[Dict[str, Any]] = list(save.get("fishList") or [])
    now = dt.datetime.now(dt.timezone.utc)

    if setting_obj.get("IsFilterSeasonFish") or setting_obj.get("IsFilterSeasonFishMaterial"):
        print("[!] 当前设置启用了季节相关过滤；本工具未加载 fish_type CSV，暂不模拟这两个过滤。")

    pool = [f for f in fish_list if eligible(f, setting_obj, now, args.ignore_cooling)]
    print(f"存档鱼总数: {len(fish_list)}")
    print(f"当前设置下候选鱼: {len(pool)}")
    print(
        "筛选: "
        + ", ".join(
            f"{k}={'on' if v else 'off'}"
            for k, v in setting_obj.items()
            if k.startswith("IsFilter")
        )
    )

    groups = group_fish(pool)
    if not groups:
        die("没有候选鱼")
    print_groups(groups, args.top)

    chosen: Optional[Tuple[int, int]] = None
    reason = ""

    if args.target:
        chosen = (args.target[0], args.target[1])
        reason = "target"
    elif args.sample:
        sample = find_sample(pool, args.sample)
        if sample is None:
            print(f"[!] 候选池里找不到样板: {args.sample}")
        else:
            chosen = (int(sample["Level"]), int(sample["Grade"]))
            reason = "sample"
    else:
        cands = [(k, v) for k, v in groups.items() if len(v) >= 10]
        if not cands:
            cands = list(groups.items())
        chosen = max(cands, key=lambda kv: (len(kv[1]), kv[0][0], kv[0][1]))[0]
        reason = "largest"

    if chosen is None or chosen not in groups:
        die(f"目标分组不存在: {chosen}")

    fishes = groups[chosen]
    print(f"\n== 自动添加将选择 ({reason}) ==")
    print(f"Level={chosen[0]} Grade={chosen[1]}  Count={len(fishes)}")
    for fish in fishes[:10]:
        print(
            f"  {fish.get('Fish')}  uid={fish.get('Id')}  "
            f"BreedCount={fish.get('BreedCount')}/{fish.get('BreedMaxCount')}  "
            f"placed={fish.get('IsPlaced')} locked={fish.get('IsLocked')}"
        )
    if len(fishes) < 10:
        print("[!] 不足 10 条：原版 Merge 会保持禁用；请更换目标或改用 fallback 策略。")


if __name__ == "__main__":
    main()
