#!/usr/bin/env python3
# 文件：prepare_atelier_sources.py。职责：核验并转换独立美术场景选定的已下载素材。
# 边界：只写 ArtRebuild/Imports；不改原包、既有导入资源、场景或导入设置。
import hashlib
import io
import json
import math
from pathlib import Path
import shutil
import struct
import zipfile

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Assets/_Game/Content/ArtRebuild/Imports'
RECORDS = []


def archive(relative, expected, folder, nested=False):
    """先核验来源包哈希，再展开选入目录；不存在覆盖既有资源的路径。"""
    path = ROOT / relative
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest == expected, relative
    target = OUT / folder
    target.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path) as z:
        for name in z.namelist():
            if nested and name.endswith('.zip'):
                with zipfile.ZipFile(io.BytesIO(z.read(name))) as child:
                    for entry in child.namelist():
                        if not entry.endswith('/'):
                            (target / Path(entry).name).write_bytes(child.read(entry))
            elif not nested and not name.endswith('/'):
                (target / Path(name).name).write_bytes(z.read(name))
    RECORDS.append(dict(source=relative, bytes=path.stat().st_size, sha256=digest, destination=str(target.relative_to(ROOT))))
    return target


def gltf_obj(folder):
    """保留 UV、法线、节点变换和材质槽，不依赖第三方 glTF 运行时。"""
    d = json.loads((folder / 'scene.gltf').read_text())
    binary = (folder / 'scene.bin').read_bytes()
    lines = ['# Krag Studio / Antique Bookcase / CC BY 4.0', 'mtllib AntiqueBookcase.mtl']
    count = 0

    def accessor(index):
        a = d['accessors'][index]
        v = d['bufferViews'][a['bufferView']]
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        fmt = '<' + {5121: 'B', 5123: 'H', 5125: 'I', 5126: 'f'}[a['componentType']] * width
        stride = v.get('byteStride', struct.calcsize(fmt))
        offset = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from(fmt, binary, offset + i * stride) for i in range(a['count'])]

    def multiply(a, b):
        return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

    def walk(index, parent):
        nonlocal count
        node = d['nodes'][index]
        if 'matrix' in node:
            local = [[node['matrix'][j * 4 + i] for j in range(4)] for i in range(4)]
        else:
            local = [[float(i == j) for j in range(4)] for i in range(4)]
        matrix = multiply(parent, local)
        if 'mesh' in node:
            for p in d['meshes'][node['mesh']]['primitives']:
                assert p.get('mode', 4) == 4
                points = accessor(p['attributes']['POSITION'])
                normals = accessor(p['attributes']['NORMAL'])
                uvs = accessor(p['attributes']['TEXCOORD_0'])
                lines.extend([f'o 书柜_{count}', 'usemtl M_Bookcase'])
                for point in points:
                    v = [sum(matrix[i][j] * (*point, 1)[j] for j in range(4)) for i in range(3)]
                    lines.append('v ' + ' '.join(map(str, v)))
                # glTF 的纹理原点在上；OBJ 使用底部原点。
                lines.extend(f'vt {uv[0]} {1-uv[1]}' for uv in uvs)
                for normal in normals:
                    v = [sum(matrix[i][j] * normal[j] for j in range(3)) for i in range(3)]
                    length = math.sqrt(sum(x*x for x in v)) or 1
                    lines.append('vn ' + ' '.join(str(x/length) for x in v))
                indices = [v[0] for v in accessor(p['indices'])]
                for i in range(0, len(indices), 3):
                    lines.append('f ' + ' '.join(f'{j+count+1}/{j+count+1}/{j+count+1}' for j in indices[i:i+3]))
                count += len(points)
        for child in node.get('children', []):
            walk(child, matrix)

    identity = [[float(i == j) for j in range(4)] for i in range(4)]
    for index in d['scenes'][d.get('scene', 0)]['nodes']:
        walk(index, identity)
    (folder / 'AntiqueBookcase.obj').write_text('\n'.join(lines))
    (folder / 'AntiqueBookcase.mtl').write_text('newmtl M_Bookcase\nKd 1 1 1\nmap_Kd M_Bookcase_BaseColor.jpeg\n')
    print(f'书柜：{count} 个带 UV 的顶点')


def main():
    """只选入完成商品页核对的三包；原始候选继续留在 AssetIntake。"""
    folder = archive('AssetIntake/ArtRefresh20261010/3D/sm_bookcase_gltf.zip',
                     'cf37c38cb5ec6156ffbfe6a65b1657ee609f9a9f2b5e9534fb655c704fdfa696', 'KragBookcase')
    gltf_obj(folder)
    archive('AssetIntake/FabVisualPass20261010/old_wooden_window_vmfwcabdw_mid.zip',
            'b41c863128075a816404fd9de313f574816e76c7837629ac2abd6b7d264ef4d1', 'QuixelWindow')
    archive('AssetIntake/FabVisualPass20261010/palette_02.zip',
            '4e376607b5923123857908577f68af23a74983457f75ae27bbf67669140f5bb1', 'LumiPalette', True)
    # 补齐既有扫描材质的粗糙度和 AO；原导入目录保持原样。
    for group in ['painted_wooden_table', 'painted_wooden_chair_01', 'painted_wooden_stool',
                  'plastered_wall_04', 'old_wooden_floor_03', 'vintage_oil_lamp', 'office_notepads']:
        target = OUT / 'PolyHavenMaps' / group
        target.mkdir(parents=True, exist_ok=True)
        for p in (ROOT / 'AssetIntake/PolyHaven' / group).glob('*'):
            if '_rough_' in p.name or '_ao_' in p.name:
                shutil.copy2(p, target / p.name)
    (OUT / '导入来源核验.json').write_text(json.dumps(RECORDS, ensure_ascii=False, indent=2))
    print('独立来源目录准备完成；Unity 导入设置由 Editor API 处理。')


if __name__ == '__main__':
    main()
