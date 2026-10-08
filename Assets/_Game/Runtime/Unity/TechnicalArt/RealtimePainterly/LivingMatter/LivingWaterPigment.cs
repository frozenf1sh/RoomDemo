// 文件：LivingWaterPigment.cs
// 职责：组织十秒的杯内流动、颜料旋转、碰杯口、落桌与湿膜传播。
// 边界：仅近似液体表现，不实现通用物理模拟，不参与游戏路线或世界事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>以同一个时钟连接液面、杯口、落水和桌面反馈纹理。</summary>
    public sealed class LivingWaterPigment : MonoBehaviour
    {
        public GpuPigment Cup,Wet;
        public Renderer Water,Sheet;
        public Transform[] Drops;
        public Vector3 Lip=new Vector3(.07f,1.17f,.29f);
        public Vector3 Landing=new Vector3(.47f,.009f,.52f);
        public float VisualTime;
        private Mesh sheetMesh;
        private Vector3[] vertices;
        private MaterialPropertyBlock properties;
        private const int Along=40,Across=16;

        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            properties=new MaterialPropertyBlock();
            sheetMesh=new Mesh { name="运行时自由液体流束",hideFlags=HideFlags.HideAndDontSave };
            sheetMesh.MarkDynamic();
            vertices=new Vector3[(Along+1)*(Across+1)];
            var uv=new Vector2[vertices.Length];var triangles=new int[Along*Across*6];int index=0;
            for(int a=0;a<=Along;a++) for(int b=0;b<=Across;b++)
            {
                int n=a*(Across+1)+b;uv[n]=new Vector2((float)b/Across,(float)a/Along);
                if(a==Along || b==Across) continue;
                triangles[index++]=n;triangles[index++]=n+1;triangles[index++]=n+Across+1;
                triangles[index++]=n+1;triangles[index++]=n+Across+2;triangles[index++]=n+Across+1;
            }
            sheetMesh.vertices=vertices;sheetMesh.uv=uv;sheetMesh.triangles=triangles;
            Sheet.GetComponent<MeshFilter>().sharedMesh=sheetMesh;
            Cup.AutomaticTime=false;Wet.AutomaticTime=false;
        }

        private static float Ease(float t,float begin,float end)
        {
            float f=Mathf.InverseLerp(begin,end,t);return f*f*(3-2*f);
        }

        /// <summary>推进液体的实际时间；重放必须先 Reset，避免保留上一轮湿润。</summary>
        public void Advance(float seconds)
        {
            VisualTime+=seconds;
            float flow=Ease(VisualTime,1.7f,3.3f);
            float ink=Ease(VisualTime,3.0f,4.5f);
            float spill=Ease(VisualTime,5.4f,6.8f);
            Cup.Flow=flow;Cup.Injection=ink;
            Wet.Injection=Ease(VisualTime,6.2f,7.0f)*.86f;
            Cup.Advance(seconds);Wet.Advance(seconds);
            Water.GetPropertyBlock(properties);
            properties.SetFloat("_Flow",flow);properties.SetFloat("_Surge",Ease(VisualTime,4.7f,6.0f));
            properties.SetFloat("_CupPhase",Mathf.Max(0,VisualTime-1.7f));Water.SetPropertyBlock(properties);
            Sheet.enabled=spill>.001f;
            if(sheetMesh!=null && spill>0)
            {
                // 按重力轨迹建立圆截面流束；截面随下落加速收缩，并保留少量流体鼓包。
                for(int a=0;a<=Along;a++)
                {
                    float s=(float)a/Along;
                    float t=s*Mathf.Min(1,spill*1.4f);
                    Vector3 centre=Vector3.Lerp(Lip,Landing,t);
                    centre.y=Mathf.Lerp(Lip.y,Landing.y,t*t);
                    centre.x+=Mathf.Sin(t*Mathf.PI)*.045f;
                    Vector3 tangent=new Vector3(Landing.x-Lip.x+.045f*Mathf.PI*Mathf.Cos(t*Mathf.PI),
                        (Landing.y-Lip.y)*2*Mathf.Max(.08f,t),Landing.z-Lip.z).normalized;
                    Vector3 axis=Vector3.Cross(tangent,Vector3.forward).normalized;
                    Vector3 other=Vector3.Cross(tangent,axis).normalized;
                    float radius=Mathf.Lerp(.043f,.021f,t)*(1+.16f*Mathf.Sin(t*29-VisualTime*5));
                    for(int b=0;b<=Across;b++)
                    {
                        float angle=(float)b/Across*Mathf.PI*2;
                        var point=centre+(axis*Mathf.Cos(angle)+other*Mathf.Sin(angle))*radius;
                        point.z+=Mathf.Sin(t*21-VisualTime*4)*.006f*(1-t);
                        vertices[a*(Across+1)+b]=point;
                    }
                }
                sheetMesh.vertices=vertices;sheetMesh.RecalculateNormals();sheetMesh.RecalculateBounds();
                Sheet.GetPropertyBlock(properties);properties.SetTexture("_Pigment",Cup.Pigment);
                properties.SetFloat("_Flow",flow);properties.SetFloat("_CupPhase",VisualTime);
                properties.SetFloat("_Sheet",1);properties.SetFloat("_Opacity",.64f*spill);Sheet.SetPropertyBlock(properties);
            }
            for(int i=0;i<Drops.Length;i++)
            {
                var drop=Drops[i];bool active=VisualTime>5.9f+i*.13f;
                drop.gameObject.SetActive(active);
                if(!active) continue;
                float age=Mathf.Repeat(VisualTime-5.9f-i*.29f,1.12f);
                float t=Mathf.Min(1,age/.69f);
                var p=Lip+new Vector3(.13f+t*.39f,0,.035f+Mathf.Sin(i*2.7f)*.06f);
                p.y=Lip.y+.16f*t-1.32f*t*t;
                drop.position=p;
                drop.gameObject.SetActive(age<.69f);
                float size=.028f+Mathf.Sin(i*3)*.007f;
                drop.localScale=new Vector3(size,size*(1+2*t),size);
            }
        }

        /// <summary>返回静水；只清理本演出拥有的颜料纹理和液体几何。</summary>
        public void ResetWater()
        {
            VisualTime=0;Cup.ResetPigment();Wet.ResetPigment();Advance(0);
        }

        private void OnDisable()
        {
            if(sheetMesh!=null) Destroy(sheetMesh);
        }
    }
}
