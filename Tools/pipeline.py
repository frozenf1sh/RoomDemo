#!/usr/bin/env python3
# 文件：pipeline.py。职责：验证编译、测试、场景与构建；任何失败都禁止进入下一步。
"""失败立即停止的 Unity 试玩流水线；复用已连接 Editor 或运行官方批处理。"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
LOGS = ROOT / 'Logs' / 'Pipeline'
LOGS.mkdir(parents=True, exist_ok=True)
VERSION = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]


def live(command, *args, timeout=240):
    """调用连接式 CLI，解析稳定 JSON 协议并检查嵌套失败结果。"""
    run = subprocess.run(['unity', 'command', command, '--project-path', str(ROOT), '--format', 'json', '--timeout', str(timeout), *args], cwd=ROOT, capture_output=True, text=True, timeout=timeout + 20)
    try:
        result = json.loads(run.stdout)
    except ValueError:
        raise RuntimeError(f'{command}: 命令行响应不是有效 JSON: {run.stderr[-1500:]}')
    (LOGS / f'{command}.json').write_text(json.dumps(result, indent=2))
    if run.returncode or not result.get('success'):
        raise RuntimeError(f'{command}: {result.get("errors", run.stderr)}')
    value = result['data']['result']
    if value.get('success') is False or value.get('failed') or value.get('compilationFailed'):
        raise RuntimeError(f'{command}: {value}')
    return value


def check_tests(result, mode):
    """拒绝失败、未执行或空套件，保存实际测试结果。"""
    summary = result.get('summary', result.get('Summary', {}))
    total = summary.get('total', summary.get('Total', 0))
    passed = summary.get('passed', summary.get('Passed', 0))
    failed = summary.get('failed', summary.get('Failed', 0))
    if result.get('success') is False or failed or total == 0 or passed != total:
        raise RuntimeError(f'{mode} 测试未全部通过: {result}')
    (LOGS / f'{mode}-results.json').write_text(json.dumps(result, indent=2))
    print(f'{"编辑模式" if mode == "editor" else "运行模式"}：{passed}/{total} 通过', flush=True)


def test_live(mode):
    """运行测试并等待异步运行模式结果，不把启动成功当作通过。"""
    args = ['--mode', mode]
    if mode == 'playmode': args += ['--async_tests', 'true']
    result = live('run_tests', *args)
    deadline = time.monotonic() + 240
    while result.get('status', result.get('result')) == 'running':
        if time.monotonic() >= deadline: raise TimeoutError('Unity 测试超时，已停止构建。')
        time.sleep(3)
        result = live('test_status')
    if result.get('status') in ('cancelled', 'idle', 'error'): raise RuntimeError(f'测试没有明确结论： {result}')
    check_tests(result, mode)


def batch(editor, stage, args, tests=False):
    """使用独立 Editor 批处理；测试不传 -quit，避免提前结束。"""
    log = LOGS / f'{stage}.log'
    base = [str(editor), '-batchmode', '-projectPath', str(ROOT), '-logFile', str(log)]
    if not tests: base += ['-quit']
    run = subprocess.run(base + args, cwd=ROOT, timeout=900)
    if run.returncode: raise RuntimeError(f'{stage} 失败（退出码 {run.returncode}），请检查 {log}')
    if tests:
        xml = ET.parse(LOGS / f'{stage}.xml').getroot()
        if xml.get('result') != 'Passed' or int(xml.get('total', '0')) == 0:
            raise RuntimeError(f'{stage}：测试报告失败或为空，已停止构建。')
        print(f'{stage}：{xml.get("passed")}/{xml.get("total")} 通过', flush=True)
    print(f'{stage}：通过', flush=True)


def compile_live():
    """请求编译后等待重载稳定，最终再确认没有编译失败。"""
    # 程序集重载可能断开编译请求；稍后必须查询真实编译结果。
    subprocess.run(['unity', 'recompile', '--project-path', str(ROOT), '--format', 'json'], cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    deadline = time.monotonic() + 120
    while True:
        try: result = live('recompile_status')
        except RuntimeError:
            if time.monotonic() >= deadline: raise
            time.sleep(2); continue
        if result.get('status') in ('completed', 'up_to_date'): break
        if time.monotonic() >= deadline: raise TimeoutError('编译超时。')
        time.sleep(2)
    # 第一次编译完成后，资源导入仍可能引发第二次程序集重载。
    # 要求连续三次确认 Editor 就绪，连接中断只能在有限超时内重试。
    stable = 0
    while stable < 3:
        if time.monotonic() >= deadline: raise TimeoutError('编译后 Editor 未恢复稳定状态。')
        time.sleep(1)
        try:
            ready = live('editor_status')
            stable = stable + 1 if not ready.get('compiling') and not ready.get('domainReloadInProgress') else 0
        except RuntimeError:
            stable = 0
    live('recompile_status')
    print("编译：通过", flush=True)


def main():
    """决定连接式或批处理模式，按依赖顺序执行验证并生成汇总。"""
    parser = argparse.ArgumentParser()
    parser.add_argument('--headless', action='store_true', help='要求先关闭项目 Editor，再使用批处理验证。')
    parser.add_argument('--editor', type=Path, default=Path(f'/Applications/Unity/Hub/Editor/{VERSION}/Unity.app/Contents/MacOS/Unity'))
    parser.add_argument('--domain-only', action='store_true', help='仅编译和运行编辑模式测试，不修改场景或构建。')
    parser.add_argument('--regenerate-scene', action='store_true', help='生成独立灰盒参考，不覆盖已有美术试玩场景。')
    args = parser.parse_args()
    status = subprocess.run(['unity', 'status', '--format', 'json'], cwd=ROOT, capture_output=True, text=True)
    connected = status.returncode == 0 and any(x.get('project') == str(ROOT) and x.get('state') == 'ready' for x in json.loads(status.stdout)['data']['instances'])
    if connected and args.headless:
        raise RuntimeError('使用 --headless 前请关闭该项目的 Editor，避免项目锁冲突。')
    if connected:
        print('复用已连接的 Unity Editor。', flush=True)
        compile_live()
        test_live('editor')
        if args.domain_only: return
        if args.regenerate_scene:
            live('eval', 'Game.Editor.DemoSceneBuilder.BuildGreyboxScene();')
            live('eval', 'UnityEditor.AssetDatabase.SaveAssets(); var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Game.Editor.DemoSceneBuilder.ScenePath); if (scene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);')
        live('eval', 'Game.Editor.DemoSceneValidator.ValidateScene();')
        test_live('playmode')
        print('正在构建 macOS 试玩程序……', flush=True)
        live('eval', 'Game.Editor.CliBuild.QueueBuild();')
        deadline = time.monotonic() + 900
        while True:
            result = json.loads((ROOT / 'Logs/build-summary.json').read_text())
            if result.get('result') != 'Queued': break
            if time.monotonic() >= deadline: raise TimeoutError('构建超时，请检查 Unity 日志。')
            time.sleep(3)
    else:
        if not args.editor.is_file(): raise FileNotFoundError(args.editor)
        batch(args.editor, 'compile', [])
        batch(args.editor, 'EditMode', ['-runTests', '-testPlatform', 'EditMode', '-testResults', str(LOGS / 'EditMode.xml')], tests=True)
        if args.domain_only: return
        if args.regenerate_scene:
            batch(args.editor, 'scene', ['-executeMethod', 'Game.Editor.DemoSceneBuilder.BuildGreyboxScene'])
        batch(args.editor, 'validate', ['-executeMethod', 'Game.Editor.DemoSceneValidator.ValidateScene'])
        batch(args.editor, 'PlayMode', ['-runTests', '-testPlatform', 'PlayMode', '-testResults', str(LOGS / 'PlayMode.xml')], tests=True)
        batch(args.editor, 'build', ['-executeMethod', 'Game.Editor.CliBuild.BuildDemo', '-buildTarget', 'StandaloneOSX'])
    summary = json.loads((ROOT / 'Logs/build-summary.json').read_text())
    if summary['result'] != 'Succeeded' or not Path(summary['output']).exists(): raise RuntimeError(summary)
    tests = {}
    for mode, stage in [('editor', 'EditMode'), ('playmode', 'PlayMode')]:
        if connected:
            payload = json.loads((LOGS / f'{mode}-results.json').read_text())
            counts = payload.get('summary', payload.get('Summary', {}))
            tests[stage] = {name: counts.get(name.lower(), counts.get(name.title(), 0)) for name in ['total', 'passed', 'failed']}
        else:
            xml = ET.parse(LOGS / f'{stage}.xml').getroot()
            tests[stage] = {name: int(xml.get(name, '0')) for name in ['total', 'passed', 'failed']}
    (ROOT / 'Logs/verification-summary.json').write_text(json.dumps({
        'verified_at': datetime.now(timezone.utc).isoformat(), 'unity': VERSION,
        'compile': 'Passed', 'scene_validation': 'Passed', 'tests': tests, 'build': summary,
        'scene_regenerated': args.regenerate_scene,
        'human_first_play_duration': '尚未验证'
    }, indent=2))
    print(f'完成： {summary["output"]}', flush=True)


if __name__ == '__main__':
    main()
