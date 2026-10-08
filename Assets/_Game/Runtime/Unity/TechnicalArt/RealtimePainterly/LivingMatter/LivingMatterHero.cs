// 文件：LivingMatterHero.cs
// 职责：两个独立 Hero 的播放、暂停、真实时间采集与帧时间记录。
// 边界：只安装本实验管线；退出时恢复自己拥有的设置，不进入正式游戏或游戏路线。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>固定镜头展示颜料内部运动，不借镜头运动制造动态。</summary>
    public sealed class LivingMatterHero : MonoBehaviour
    {
        public const string SkyScene="LivingStarryNightHero",WaterScene="LivingWaterPigmentHero";
        public RenderPipelineAsset Pipeline;
        public GpuPigment Sky;
        public LivingWaterPigment Water;
        public Camera View;
        public bool Playing=true;
        private RenderPipelineAsset previousDefault,previousQuality;
        private bool ownsPipeline,capturing;
        private int previousVsync,previousTargetFrameRate;
        private bool previousRunInBackground;
        private readonly List<float> timing=new List<float>();

        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            previousDefault=GraphicsSettings.defaultRenderPipeline;previousQuality=QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline=Pipeline;QualitySettings.renderPipeline=Pipeline;
            previousVsync=QualitySettings.vSyncCount;previousTargetFrameRate=Application.targetFrameRate;
            previousRunInBackground=Application.runInBackground;
            QualitySettings.vSyncCount=0;ownsPipeline=true;
        }

        private void Start()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;
            if(Sky!=null) Sky.AutomaticTime=false;
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)
                if(args[i]=="--matter-capture") StartCoroutine(Capture(args[i+1]));
        }

        private void Update()
        {
            if(capturing) return;
            var key=Keyboard.current;
            if(key!=null)
            {
                if(key.spaceKey.wasPressedThisFrame) Playing=!Playing;
                if(key.rKey.wasPressedThisFrame) ResetHero();
                if(key.fKey.wasPressedThisFrame) SwitchScene();
                if(key.escapeKey.wasPressedThisFrame) Application.Quit();
            }
            if(Playing) Advance(Time.unscaledDeltaTime);
        }

        /// <summary>只推进颜料和物质状态；摄像机的位置与投影保持固定。</summary>
        public void Advance(float seconds)
        {
            if(Sky!=null) Sky.Advance(seconds);
            if(Water!=null) Water.Advance(seconds);
        }

        /// <summary>重放当前 Hero，清除本轮湿膜和颜料历史。</summary>
        public void ResetHero()
        {
            Playing=true;if(Sky!=null) Sky.ResetPigment();if(Water!=null) Water.ResetWater();
            View.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().resetHistory=true;
        }

        /// <summary>在两个独立 Hero 之间切换，保留正式场景的构建配置。</summary>
        public void SwitchScene()
        {
            string next=Sky!=null ? WaterScene : SkyScene;
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Game/Content/Scenes/"+next+".unity",new LoadSceneParameters(LoadSceneMode.Single));
            #else
            SceneManager.LoadScene(next);
            #endif
        }

        private IEnumerator Capture(string directory)
        {
            capturing=true;Directory.CreateDirectory(directory);
            string name=Sky!=null ? "sky" : "water";
            var folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            ResetHero();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+"_开始.png"));
            yield return new WaitForSecondsRealtime(.15f);
            float start=Time.realtimeSinceStartup,next=start;
            float previous=start,duration=Sky!=null ? 15 : 10;
            int frame=0;
            using(var output=new StreamWriter(Path.Combine(folder,"times.csv")))
            {
                output.WriteLine("frame,time");
                while(Time.realtimeSinceStartup-start<duration)
                {
                    float now=Time.realtimeSinceStartup;Advance(now-previous);previous=now;
                    yield return new WaitForEndOfFrame();
                    if(Time.realtimeSinceStartup<next) continue;
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder,frame.ToString("D4")+".png"));
                    output.WriteLine(frame+","+(Time.realtimeSinceStartup-start).ToString("F5",CultureInfo.InvariantCulture));
                    frame++;next+=1f/24;
                    if(next<Time.realtimeSinceStartup) next=Time.realtimeSinceStartup+1f/24;
                }
            }
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+"_结束.png"));
            // 性能段不写 PNG，记录真正运行时帧间隔，避免把截图磁盘成本当成渲染性能。
            timing.Clear();start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<5)
            {
                Advance(Time.unscaledDeltaTime);timing.Add(Time.unscaledDeltaTime*1000);yield return null;
            }
            timing.Sort();
            File.WriteAllText(Path.Combine(directory,name+"_性能.json"),
                "{\"frame_count\":"+timing.Count+",\"median_ms\":"+timing[timing.Count/2].ToString("F3",CultureInfo.InvariantCulture)+
                ",\"p95_ms\":"+timing[Mathf.Min(timing.Count-1,Mathf.FloorToInt(timing.Count*.95f))].ToString("F3",CultureInfo.InvariantCulture)+
                ",\"target_fps\":60,\"width\":"+Screen.width+",\"height\":"+Screen.height+"}");
            Debug.Log((Sky!=null ? "十五秒纯天空" : "十秒杯中水与颜料")+"真实录像与独立性能采集完成。");
            if(Sky!=null) SwitchScene();else Application.Quit();
        }

        private void OnDisable()
        {
            if(!ownsPipeline) return;
            if(GraphicsSettings.defaultRenderPipeline==Pipeline) GraphicsSettings.defaultRenderPipeline=previousDefault;
            if(QualitySettings.renderPipeline==Pipeline) QualitySettings.renderPipeline=previousQuality;
            QualitySettings.vSyncCount=previousVsync;
            Application.targetFrameRate=previousTargetFrameRate;Application.runInBackground=previousRunInBackground;
            ownsPipeline=false;
        }
    }
}
