# 旧主线测试与构建流程归档

归档日期：2026-10-10

归档原因：用户要求新主场景美术重构不再受旧玩法流程和旧测试流程约束。

## 内容

- `Archive/LegacyValidation/Tools/pipeline.py`：旧主线全量验证入口，曾执行编译、EditMode/PlayMode 测试、旧场景校验和 macOS 构建。
- `Assets/_Game/Tests/`：旧玩法测试源码仍留在 Unity 项目原位置，供历史逻辑追溯；本归档只撤下旧流程入口，没有删除或移动测试源码。
- `Docs/Benchmark/`、`Docs/Exploration/` 等位置保留旧测试/试玩报告，作为历史证据，不代表新场景已验收。

## 当前状态

旧 `Tools/pipeline.py` 已从活动 `Tools/` 移入本目录，避免被误认为当前项目验证入口。脚本源文件保留供追溯；它依赖旧场景、旧测试和旧构建流程，**不用于新美术场景，也不应作为本轮启动命令**。

当前 `ProjectSettings/EditorBuildSettings.asset` 只启用 `Assets/_Game/Content/Scenes/AtelierNight.unity`；`AtelierPlayable.unity` 是玩法开发副本，不在构建场景列表中。新场景实施者应先明确目标场景与构建入口，再按实际美术目标编写或选择验证方式；不要自动恢复旧场景或旧测试流水线。
