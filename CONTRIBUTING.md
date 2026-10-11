# 开发约定

开始前先读[新人上手](Docs/Guide/新人上手.md)和[项目规范](AGENTS.md)。

## 改动与验证

- 先看 `git status --short --branch`。只暂存本任务文件，不用 `git add -A`、`git reset --hard` 或清理命令处理共享工作区。
- 玩法事实与配方放在 `Assets/_Game/Runtime/Domain/`；用例放在 `Application/`；Unity 输入和表现放在 `Unity/`。Presenter 不写 `GameState`。
- 场景、Prefab 和导入设置通过 Unity Editor 或 `AssetDatabase` 修改；保留对应 `.meta`，不要手改复杂 YAML。
- 领域或应用逻辑改动运行相关测试。纯美术、Shader、VFX、动画和场景改动做编译、运行观察与 Console 检查。
- 大文件按 `.gitattributes` 使用 Git LFS。导入素材时，把实际来源、许可和用途记在资源目录的来源说明中。

## 提交

提交信息使用 `feat(范围): 中文说明`、`fix(范围): 中文说明` 等格式。一个提交只放一类可审阅的改动；不要把无关的工作区变更带进去。
