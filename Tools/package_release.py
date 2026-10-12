#!/usr/bin/env python3
"""校验 Unity 桌面包的 CPU 架构并生成保留权限与 UTF-8 文件名的 ZIP。"""

from __future__ import annotations

import argparse
import hashlib
import os
import plistlib
import stat
import subprocess
import zipfile
from pathlib import Path


EXPECTED_AMD64 = 0x8664
EXPECTED_ARM64 = 0xAA64


def read_pe_machine(path: Path) -> int:
    """读取并校验 Windows PE 表头与区段边界，返回 Machine 字段。"""
    data = path.read_bytes()
    if len(data) < 0x40 or data[:2] != b"MZ":
        raise ValueError(f"不是有效的 Windows 可执行文件：{path}")
    pe_offset = int.from_bytes(data[0x3C:0x40], "little")
    if pe_offset + 24 > len(data) or data[pe_offset : pe_offset + 4] != b"PE\0\0":
        raise ValueError(f"PE 签名缺失或文件被截断：{path}")

    machine = int.from_bytes(data[pe_offset + 4 : pe_offset + 6], "little")
    section_count = int.from_bytes(data[pe_offset + 6 : pe_offset + 8], "little")
    optional_size = int.from_bytes(data[pe_offset + 20 : pe_offset + 22], "little")
    optional_offset = pe_offset + 24
    if optional_size < 70 or optional_offset + optional_size > len(data):
        raise ValueError(f"PE Optional Header 无效：{path}")
    optional_magic = int.from_bytes(data[optional_offset : optional_offset + 2], "little")
    if optional_magic != 0x20B:
        raise ValueError(f"要求 PE32+ 64 位程序，实际 Optional Header 为 {optional_magic:#x}：{path}")

    entry_point = int.from_bytes(data[optional_offset + 16 : optional_offset + 20], "little")
    section_offset = optional_offset + optional_size
    if section_offset + section_count * 40 > len(data):
        raise ValueError(f"PE 区段表被截断：{path}")

    entry_point_found = False
    for index in range(section_count):
        offset = section_offset + index * 40
        virtual_size = int.from_bytes(data[offset + 8 : offset + 12], "little")
        virtual_address = int.from_bytes(data[offset + 12 : offset + 16], "little")
        raw_size = int.from_bytes(data[offset + 16 : offset + 20], "little")
        raw_offset = int.from_bytes(data[offset + 20 : offset + 24], "little")
        if raw_size and raw_offset + raw_size > len(data):
            raise ValueError(f"PE 区段超出文件边界：{path}")
        if virtual_address <= entry_point < virtual_address + max(virtual_size, raw_size):
            entry_point_found = True
    if not entry_point_found:
        raise ValueError(f"PE 入口点不在任何已定义区段中：{path}")
    return machine


def require_windows_x64(source: Path) -> None:
    """确认 Unity 玩家、播放器运行时和崩溃处理器都是真正的 AMD64 PE。"""
    for relative in ("RoomDemo.exe", "UnityPlayer.dll", "UnityCrashHandler64.exe"):
        binary = source / relative
        if not binary.is_file():
            raise FileNotFoundError(f"Windows 包缺少必要文件：{binary}")
        machine = read_pe_machine(binary)
        if machine != EXPECTED_AMD64:
            raise ValueError(f"{binary} 架构错误：Machine={machine:#06x}，要求 AMD64 {EXPECTED_AMD64:#06x}")
        print(f"架构校验通过：{relative} -> AMD64 ({machine:#06x})")


def require_macos_arm64(source: Path) -> None:
    """确认 macOS 应用主程序与 UnityPlayer 动态库仅包含 ARM64。"""
    app = source / "RoomDemo.app"
    info_path = app / "Contents" / "Info.plist"
    if not info_path.is_file():
        raise FileNotFoundError(f"macOS 应用或 Info.plist 缺失：{info_path}")
    with info_path.open("rb") as stream:
        info = plistlib.load(stream)
    executable_name = info.get("CFBundleExecutable")
    if not executable_name:
        raise ValueError(f"Info.plist 未声明 CFBundleExecutable：{info_path}")

    candidates = [app / "Contents" / "MacOS" / executable_name]
    candidates.extend(app.rglob("UnityPlayer.dylib"))
    binaries = list(dict.fromkeys(candidates))
    if len(binaries) < 2 or any(not binary.is_file() for binary in binaries):
        raise FileNotFoundError("macOS 包缺少主程序或 UnityPlayer.dylib。")

    for binary in binaries:
        result = subprocess.run(
            ["lipo", "-archs", str(binary)],
            check=True,
            capture_output=True,
            text=True,
        )
        architectures = set(result.stdout.strip().split())
        if architectures != {"arm64"}:
            raise ValueError(f"{binary} 架构错误：{sorted(architectures)}，要求仅 ARM64。")
        print(f"架构校验通过：{binary.relative_to(source)} -> ARM64")


def add_symlink(archive: zipfile.ZipFile, path: Path, name: str) -> None:
    """以 Unix 符号链接条目写入路径，避免把 macOS 应用链接摊平成文件。"""
    info = zipfile.ZipInfo(name)
    info.create_system = 3
    info.external_attr = (stat.S_IFLNK | 0o777) << 16
    archive.writestr(info, os.readlink(path), compress_type=zipfile.ZIP_STORED)


def package_tree(source: Path, output: Path, root_name: str) -> None:
    """压缩完整游戏目录，保留 Unix 权限并验证 Unicode 文件名标志和 CRC。"""
    if not source.is_dir():
        raise NotADirectoryError(source)
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        root_info = zipfile.ZipInfo(root_name.rstrip("/") + "/")
        root_info.create_system = 3
        root_info.external_attr = ((stat.S_IFDIR | 0o755) << 16) | 0x10
        archive.writestr(root_info, b"")

        for current, directories, files in os.walk(source, followlinks=False):
            current_path = Path(current)
            children = sorted([*(current_path / name for name in directories), *(current_path / name for name in files)])
            for path in children:
                relative = path.relative_to(source)
                if any(part == "__MACOSX" or part == ".DS_Store" or part.startswith("._") for part in relative.parts):
                    if path.name in directories:
                        directories.remove(path.name)
                    continue

                archive_name = f"{root_name.rstrip('/')}/{relative.as_posix()}"
                if path.is_symlink():
                    add_symlink(archive, path, archive_name)
                    if path.name in directories:
                        directories.remove(path.name)
                elif path.is_dir():
                    info = zipfile.ZipInfo(archive_name.rstrip("/") + "/")
                    info.create_system = 3
                    info.external_attr = ((stat.S_IFDIR | stat.S_IMODE(path.stat().st_mode)) << 16) | 0x10
                    archive.writestr(info, b"")
                else:
                    archive.write(path, archive_name, compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)

    with zipfile.ZipFile(output) as archive:
        bad_member = archive.testzip()
        if bad_member is not None:
            raise ValueError(f"ZIP CRC 校验失败：{bad_member}")
        for item in archive.infolist():
            if any(ord(character) > 127 for character in item.filename) and not item.flag_bits & 0x800:
                raise ValueError(f"ZIP 中文文件名缺少 UTF-8 标志：{item.filename!r}")

    digest_builder = hashlib.sha256()
    with output.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest_builder.update(chunk)
    digest = digest_builder.hexdigest()
    print(f"ZIP 校验通过：{output} ({output.stat().st_size} bytes, SHA-256 {digest})")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=("windows-x64", "macos-arm64"), required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--root-name", required=True)
    args = parser.parse_args()

    source = args.source.resolve()
    output = args.output.resolve()
    if args.platform == "windows-x64":
        require_windows_x64(source)
    else:
        require_macos_arm64(source)
    package_tree(source, output, args.root_name)


if __name__ == "__main__":
    main()
