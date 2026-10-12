# GitHub Actions 发布说明

发布工作流位于 `.github/workflows/release.yml`。可在 GitHub Actions 页面从 `main` 手动运行“构建并发布试玩包”，填写 `数字.数字.数字-年月日` 格式的版本号，例如 `0.0.2-261012`；也可把符合此格式的新版本标签推送到 `main` 历史中的提交来启动工作流。工作流只在两个平台都构建、架构校验和打包成功后创建 Release 与标签。

## 首次运行前配置 Unity 授权

仓库管理员需按 [GameCI 激活说明](https://game.ci/docs/github/activation/) 准备 Unity 授权，并在 GitHub 仓库的 **Settings → Secrets and variables → Actions** 中添加对应 Actions secrets。不要把授权文件或账户密码提交到仓库：

| Secret | 内容 |
| --- | --- |
| `UNITY_LICENSE` | Personal License 使用：按指南激活后取得的 Unity `.ulf` 文件全文 |
| `UNITY_SERIAL` | Professional License 使用：Unity 提供的序列号；与 `UNITY_LICENSE` 二选一 |
| `UNITY_EMAIL` | Unity 账户邮箱 |
| `UNITY_PASSWORD` | Unity 账户密码 |

工作流启动时会先检查相应授权 secrets；缺失时会停止，不会创建版本标签或 Release。

## 构建与产物检查

- Windows 使用 `StandaloneWindows64` 与显式 `x64` 架构，打包前校验 `RoomDemo.exe`、`UnityPlayer.dll` 和 `UnityCrashHandler64.exe` 的 PE Machine 均为 AMD64。
- macOS 使用 Apple Silicon runner，构建 `StandaloneOSX` ARM64 应用，并检查 app 主程序和 `UnityPlayer.dylib`。
- 两个平台都从 `AtelierPlayable.unity` 构建，不修改项目正式 Build Settings 场景列表。
- ZIP 生成后进行 CRC 检查；带中文的文件名必须带 ZIP UTF-8 标志。发布包另附 `SHA256SUMS.txt`。
