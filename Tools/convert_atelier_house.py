#!/usr/bin/env python3
# 文件：convert_atelier_house.py。职责：把已授权 GLTF 房屋转换成 Unity 可导入 OBJ 与 PBR 通道。
# 边界：保留作者 UV 与节点变换，只移除源文件自带的道路和草地平板，不读取概念图。
import json
import math
from pathlib import Path
import shutil
import struct
from PIL import Image

SRC = Path('AssetIntake/AtelierRevision20261010/JimbogiesHouse/Source')
OUT = Path('Assets/_Game/Content/ArtRebuild/Imports/JimbogiesHouse')
OUT.mkdir(exist_ok=True)
D = json.loads((SRC / 'scene.gltf').read_text())
BIN = (SRC / 'scene.bin').read_bytes()
LINES = ['# Jimbogies / Free Small Old House / CC BY 4.0', 'mtllib House.mtl']
OFFSET = 0


def accessor(index):
    """按声明的 stride 和偏移读取，不假定缓冲区紧凑排列。"""
    a = D['accessors'][index]
    v = D['bufferViews'][a['bufferView']]
    width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
    fmt = '<' + {5121: 'B', 5123: 'H', 5125: 'I', 5126: 'f'}[a['componentType']] * width
    start = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    return [struct.unpack_from(fmt, BIN, start + j * v.get('byteStride', struct.calcsize(fmt))) for j in range(a['count'])]


def multiply(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def walk(index, parent):
    """累计节点变换；无源法线的面在 OBJ 导入时由 Unity 重算。"""
    global OFFSET
    node = D['nodes'][index]
    local = [[float(x == y) for y in range(4)] for x in range(4)]
    if 'matrix' in node:
        local = [[node['matrix'][j * 4 + x] for j in range(4)] for x in range(4)]
    matrix = multiply(parent, local)
    if 'mesh' in node:
        for p in D['meshes'][node['mesh']]['primitives']:
            material = p.get('material', 10)
            if material in (4, 9):
                continue
            attributes = p['attributes']
            points = accessor(attributes['POSITION'])
            uv = accessor(attributes['TEXCOORD_0']) if 'TEXCOORD_0' in attributes else [(0, 0)] * len(points)
            LINES.extend(['o ' + D['materials'][material]['name'], 'usemtl ' + D['materials'][material]['name']])
            for point in points:
                transformed = [sum(matrix[x][j] * (*point, 1)[j] for j in range(4)) for x in range(3)]
                LINES.append('v ' + ' '.join(map(str, transformed)))
            LINES.extend(f'vt {u} {1-v}' for u, v in uv)
            indices = [x[0] for x in accessor(p['indices'])]
            for k in range(0, len(indices), 3):
                LINES.append('f ' + ' '.join(f'{x+OFFSET+1}/{x+OFFSET+1}' for x in indices[k:k+3]))
            OFFSET += len(points)
    for child in node.get('children', []):
        walk(child, matrix)


for index in D['scenes'][D.get('scene', 0)]['nodes']:
    walk(index, [[float(x == y) for y in range(4)] for x in range(4)])
records = []
mtl = []
for index, material in enumerate(D['materials']):
    if index in (4, 9):
        continue
    name = material['name']
    mtl.extend(['newmtl ' + name, 'Kd 1 1 1'])
    pbr = material.get('pbrMetallicRoughness', {})
    record = dict(name=name, baseColorFactor=pbr.get('baseColorFactor', [1, 1, 1, 1]),
                  metallicFactor=pbr.get('metallicFactor', 1), roughnessFactor=pbr.get('roughnessFactor', 1))
    for channel, texture in [('baseColor', pbr.get('baseColorTexture')), ('metalRough', pbr.get('metallicRoughnessTexture')), ('normal', material.get('normalTexture'))]:
        if not texture:
            continue
        uri = D['images'][D['textures'][texture['index']]['source']]['uri']
        destination = OUT / Path(uri).name
        image = Image.open(SRC / uri)
        image.thumbnail((2048, 2048))
        image.save(destination)
        record[channel] = destination.name
        if channel == 'baseColor':
            mtl.append('map_Kd ' + destination.name)
        if channel == 'metalRough':
            raw = image.convert('RGB')
            r, g, b = raw.split()
            packed = Image.merge('RGBA', (b, Image.new('L', raw.size, 0), Image.new('L', raw.size, 0), Image.eval(g, lambda x: 255-x)))
            packed.save(OUT / (name + '_URPMask.png'))
            record['packed'] = name + '_URPMask.png'
    records.append(record)
(OUT / 'House.obj').write_text('\n'.join(LINES))
(OUT / 'House.mtl').write_text('\n'.join(mtl))
(OUT / '材质通道.json').write_text(json.dumps(dict(materials=records), ensure_ascii=False, indent=2))
shutil.copy2(SRC / 'license.txt', OUT / 'license.txt')
print('房屋转换完成：', OFFSET, '顶点；删除源道路和草地平板，贴图上限 2K。')
