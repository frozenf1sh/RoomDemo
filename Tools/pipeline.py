#!/usr/bin/env python3
"""Fail-fast Unity demo pipeline; reuse an open Editor or run official batch CLI."""
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
    run = subprocess.run(['unity', 'command', command, '--project-path', str(ROOT), '--format', 'json', '--timeout', str(timeout), *args], cwd=ROOT, capture_output=True, text=True, timeout=timeout + 20)
    try:
        result = json.loads(run.stdout)
    except ValueError:
        raise RuntimeError(f'{command}: invalid CLI response: {run.stderr[-1500:]}')
    (LOGS / f'{command}.json').write_text(json.dumps(result, indent=2))
    if run.returncode or not result.get('success'):
        raise RuntimeError(f'{command}: {result.get("errors", run.stderr)}')
    value = result['data']['result']
    if value.get('success') is False or value.get('failed') or value.get('compilationFailed'):
        raise RuntimeError(f'{command}: {value}')
    return value


def check_tests(result, mode):
    summary = result.get('summary', result.get('Summary', {}))
    total = summary.get('total', summary.get('Total', 0))
    passed = summary.get('passed', summary.get('Passed', 0))
    failed = summary.get('failed', summary.get('Failed', 0))
    if result.get('success') is False or failed or total == 0 or passed != total:
        raise RuntimeError(f'{mode} tests did not all pass: {result}')
    (LOGS / f'{mode}-results.json').write_text(json.dumps(result, indent=2))
    print(f'{mode}: {passed}/{total} passed', flush=True)


def test_live(mode):
    args = ['--mode', mode]
    if mode == 'playmode': args += ['--async_tests', 'true']
    result = live('run_tests', *args)
    deadline = time.monotonic() + 240
    while result.get('status', result.get('result')) == 'running':
        if time.monotonic() >= deadline: raise TimeoutError('Unity tests timed out; build stopped.')
        time.sleep(3)
        result = live('test_status')
    if result.get('status') in ('cancelled', 'idle', 'error'): raise RuntimeError(f'Tests have no verdict: {result}')
    check_tests(result, mode)


def batch(editor, stage, args, tests=False):
    log = LOGS / f'{stage}.log'
    base = [str(editor), '-batchmode', '-projectPath', str(ROOT), '-logFile', str(log)]
    if not tests: base += ['-quit']
    run = subprocess.run(base + args, cwd=ROOT, timeout=900)
    if run.returncode: raise RuntimeError(f'{stage} failed (exit {run.returncode}); inspect {log}')
    if tests:
        xml = ET.parse(LOGS / f'{stage}.xml').getroot()
        if xml.get('result') != 'Passed' or int(xml.get('total', '0')) == 0:
            raise RuntimeError(f'{stage}: test report failed or empty; build stopped.')
        print(f'{stage}: {xml.get("passed")}/{xml.get("total")} passed', flush=True)
    print(f'{stage}: passed', flush=True)


def compile_live():
    # A domain reload can close the recompile HTTP response; confirm actual status afterwards.
    subprocess.run(['unity', 'recompile', '--project-path', str(ROOT), '--format', 'json'], cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    deadline = time.monotonic() + 120
    while True:
        try: result = live('recompile_status')
        except RuntimeError:
            if time.monotonic() >= deadline: raise
            time.sleep(2); continue
        if result.get('status') in ('completed', 'up_to_date'): break
        if time.monotonic() >= deadline: raise TimeoutError('Compilation timed out.')
        time.sleep(2)
    # Asset import may trigger a second reload after the first compile reports complete.
    # Require three consecutive ready responses, tolerating only bounded transport gaps.
    stable = 0
    while stable < 3:
        if time.monotonic() >= deadline: raise TimeoutError('Editor did not stabilize after compilation.')
        time.sleep(1)
        try:
            ready = live('editor_status')
            stable = stable + 1 if not ready.get('compiling') and not ready.get('domainReloadInProgress') else 0
        except RuntimeError:
            stable = 0
    live('recompile_status')
    print("Compile: passed", flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--headless', action='store_true', help='Require the Editor to be closed and verify with batch mode.')
    parser.add_argument('--editor', type=Path, default=Path(f'/Applications/Unity/Hub/Editor/{VERSION}/Unity.app/Contents/MacOS/Unity'))
    parser.add_argument('--domain-only', action='store_true', help='Compile and run EditMode tests without changing scenes or building.')
    parser.add_argument('--regenerate-scene', action='store_true', help='Explicitly rebuild the greybox scene and generated prefabs.')
    args = parser.parse_args()
    status = subprocess.run(['unity', 'status', '--format', 'json'], cwd=ROOT, capture_output=True, text=True)
    connected = status.returncode == 0 and any(x.get('project') == str(ROOT) and x.get('state') == 'ready' for x in json.loads(status.stdout)['data']['instances'])
    if connected and args.headless:
        raise RuntimeError('Close this project in the Editor before using --headless (project lock).')
    if connected:
        print('Reusing connected Unity Editor.', flush=True)
        compile_live()
        test_live('editor')
        if args.domain_only: return
        if args.regenerate_scene:
            live('eval', 'Game.Editor.DemoSceneBuilder.Build();')
            live('eval', 'UnityEditor.AssetDatabase.SaveAssets(); var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Game.Editor.DemoSceneBuilder.ScenePath); if (scene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);')
        live('eval', 'Game.Editor.DemoSceneValidator.ValidateScene();')
        test_live('playmode')
        print('Building macOS executable...', flush=True)
        live('eval', 'Game.Editor.CliBuild.QueueBuild();')
        deadline = time.monotonic() + 900
        while True:
            result = json.loads((ROOT / 'Logs/build-summary.json').read_text())
            if result.get('result') != 'Queued': break
            if time.monotonic() >= deadline: raise TimeoutError('Build timed out; inspect Unity log.')
            time.sleep(3)
    else:
        if not args.editor.is_file(): raise FileNotFoundError(args.editor)
        batch(args.editor, 'compile', [])
        batch(args.editor, 'EditMode', ['-runTests', '-testPlatform', 'EditMode', '-testResults', str(LOGS / 'EditMode.xml')], tests=True)
        if args.domain_only: return
        if args.regenerate_scene:
            batch(args.editor, 'scene', ['-executeMethod', 'Game.Editor.DemoSceneBuilder.Build'])
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
        'human_first_play_duration': 'Not yet verified'
    }, indent=2))
    print(f'DONE: {summary["output"]}', flush=True)


if __name__ == '__main__':
    main()
