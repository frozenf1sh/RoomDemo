// =============================================================================
// 文件：VisualSprintAudioCapture.cs
// 职责：在显式美术录像期间缓存监听器的真实混音，输出与画面配套的 WAV。
// 边界：只读声音样本，不改变音量、世界或输入；正常游玩禁用该组件。
// =============================================================================
using System;
using System.IO;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>本轮录像的短混音缓存，不是游戏音频框架或麦克风录音。</summary>
    public sealed class VisualSprintAudioCapture : MonoBehaviour
    {
        private readonly object gate = new object();
        private float[] samples;
        private int count, channels, sampleRate;
        private bool capturing;
        /// <summary>主线程预留最多三十秒的混音，音频线程中不分配资源。</summary>
        public void Begin()
        {
            sampleRate = AudioSettings.outputSampleRate;
            lock (gate)
            {
                samples = new float[sampleRate * 6 * 30];
                count = 0; channels = 2; capturing = true;
            }
        }
        private void OnAudioFilterRead(float[] data, int channelCount)
        {
            // 音频线程只复制数组；不调用 Unity API 或进行磁盘 I/O。
            lock (gate)
            {
                if (!capturing) return;
                channels = channelCount;
                int length = Math.Min(data.Length, samples.Length - count);
                Array.Copy(data, 0, samples, count, length);
                count += length;
            }
        }
        /// <summary>在主线程结束并保存 PCM16；缓存所有权在锁内转移，随后释放内存。</summary>
        /// <param name="path">显式指定的录像输出目录中的音频文件。</param>
        public void Finish(string path)
        {
            float[] values; int length, tracks;
            lock (gate)
            {
                capturing = false; values = samples; length = count; tracks = channels;
                samples = null;
            }
            if (values == null || length == 0) { Debug.LogWarning("录像混音没有收到声音样本。"); return; }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)tracks); writer.Write(sampleRate);
                writer.Write(sampleRate * tracks * 2); writer.Write((short)(tracks * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(length * 2);
                for (int i = 0; i < length; i++)
                    writer.Write((short)(Math.Max(-1, Math.Min(1, values[i])) * short.MaxValue));
            }
        }
    }
}
