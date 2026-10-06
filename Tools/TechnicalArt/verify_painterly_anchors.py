#!/usr/bin/env python3
# 文件：verify_painterly_anchors.py。职责：把真实播放器的桌面锚点截图重投影到同一世界平面。
# 边界：仅检查这组慢速镜头的桌面区域，不把数值当作任意镜头／曲面的保证。
import csv
import json
from pathlib import Path
import sys
import numpy as np
from PIL import Image


def main(directory):
    """比较四角投影对应的固定世界区域，输出小范围整数偏移搜索结果。"""
    points = {}
    for row in csv.DictReader((directory / '世界锚点投影.csv').open()):
        points.setdefault(int(row['sample']), {})[int(row['corner'])] = (float(row['x']), float(row['y']))
    images = []
    for sample in sorted(points):
        matrix, target = [], []
        for index, (x, y) in enumerate([(0, 400), (800, 400), (800, 0), (0, 0)]):
            u, v = points[sample][index]
            matrix += [[x, y, 1, 0, 0, 0, -u*x, -u*y], [0, 0, 0, x, y, 1, -v*x, -v*y]]
            target += [u, v]
        coefficients = np.linalg.solve(np.asarray(matrix), target)
        image = Image.open(directory / f'锚点_{sample}.png').convert('RGB')
        projected = image.transform((800, 400), Image.Transform.PERSPECTIVE, coefficients, Image.Resampling.BILINEAR)
        images.append(np.asarray(projected, dtype=float)/255)
    # 桌面前缘的空白窄带避开物体遮挡和受保护的颜料线；同一世界范围用于所有帧。
    reference = images[0][355:384, 130:670]
    results = []
    for sample, image in enumerate(images[1:], 1):
        errors = []
        for dx in range(-5, 6):
            for dy in range(-3, 4):
                difference = np.mean(np.abs(reference-image[355+dy:384+dy, 130+dx:670+dx]))
                errors.append((float(difference), dx, dy))
        best = min(errors)
        movement = np.mean([np.linalg.norm(np.asarray(points[sample][p])-points[0][p]) for p in range(4)])
        results.append({'sample': sample, 'screen_movement_px': float(movement),
                        'mean_rgb_error': float(np.mean(np.abs(reference-image[355:384, 130:670]))),
                        'best_offset_px': [best[1], best[2]]})
    print(json.dumps({'camera_speed_degrees_per_second': .45, 'azimuth_range_degrees': [10, 15.4],
                      'scope': '固定桌面前缘、世界种子调试颜色、五个镜头位置', 'results': results}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main(Path(sys.argv[1] if len(sys.argv) > 1 else 'Logs/LivingPainting/PlayerReview'))
