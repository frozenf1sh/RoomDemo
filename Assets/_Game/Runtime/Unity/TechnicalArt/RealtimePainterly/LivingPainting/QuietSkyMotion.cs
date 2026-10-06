// 文件：QuietSkyMotion.cs
// 职责：只让指定天空长笔势和三颗沿路径的星光缓慢运动。
// 边界：三维几何表现，不滚动 Skybox，不使房间普通物体一起运动。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>将运动限制到已创作的长笔势，整组漂移幅度不到半个笔触宽度。</summary>
    public sealed class QuietSkyMotion : MonoBehaviour
    {
        public Transform[] Bands;
        public Transform[] Travelers;
        public float VisualTime;
        public bool AutomaticTime=true;
        private Vector3[] initial;

        private void Start()
        {
            initial=new Vector3[Bands.Length];
            for (int i=0;i<Bands.Length;i++) initial[i]=Bands[i].localPosition;
        }

        private void Update()
        {
            if (AutomaticTime) VisualTime+=Time.unscaledDeltaTime;
            if (initial==null) return;
            // 周期约三分钟；方向共用左至右的长笔势，静态星点始终固定。
            for (int i=0;i<Bands.Length;i++)
                Bands[i].localPosition=initial[i]+new Vector3(Mathf.Sin(VisualTime*.032f+i*.7f)*.16f,Mathf.Sin(VisualTime*.025f+i)*.055f,0);
            for (int i=0;i<Travelers.Length;i++)
            {
                float x=-12+i*9+VisualTime*.024f;
                float y=6.3f+.75f*Mathf.Sin(x*.09f)+.22f*Mathf.Sin(x*.23f);
                Travelers[i].localPosition=new Vector3(x,y,24.8f);
            }
        }
    }
}
