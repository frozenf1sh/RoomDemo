// 文件：AtelierWalkthroughCapture.cs
// 职责：仅在 Editor 录制显式星夜流程，把 Game 视图逐帧写入 ffmpeg，不进入 Player 构建。
// 边界：只录画面，不操作玩法；Time.captureFramerate 固定帧步进，录制比实时慢也不改变视频速度。
#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>固定帧率采集 Game 视图；结束时关闭 ffmpeg stdin 以写完 MP4。</summary>
    public sealed class AtelierWalkthroughCapture : MonoBehaviour
    {
        public string OutputPath;
        public int Width = 1280, Height = 720, Fps = 30;
        private RenderTexture target;
        private Texture2D frame;
        private Process encoder;
        private Stream input;
        private bool stopping;

        /// <summary>请求停止；采集协程在下一帧关闭编码器并完成文件。</summary>
        public void StopAndClose()
        {
            stopping = true;
        }

        private void Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? ".");
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var start = new ProcessStartInfo
            {
                FileName = "/opt/homebrew/bin/ffmpeg",
                Arguments = $"-hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size {Width}x{Height} -framerate {Fps} -i - -vf vflip -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart -y \"{OutputPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
            encoder = Process.Start(start);
            if (encoder == null) throw new InvalidOperationException("无法启动 ffmpeg 录制。");
            input = encoder.StandardInput.BaseStream;
            // 固定每帧 1/Fps 的游戏时间；真实录屏耗时不会改变视频速度。
            Time.captureFramerate = Fps;
            StartCoroutine(CaptureFrames());
        }

        private System.Collections.IEnumerator CaptureFrames()
        {
            var raw = new byte[Width * Height * 3];
            while (!stopping)
            {
                yield return new WaitForEndOfFrame();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot == null) continue;
                Graphics.Blit(shot, target);
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                frame.Apply();
                frame.GetRawTextureData<byte>().CopyTo(raw);
                RenderTexture.active = null;
                Destroy(shot);
                input.Write(raw, 0, raw.Length);
                input.Flush();
                yield return null;
            }
            input.Close();
            encoder.WaitForExit(30000);
            target.Release();
            Destroy(target);
            Destroy(frame);
            Time.captureFramerate = 0;
            UnityEngine.Debug.Log("[星夜录制] 视频已写入：" + OutputPath);
        }
    }
}
#endif
