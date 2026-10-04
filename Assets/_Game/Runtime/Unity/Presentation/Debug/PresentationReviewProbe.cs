// =============================================================================
// 文件：PresentationReviewProbe.cs
// 职责：开发时采样实际渲染帧间隔并导出截图和性能记录。
// 边界：只观察表现；不写领域状态、不在正式游玩时自动运行。
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>由编辑器或人工评审显式启动的轻量采样器。</summary>
    public sealed class PresentationReviewProbe : MonoBehaviour
    {
        /// <summary>采样若干秒并保存截图。帧率含编辑器开销，不等于目标 PC 性能。</summary>
        public void Capture(string directory, string label, float seconds = 5)
        {
            // 同一探针一次只采样一段，防止相互覆盖报告。
            StopAllCoroutines();
            StartCoroutine(Record(directory, label, seconds));
        }
        /// <summary>跳过首帧，收集帧时间并计算平均与百分之九十九分位。</summary>
        private IEnumerator Record(string directory, string label, float seconds)
        {
            yield return null;
            var intervals = new List<float>(1024);
            float elapsed = 0;
            int gcBefore = System.GC.CollectionCount(0);
            while (elapsed < seconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                intervals.Add(dt);
                elapsed += dt;
            }
            intervals.Sort();
            int tail = Mathf.Clamp(Mathf.CeilToInt(intervals.Count * .99f) - 1, 0, intervals.Count - 1);
            var report = new Report { frames = intervals.Count, seconds = elapsed, average_fps = intervals.Count / elapsed,
                p99_frame_ms = intervals[tail] * 1000, gen0_collections = System.GC.CollectionCount(0) - gcBefore,
                resolution = Screen.width + "x" + Screen.height, environment = UnityEngine.Application.isEditor ? "Unity 编辑器运行模式" : "macOS 独立试玩程序" };
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, label + ".json"), JsonUtility.ToJson(report, true));
            // 截图在帧末抓取，包含实际面板；不会改相机或保存运行模式场景。
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, label + ".png"));
            Debug.Log("演出采样完成：" + label + "，平均帧率 " + report.average_fps.ToString("F1"));
        }
        /// <summary>仅为机器报告提供稳定字段，不参与任何世界判定。</summary>
        [System.Serializable] private sealed class Report
        {
            public int frames, gen0_collections;
            public float seconds, average_fps, p99_frame_ms;
            public string resolution, environment;
        }
    }
}
