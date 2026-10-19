#!/usr/bin/env python3
"""文件：校验画室四阶段交付包。

职责：核对仓库内文档、源图、采用图、Prefab元数据和有限颜料模型。
边界：仅用Python标准库，不访问Downloads、废纸篓、网络或本机Unity日志；
不执行Unity、不验证浏览器布局，也不把静态检查当作真人理解度验收。
"""

from functools import lru_cache
import hashlib
import json
from pathlib import Path
import re
import struct
import zipfile


ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "Docs/Design/全流程重设计20261019"
REFERENCES = PACKAGE / "参考资料"


def require(condition, message):
    """拒绝静默通过，错误信息只报告需要修复的交付路径。"""
    if not condition:
        raise ValueError(message)


def read_json(path):
    """读取仓库内UTF-8记录，不依赖制作电脑的绝对路径。"""
    return json.loads(path.read_text(encoding="utf-8"))


def digest_bytes(value):
    """用原始字节指纹核对复制关系，不重编码或修改图片。"""
    return hashlib.sha256(value).hexdigest()


def digest(path):
    """计算已交付文件的SHA256。"""
    return digest_bytes(path.read_bytes())


def png_size(path):
    """读取PNG签名和IHDR尺寸，只检查格式，不宣称做过视觉验收。"""
    data = path.read_bytes()
    require(data[:8] == b"\x89PNG\r\n\x1a\n", f"PNG签名错误：{path}")
    require(data[12:16] == b"IHDR", f"PNG缺少IHDR：{path}")
    return list(struct.unpack(">II", data[16:24]))


def validate_links():
    """相对链接按各文档所在目录解析；历史原件不重新排版。"""
    from urllib.parse import unquote

    documents = list(PACKAGE.rglob("*.md"))
    documents += [ROOT / "Docs/Guide/新人上手.md"]
    documents += [ROOT / "Docs/Archive/Design/2026-10-19/README.md"]
    links = 0
    for path in documents:
        text = path.read_text(encoding="utf-8")
        for match in re.finditer(r"!?\[[^\]]*\]\((<[^>]+>|[^\n)]+)\)", text):
            target = unquote(match[1].strip("<>").split("#", 1)[0])
            if not target or re.match(r"^[a-z]+:", target):
                continue
            require((path.parent / target).exists(), f"链接缺失：{path.name} → {target}")
            links += 1
        if path.is_relative_to(PACKAGE):
            for number, line in enumerate(text.splitlines(), 1):
                require(line == line.rstrip(), f"文档行尾空格：{path.name}:{number}")
    return links


def validate_sources():
    """核对仓库副本、源稿内图、原有参考与正式四张图样。"""
    for item in read_json(REFERENCES / "设计输入索引.json"):
        path = ROOT / item["仓库副本"]
        require(path.is_file(), f"设计输入副本缺失：{path}")
        require(digest(path) == item["sha256"], f"设计输入副本不一致：{path}")

    with zipfile.ZipFile(REFERENCES / "全流程 (1).docx") as document:
        for index in range(1, 5):
            name = f"image{index}.png"
            expected = digest_bytes(document.read("word/media/" + name))
            require(digest(REFERENCES / name) == expected, f"原稿内图不一致：{name}")

    for item in read_json(REFERENCES / "原图复制记录.json"):
        path = ROOT / item["副本"]
        require(digest(path) == item["sha256"], f"旧参考副本不一致：{path.name}")

    assets = read_json(REFERENCES / "新资产实查.json")
    for index in range(1, 5):
        name = f"{index}.png"
        item = next(item for item in assets if item["文件"].endswith("/笔刷/" + name))
        path = PACKAGE / "概念图/策划笔刷" / name
        require(digest(path) == item["sha256"], f"笔刷原件不一致：{name}")
        require(png_size(path) == [2048, 2048], f"笔刷尺寸不符：{name}")

    source_dialogue = read_json(REFERENCES / "原稿台词实查.json")
    for item in source_dialogue["缪斯"]:
        require(not ({"采用", "触发", "调整"} & item.keys()), "原文抽取中残留过期采用记录")


def validate_concepts():
    """只核对采用的17张文件，明确弃用图不作为外观依据。"""
    manifest = read_json(PACKAGE / "概念图/采用与弃用清单.json")
    records = read_json(PACKAGE / "概念图/生成记录.json")["采用图"]
    adopted = sorted(path.name for path in (PACKAGE / "概念图/新生成").glob("*.png"))
    require(adopted == sorted(manifest["采用图"]), "采用清单与实际概念图不一致")
    require(len(adopted) == 17 and "K6_C.png" not in adopted, "采用图数量或弃用结论不符")
    result = []
    for item in records:
        path = PACKAGE / item["采用文件"]
        require(path.name in adopted, f"生成记录引用非采用图：{path.name}")
        require(digest(path) == item["sha256"], f"采用图指纹不一致：{path.name}")
        result.append({"文件": item["采用文件"], "尺寸": png_size(path), "sha256": digest(path)})
    require(len(result) == 17, "生成记录未覆盖全部采用图")
    return result


def validate_prefabs():
    """检查交付模块和meta；迁移字段及运行结论来自已有报告，不重新冒充运行。"""
    migration = read_json(REFERENCES / "Prefab迁移报告.json")
    runtime = read_json(REFERENCES / "Prefab运行检查.json")
    paths = sorted((ROOT / "Assets/_Game/Content/Prefabs/AtelierModules").rglob("*.prefab"))
    require(migration["success"] and len(paths) == migration["createdCount"] == 71, "Prefab数量或迁移报告不符")
    guids = []
    for path in paths:
        meta = Path(str(path) + ".meta")
        require(meta.is_file(), f"缺少meta：{path.name}")
        match = re.search(r"^guid: ([a-f0-9]{32})$", meta.read_text(encoding="utf-8"), re.M)
        require(match is not None, f"meta缺少GUID：{path.name}")
        guids.append(match[1])
    require(len(set(guids)) == len(guids), "新增Prefab存在重复GUID")
    for item in migration["modules"]:
        require((ROOT / item["asset"]).exists(), f"迁移模块缺失：{item['asset']}")
    require(runtime["success"] and not runtime["errors"], "现有运行检查报告未通过")
    # QualitySettings哈希包含制作时用户的本地改动，不要求其他开发机复刻。
    return len(paths)


def validate_paint_budget():
    """穷举可达整数成本状态，证明每条完整路径耗尽且末笔处于全局范围。"""
    results = []
    for planned in (5, 6):
        states = set()

        @lru_cache(maxsize=None)
        def ways(remaining, count):
            states.add((remaining, count))
            if count == 0:
                require(remaining == 0, "最后一笔后仍有颜料")
                return 1
            low = max(12, remaining - (count - 1) * 24)
            high = min(24, remaining - (count - 1) * 12)
            require(12 <= low <= high <= 24, "颜料出现不可行区间")
            if count == 1:
                require(low == high == remaining, "最后一笔无法原样耗尽")
            return sum(ways(remaining - cost, count - 1) for cost in range(low, high + 1))

        complete_paths = ways(100, planned)
        results.append({"计划笔数": planned, "总颜料": 100, "每笔范围": [12, 24],
                        "穷举可达状态数": len(states), "覆盖合法完整序列数": complete_paths,
                        "结果": "所有路径合法、末笔合法且余量为零；仅验证设计模型，未验证尚未实现的游戏代码"})
    return results


def main():
    """将静态校验结果写回交付资料，重复运行使用相同内容。"""
    links = validate_links()
    validate_sources()
    images = validate_concepts()
    prefabs = validate_prefabs()
    budgets = validate_paint_budget()
    (REFERENCES / "颜料预算模型校验.json").write_text(json.dumps(budgets, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    documents = list(PACKAGE.glob("*.md"))
    report = {
        "success": True, "文档数量": len(documents),
        "文档字符数": sum(len(path.read_text(encoding="utf-8")) for path in documents),
        "采用新概念图": len(images), "原稿图片原字节一致": 4,
        "旧参考原字节一致": 4, "笔刷原字节一致": 4,
        "仓库设计输入副本已校验": True, "文档链接已检查": links,
        "新增Prefab": prefabs, "Prefab元数据齐全且GUID不重复": True,
        "已有运行报告通过": True, "颜料模型全路径通过": True,
        "制作机器本地目录依赖": False,
        "QualitySettings说明": "迁移时保留了制作机器的用户改动；本校验不要求其他电脑匹配该本地文件哈希。",
        "图片": images,
        "边界": "仅为静态交付核对、设计模型及已有运行报告；未重新运行Unity，不证明浏览器、真实构建UI或真人理解度通过。",
    }
    (REFERENCES / "交付完整性检查.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in report.items() if key != "图片"}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
