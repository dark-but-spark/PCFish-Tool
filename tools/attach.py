#!/usr/bin/env python3
"""Attach PCFish Tool's Frida script to PCFish.exe.

Usage:
  PCFishTool.exe              # one-shot: start the game first, then run this
  PCFishTool.exe --watch      # keep running; attach whenever PCFish.exe starts
"""

import os
import sys
import threading
import time

if getattr(sys, "frozen", False):
    ROOT = os.path.dirname(sys.executable)
else:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Repository layout: <root>/src/pcfish_autogroup.js
# Installed layout:  <root>/pcfish_autogroup.js
# PyInstaller data:  <_MEIPASS>/pcfish_autogroup.js
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


def find_pid(device):
    try:
        for proc in device.enumerate_processes():
            if proc.name.lower() == "pcfish.exe":
                return proc.pid
    except Exception as exc:  # noqa: BLE001
        print(f"[!] 枚举进程失败: {exc}")
    return None


def attach_once(device, source, pid: int, watch: bool) -> None:
    session = device.attach(pid)
    script = session.create_script(source)

    def on_message(message, data):
        if message.get("type") == "send":
            print("[AutoGroup]", message.get("payload"))
        elif message.get("type") == "error":
            print("[AutoGroup][error]", message.get("stack") or message.get("description"))

    script.on("message", on_message)
    script.load()

    print(f"[*] 已附加到 PCFish.exe (PID {pid})。")
    print("[*] 合成界面先放一条样板鱼，再点“自动添加”。")
    print("[*] 按 Ctrl+C 退出（不会关闭游戏）。")

    if not watch:
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\n[*] 正在断开...")
            session.detach()
        return

    detached = threading.Event()

    def on_detached(*args):
        detached.set()

    try:
        session.on("detached", on_detached)
    except Exception:
        pass

    detached.wait()
    try:
        session.detach()
    except Exception:
        pass
    print("[*] 游戏已退出，继续等待下一次启动...")


def main() -> None:
    try:
        import frida
    except ImportError:
        fail("缺少 frida。请先运行: python -m pip install frida", 1)

    if not os.path.isfile(SCRIPT):
        fail(f"找不到脚本: {SCRIPT}")

    watch = "--watch" in sys.argv[1:]
    with open(SCRIPT, "r", encoding="utf-8") as f:
        source = f.read()

    device = frida.get_local_device()

    while True:
        pid = find_pid(device)
        if pid is None:
            if not watch:
                fail("未找到 PCFish.exe。请先启动游戏并进入鱼缸/主界面，再运行本脚本。", 2)
            print("[*] 监视模式：等待 PCFish.exe 启动...")
            time.sleep(2)
            continue

        try:
            attach_once(device, source, pid, watch)
        except KeyboardInterrupt:
            print("\n[*] 已退出。")
            return
        except Exception as exc:  # noqa: BLE001
            print(f"[!] 附加失败: {exc}")
            try:
                time.sleep(2)
            except KeyboardInterrupt:
                return

        if not watch:
            return
        time.sleep(1)


if __name__ == "__main__":
    main()
