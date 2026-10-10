#!/usr/bin/env python3
# 文件：intake_atelier_revision.py。职责：下载已在 Chrome 商品页核对的 CC0 画室补充素材。
# 边界：只使用 Poly Haven 官方 API，不购买、不接受新 EULA；原下载包和既有资产保持原样。
import concurrent.futures
import hashlib
import json
from pathlib import Path
import shutil
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
INTAKE = ROOT / 'AssetIntake/AtelierRevision20261010'
OUT = ROOT / 'Assets/_Game/Content/ArtRebuild/Imports/Revision'
HEADERS = {'User-Agent': 'RoomDemo local art asset audit'}


def fetch(url):
    """读取公开官方文件，不复用浏览器登录信息。"""
    return urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=90).read()


def acquire(asset):
    """逐文件校验 API 公布的大小和 MD5，并保存可追溯的下载记录。"""
    folder = INTAKE / asset
    folder.mkdir(parents=True, exist_ok=True)
    metadata = json.loads(fetch('https://api.polyhaven.com/files/' + asset))
    (folder / '官方文件索引.json').write_text(json.dumps(metadata, indent=2))
    info = json.loads(fetch('https://api.polyhaven.com/info/' + asset))
    (folder / '官方商品信息.json').write_text(json.dumps(info, ensure_ascii=False, indent=2))
    item = metadata['hdri']['2k']['hdr'] if 'hdri' in metadata else metadata['fbx']['1k']['fbx']
    files = [item] + list(item.get('include', {}).values())
    target = OUT / asset
    target.mkdir(parents=True, exist_ok=True)
    records = []
    for spec in files:
        name = spec['url'].rsplit('/', 1)[1]
        path = folder / name
        if not path.exists():
            path.write_bytes(fetch(spec['url']))
        data = path.read_bytes()
        assert len(data) == spec['size'], name
        assert hashlib.md5(data).hexdigest() == spec['md5'], name
        shutil.copy2(path, target / name)
        records.append(dict(file=name, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(), url=spec['url']))
    report = dict(asset=asset, page='https://polyhaven.com/a/' + asset, license='CC0 1.0', date='2026-10-10',
                  unity='FBX + PBR 贴图；HDR 使用 Skybox/Panoramic。Geometry Nodes 不导入 Unity。', files=records)
    (folder / '下载核验.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print(asset, sum(r['bytes'] for r in records), '字节，官方 MD5 全部一致', flush=True)
    return report


if __name__ == '__main__':
    INTAKE.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        records = list(pool.map(acquire, ['kloppenheim_06_puresky', 'tree_small_02', 'fir_sapling', 'shrub_02',
                                          'rock_moss_set_01', 'potted_plant_02', 'book_encyclopedia_set_01']))
    (INTAKE / '本轮下载核验.json').write_text(json.dumps(records, ensure_ascii=False, indent=2))
