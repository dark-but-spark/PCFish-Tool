#!/usr/bin/env python3
"""Attach PCFish Tool's Frida script to a running PCFish.exe."""

import os
import sys
import time

if getattr(sys, "frozen", False):
    ROOT = os.path.dirname(sys.executable)
else:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 仓库布局: <root>/src/pcfish_autogroup.js
# 安装布局: <root>/pcfish_autogroup.js
# PyInstaller 内嵌: <_MEIPASS>/pcfish_autogroup.js
_CANDIDATES = [
    os.path.join(ROOT, "src", "pcfish_autogroup.js"),
    os.path.join(ROOT, "pcfish_autogroup.js"),
]
if getattr(sys, "frozen", False):
    _CANDIDATES.append(os.path.join(getattr(sys, "_MEIPASS", ROOT), "pcfish_autogroup.js"))
SCRIPT = next((p for p in _CANDIDATES if os.path.isfile(p)), _CANDIDATES[0])


def fail(msg: str, code: int = 1) -> None:
    print(f"[x] {msg}")
    sys.exit(code)


def main() -> None:
    try:
        import frida
    except ImportError:
        fail("缺少 frida。请先运行: python -m pip install frida", 1)

    if not os.path.isfile(SCRIPT):
        fail(f"找不到脚本: {SCRIPT}")

    device = frida.get_local_device()
    target = None
    for proc in device.enumerate_processes():
        if proc.name.lower() == "pcfish.exe":
            target = proc
            break

    if target is None:
        fail("未找到 PCFish.exe。请先启动游戏并进入鱼缸/主界面，再运行本脚本。", 2)

    with open(SCRIPT, "r", encoding="utf-8") as f:
        source = f.read()

    try:
        session = device.attach(target.pid)
    except Exception as exc:  # noqa: BLE001
        fail(f"附加失败: {exc}", 3)

    script = session.create_script(source)

    def on_message(message, data):
        if message.get("type") == "send":
            print("[AutoGroup]", message.get("payload"))
        elif message.get("type") == "error":
            print("[AutoGroup][error]", message.get("stack") or message.get("description"))

    script.on("message", on_message)

    try:
        script.load()
    except Exception as exc:  # noqa: BLE001
        fail(f"加载脚本失败: {exc}", 4)

    print(f"[*] 已附加到 PCFish.exe (PID {target.pid})。")
    print("[*] 回到游戏：合成界面先放一条样板鱼，再点“自动添加”。")
    print("[*] 按 Ctrl+C 退出（不会关闭游戏）。")

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n[*] 正在断开...")
        session.detach()


if __name__ == "__main__":
    main()
