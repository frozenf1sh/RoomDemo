// =============================================================================
// 文件：AudioArtPass.cs
// 职责：生成可替换的原创占位音频并配置这个房间的空间音源。
// 边界：声音只服务表现，不等待音频回调生成事实，不引入通用音频框架。
// =============================================================================
using System.IO;
using Game.Unity;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    /// <summary>确定性占位声音制作工具，后续可替换为同用途的录制音频。</summary>
    public static class AudioArtPass
    {
        /// <summary>逐节拍补充声音，把画、书、水、植物与窗外设为空间声源。</summary>
        [MenuItem("游戏/美术/应用演出声音")]
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            var painting = root.GetComponentInChildren<PaintingPresenter>(true);
            painting.ConfigureAnomalyAudio(Source(painting.transform, "Anomaly", false, .45f));
            painting.SeaAudio.clip = Clip("Sea", true); painting.SeaAudio.volume = .37f;
            var wind = root.GetComponentInChildren<WindPresenter>(true); wind.WindAudio.clip = Clip("Wind", true); wind.WindAudio.volume = .18f;
            root.GetComponentInChildren<PaintingSeaSequence>(true).ConfigureWind(wind);
            var ambience = (AudioSource)new SerializedObject(root.GetComponentInChildren<EndingPresenter>(true)).FindProperty("ambience").objectReferenceValue;
            ambience.clip = Clip("Ambience", true); ambience.volume = .35f;
            foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
            {
                if (audio.transform.IsChildOf(root.Find("UI")) || audio == wind.WindAudio || audio == ambience) continue;
                audio.spatialBlend = 1; audio.minDistance = 2.5f; audio.maxDistance = 15; audio.rolloffMode = AudioRolloffMode.Linear;
            }
            PresentationArtPass.Save(root);
        }
        /// <summary>建立或复用具名音源，配置循环、音量和空间衰减。</summary>
        public static AudioSource Source(Transform parent, string kind, bool loop, float volume, bool spatial = true)
        {
            var node = PresentationArtPass.Ensure(parent, kind + "Cue");
            var audio = node.GetComponent<AudioSource>(); if (audio == null) audio = node.gameObject.AddComponent<AudioSource>();
            audio.clip = Clip(kind, loop); audio.loop = loop; audio.playOnAwake = false; audio.volume = volume;
            audio.spatialBlend = spatial ? 1 : 0; audio.minDistance = 2.5f; audio.maxDistance = 15;
            audio.rolloffMode = AudioRolloffMode.Linear; return audio;
        }
        /// <summary>写标准 PCM WAV；短音和循环音按具体节拍使用不同频率与包络。</summary>
        public static AudioClip Clip(string kind, bool loop)
        {
            const string folder = "Assets/_Game/Content/Audio/Benchmark/";
            Directory.CreateDirectory(folder);
            string path = folder + kind + ".wav";
            const int rate = 22050; int count = rate * (loop ? 8 : 1);
            var random = new System.Random(83 + kind.Length); float low = 0, mid = 0;
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + count * 2);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16); stream.Write((short)1); stream.Write((short)1);
                stream.Write(rate); stream.Write(rate * 2); stream.Write((short)2); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / rate, u = (float)i / count;
                    float noise = (float)random.NextDouble() * 2 - 1;
                    low += (noise - low) * .025f; mid += (noise - mid) * .18f;
                    // 循环端点使用短淡入淡出；主体波动保持轻柔，不让所有节拍同时爆发。
                    float envelope = loop ? Mathf.Min(1, Mathf.Min(t * 5, (count / (float)rate - t) * 5)) : Mathf.Exp(-t * 6);
                    float sample;
                    switch (kind)
                    {
                        case "Anomaly": sample = (Mathf.Sin(t * 970) * .05f + Mathf.Sin(t * 1020) * .04f + mid * .30f) * envelope; break;
                        case "Drop": sample = Mathf.Sin(t * 5600 / (1 + t * 16)) * .48f * Mathf.Exp(-t * 15) + mid * .06f * envelope; break;
                        case "Sea": sample = (low * 1.8f + mid * .18f) * (.55f + .28f * Mathf.Sin(t * 1.57f)); break;
                        case "Water": sample = low * .45f + mid * .04f; break;
                        case "Wind": sample = low * 1.4f * (.65f + .20f * Mathf.Sin(t * .7854f)); break;
                        case "Aquatic": sample = (Mathf.Sin(t * 1700 + Mathf.Sin(t * 40) * 7) * .13f + mid * .24f) * envelope; break;
                        case "Ocean": sample = (low * 1.4f + Mathf.Sin(t * 880) * .035f) * envelope; break;
                        case "Ambience": sample = low * .6f + Mathf.Sin(t * Mathf.PI * 2 * 110) * .025f + Mathf.Sin(t * Mathf.PI * 2 * 165) * .018f; break;
                        case "Paper": sample = mid * .5f * envelope * (.5f + .5f * Mathf.Sin(t * 55)); break;
                        case "Ink": sample = (Mathf.Sin(t * Mathf.PI * 2 * 523) + Mathf.Sin(t * Mathf.PI * 2 * 784)) * .055f * envelope + mid * .10f * envelope; break;
                        case "Scratch": sample = mid * .22f * envelope; break;
                        // 星空样板使用安静虫鸣与低火声；仍是原创占位声音，方便后续替换录音。
                        case "Night": sample = low * .20f + Mathf.Sin(t * 7900) * .07f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 19)), 8) * (.5f + .5f * Mathf.Sin(t * .8f)); break;
                        case "Fire": sample = low * .55f + mid * .16f + (noise > .997f ? noise * .35f : 0); break;
                        default: sample = mid * .15f * envelope; break;
                    }
                    stream.Write((short)(Mathf.Clamp(sample * (loop ? envelope : 1), -1, 1) * 28000));
                }
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
