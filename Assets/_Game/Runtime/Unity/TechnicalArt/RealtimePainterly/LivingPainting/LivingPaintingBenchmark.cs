// 文件：LivingPaintingBenchmark.cs
// 职责：独立 18 秒桌面演出、四级对照、局部影响与真实播放器采集。
// 边界：纯技术美术；不加载主房间、不调用游戏规则、不保存领域状态。
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>将缓慢变化记录为可重放的表现时间，停止后仍能查看静态画面。</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class LivingPaintingBenchmark : MonoBehaviour
    {
        public const string TableScene="LivingPaintingBenchmark";
        public const string HeroScene="PainterlyFinalHero";
        public UniversalRenderPipelineAsset Pipeline;
        public Camera View;
        public PainterlyCamera Painterly;
        public PainterlyStyleProfile[] Profiles;
        public LivingStroke Stroke;
        public PainterlyInfluence[] Regions;
        public PainterlyInfluence EarlyObject;
        public PainterlyOpening Opening;
        public QuietSkyMotion Sky;
        public Canvas Controls;
        public Text Label;
        public bool IsHero;
        public bool Playing=true;
        public float SequenceTime;
        private float azimuth=10, openingTime=-1;
        private bool orbit, capturing, installed;
        private RenderPipelineAsset oldDefault,oldQuality;
        private static readonly Vector3 Target=new Vector3(0,1.30f,.12f);

        private void OnEnable()
        {
            if (!Application.isPlaying || Pipeline==null) return;
            oldDefault=GraphicsSettings.defaultRenderPipeline;
            oldQuality=QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline=Pipeline;
            QualitySettings.renderPipeline=Pipeline;
            installed=true;
        }

        private void OnDisable()
        {
            // 只在实验仍拥有设置时恢复，避免覆盖其他场景后来安装的管线。
            if (!installed) return;
            if (GraphicsSettings.defaultRenderPipeline==Pipeline) GraphicsSettings.defaultRenderPipeline=oldDefault;
            if (QualitySettings.renderPipeline==Pipeline) QualitySettings.renderPipeline=oldQuality;
            installed=false;
        }

        private void Start()
        {
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            if (!IsHero) SetSequenceTime(0);
            else Profiles[3].Apply(Painterly);
            UpdateCamera();
            var args=Environment.GetCommandLineArgs();
            for (int i=0;i+1<args.Length;i++)
                if (args[i]=="--living-capture") StartCoroutine(IsHero ? CaptureHero(args[i+1]) : CaptureTable(args[i+1]));
        }

        private void Update()
        {
            if (capturing) return;
            var key=Keyboard.current;
            if (key!=null)
            {
                if (key.spaceKey.wasPressedThisFrame) TogglePlay();
                if (key.rKey.wasPressedThisFrame) Restart();
                if (key.hKey.wasPressedThisFrame) Controls.enabled=!Controls.enabled;
                if (key.oKey.wasPressedThisFrame) ShowOpening();
                if (key.cKey.wasPressedThisFrame) orbit=!orbit;
                if (key.digit1Key.wasPressedThisFrame) Reality();
                if (key.digit2Key.wasPressedThisFrame) Touched();
                if (key.digit3Key.wasPressedThisFrame) Dream();
                if (key.digit4Key.wasPressedThisFrame) Final();
                if (key.fKey.wasPressedThisFrame) SwitchScene();
                if (key.escapeKey.wasPressedThisFrame) Application.Quit();
            }
            if (Playing && !IsHero)
            {
                if (SequenceTime<18) SetSequenceTime(Mathf.Min(18,SequenceTime+Time.unscaledDeltaTime));
                else if (Stroke!=null)
                {
                    // 传播完成后仍继续克制地流动；冻结按钮才停止时间。
                    Stroke.VisualTime+=Time.unscaledDeltaTime;
                    Stroke.LightDistance+=Time.unscaledDeltaTime*.09f;
                    Stroke.Apply();
                }
            }
            if (openingTime>=0 && Opening!=null)
            {
                openingTime+=Time.unscaledDeltaTime;
                Opening.SetRewrite(Smooth(openingTime,0,10));
            }
            if (orbit && !IsHero) azimuth+=Time.unscaledDeltaTime*.45f;
            if (Sky!=null) Sky.AutomaticTime=Playing;
            UpdateCamera();
        }

        private static float Smooth(float time,float start,float end)
        {
            float t=Mathf.InverseLerp(start,end,time);
            return t*t*(3-2*t);
        }

        /// <summary>确定性重放：静止 0～3，流动 3～6，点亮 6～10，延伸 10～16，传播 14～18。</summary>
        /// <param name="time">秒数；该时间仅属于视觉演出。</param>
        public void SetSequenceTime(float time)
        {
            SequenceTime=Mathf.Clamp(time,0,18);
            Profiles[0].Apply(Painterly);
            if (Stroke!=null)
            {
                Stroke.AutomaticTime=false;
                Stroke.VisualTime=Mathf.Max(0,time-3);
                Stroke.LightDistance=Mathf.Max(0,time-6)*.09f;
                Stroke.SetVisualState(Smooth(time,3,6),Smooth(time,6,9),Mathf.Lerp(.42f,1,Smooth(time,10,16)),.27f);
            }
            float spread=Smooth(time,14,18);
            for (int i=0;i<Regions.Length;i++)
            {
                Regions[i].Radii=new Vector3(Mathf.Lerp(.07f,.53f,spread),Mathf.Lerp(.025f,.22f,spread),Mathf.Lerp(.06f,.45f,spread));
                Regions[i].SetPainterlyIntensity(Profiles[2].PainterlyIntensity*spread);
            }
            if (EarlyObject!=null) EarlyObject.SetPainterlyIntensity(Profiles[1].PainterlyIntensity*Smooth(time,5,9));
            string stage=time<3 ? "静止" : time<6 ? "流动" : time<10 ? "点亮" : time<14 ? "延伸" : "局部绘画化";
            if (Label!=null) Label.text="一幅画正在慢慢活过来 · "+stage+"    "+SequenceTime.ToString("F1")+" / 18 秒";
        }

        /// <summary>重新播放同一条路径，清除局部影响与打开进度。</summary>
        public void Restart()
        {
            Playing=true;
            openingTime=-1;
            if (Opening!=null) Opening.SetRewrite(0);
            if (!IsHero) SetSequenceTime(0);
        }
        /// <summary>冻结时间；冻结后单帧仍是完整视觉结果。</summary>
        public void TogglePlay() => Playing=!Playing;
        /// <summary>开始十秒的局部表面擦开，其他物体保持原来的状态。</summary>
        public void ShowOpening() { openingTime=0; if (Opening!=null) Opening.SetRewrite(0); }
        /// <summary>切换到独立 Hero 或桌面实验，不进入正式游戏。</summary>
        public void SwitchScene()
        {
            string next=IsHero ? TableScene : HeroScene;
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Game/Content/Scenes/"+next+".unity",new LoadSceneParameters(LoadSceneMode.Single));
            #else
            SceneManager.LoadScene(next);
            #endif
        }
        public void Reality() => SetProfile(0);
        public void Touched() => SetProfile(1);
        public void Dream() => SetProfile(2);
        public void Final() => SetProfile(3);

        /// <summary>独立对照四档；暂停自动演出并关闭局部权重，避免对照混杂。</summary>
        public void SetProfile(int index)
        {
            index=Mathf.Clamp(index,0,3);
            Playing=false;
            foreach (var region in Regions) region.SetPainterlyIntensity(0);
            if (EarlyObject!=null) EarlyObject.SetPainterlyIntensity(0);
            Profiles[index].Apply(Painterly);
            View.GetUniversalAdditionalCameraData().resetHistory=true;
            if (Label!=null) Label.text=Profiles[index].DisplayName+" · PainterlyIntensity "+Painterly.PainterlyIntensity.ToString("F2");
        }

        private void UpdateCamera()
        {
            if (IsHero) return;
            float angle=azimuth*Mathf.Deg2Rad;
            View.transform.position=Target+new Vector3(Mathf.Sin(angle)*4.5f,2.15f,Mathf.Cos(angle)*4.5f);
            View.transform.LookAt(Target);
        }

        /// <summary>采集真实帧和实际时间戳；迟帧不补画，不加速成假实时。</summary>
        private IEnumerator Record(string directory,float duration,Action<float> update)
        {
            Directory.CreateDirectory(directory);
            float start=Time.realtimeSinceStartup, next=start;
            int frame=0;
            using var times=new StreamWriter(Path.Combine(directory,"times.csv"));
            times.WriteLine("frame,time");
            while (Time.realtimeSinceStartup-start<duration)
            {
                update(Time.realtimeSinceStartup-start);
                yield return new WaitForEndOfFrame();
                if (Time.realtimeSinceStartup<next) continue;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,frame.ToString("D4")+".png"));
                times.WriteLine(frame+","+(Time.realtimeSinceStartup-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture));
                frame++;
                next+=1f/24;
                if (next<Time.realtimeSinceStartup) next=Time.realtimeSinceStartup+1f/24;
            }
        }

        private IEnumerator Still(string path)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.15f);
        }

        private IEnumerator CaptureTable(string directory)
        {
            capturing=true;
            Controls.enabled=false;
            Directory.CreateDirectory(directory);
            for (int i=0;i<4;i++)
            {
                SetProfile(i);
                yield return Still(Path.Combine(directory,"档位_"+i+".png"));
            }
            SetSequenceTime(18);
            yield return Still(Path.Combine(directory,"局部影响.png"));
            SetSequenceTime(0);
            yield return new WaitForSecondsRealtime(1);
            yield return Record(Path.Combine(directory,"sequence"),18,t=>SetSequenceTime(t));
            SetSequenceTime(18);
            yield return Still(Path.Combine(directory,"桌面终帧.png"));
            Controls.enabled=true;
            yield return Still(Path.Combine(directory,"中文面板.png"));
            Controls.enabled=false;
            SetProfile(3);
            yield return Record(Path.Combine(directory,"stability"),12,t=> { azimuth=10+t*.45f; UpdateCamera(); });
            // 第二次以固定世界种子显示锚点，记录桌面四角的投影供离线重投影检查。
            Painterly.DebugWorldCells=true;
            var corners=new[]{new Vector3(-1.9f,1.22f,.92f),new Vector3(1.9f,1.22f,.92f),new Vector3(1.9f,1.22f,-.92f),new Vector3(-1.9f,1.22f,-.92f)};
            using (var points=new StreamWriter(Path.Combine(directory,"世界锚点投影.csv")))
            {
                points.WriteLine("sample,corner,x,y");
                for (int n=0;n<5;n++)
                {
                    azimuth=10+n*1.35f;
                    UpdateCamera();
                    yield return Still(Path.Combine(directory,"锚点_"+n+".png"));
                    for (int p=0;p<4;p++)
                    {
                        var v=View.WorldToScreenPoint(corners[p]);
                        points.WriteLine(n+","+p+","+v.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+(Screen.height-v.y).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }
            Painterly.DebugWorldCells=false;
            Opening.SetRewrite(0);
            yield return Still(Path.Combine(directory,"打开前.png"));
            yield return Record(Path.Combine(directory,"opening"),10,t=>Opening.SetRewrite(Smooth(t,0,10)));
            Opening.SetRewrite(1);
            yield return Still(Path.Combine(directory,"打开后.png"));
            Debug.Log("桌面演出、档位、局部影响与镜头采集完成，进入独立星夜。");
            SwitchScene();
        }

        private IEnumerator CaptureHero(string directory)
        {
            capturing=true;
            Playing=false;
            Controls.enabled=false;
            Sky.AutomaticTime=false;
            Sky.VisualTime=0;
            yield return Still(Path.Combine(directory,"Final星夜.png"));
            yield return Record(Path.Combine(directory,"hero"),12,t=>Sky.VisualTime=t);
            Controls.enabled=true;
            yield return Still(Path.Combine(directory,"Final中文面板.png"));
            Debug.Log("独立星夜与实时倒影采集完成。");
            Application.Quit();
        }
    }
}
