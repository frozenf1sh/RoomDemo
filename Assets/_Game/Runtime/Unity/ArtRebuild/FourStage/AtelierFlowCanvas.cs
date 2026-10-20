// 文件：AtelierFlowCanvas.cs
// 职责：以领域笔触列表重建真实世界画布，并提供UI可绑定的提交、撤销与收层接口。
// 边界：作品RT归本组件所有；UI只读句柄。预览和作品分开，未提交不会改变画作。
using System;
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity.ArtRebuild
{
    /// <summary>最多四层、每层五六枚笔印；当前以1024平方RT验证真实图样。</summary>
    public sealed class AtelierFlowCanvas : MonoBehaviour
    {
        public Renderer Surface;
        public Texture2D[] Brushes;
        public Shader CanvasShader, StampShader, SpreadShader;
        public int Resolution = 1024;
        public TextAsset BrushCalibration;
        [Serializable] private sealed class BrushShape { public Vector4 rect; public float aspect,defaultSpan; public byte[] alpha; }
        [Serializable] private sealed class Calibration { public BrushShape[] brushes; }
        private Calibration calibration;
        private GameSession session;
        private Material original, owned, stamp, spread;
        private RenderTexture artwork;
        private int renderedRevision = -1;
        private Vector2 previewUV;
        private bool preview;
        /// <summary>真实作品纹理，由场景拥有，UI不得释放。</summary>
        public Texture CanvasTexture => artwork;
        /// <summary>UI读取相同领域账本，适配器不另存颜料和笔数。</summary>
        public AtelierCanvasDocument Document => session?.State.AtelierCanvas;
        /// <summary>正式UI与当前按钮读取同一领域许可，不能只用笔数判断第三层可完成。</summary>
        public string FinishBlockedReason => session==null ? "画布尚未准备好。"
            : AtelierCanvasOperations.FinishBlockedReason(session.State,session.Rules,Document.Revision) ?? "";
        /// <summary>包括三笔与刮刀两面等真实门槛。</summary>
        public bool CanFinish => string.IsNullOrEmpty(FinishBlockedReason);
        /// <summary>绑定并从笔触命令列表恢复，不从旧静物显露进度生成图像。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            calibration = JsonUtility.FromJson<Calibration>(BrushCalibration.text);
            original = Surface.sharedMaterial;
            owned = new Material(CanvasShader) { name = "主流程_自有世界画布" };
            stamp = new Material(StampShader);
            if(SpreadShader!=null) spread=new Material(SpreadShader);
            artwork = new RenderTexture(Resolution,Resolution,0,RenderTextureFormat.ARGB32) { name = "主流程_领域笔印作品" };
            artwork.Create();
            Surface.sharedMaterial = owned;
            Surface.SetPropertyBlock(null);
            owned.SetTexture("_BaseMap",artwork);
            session.EventRaised += OnEvent;
            Rebuild();
        }
        private void OnEvent(DomainEvent e) { if (e is AtelierCanvasChanged change) Rebuild(change.Operation=="spread"); }
        /// <summary>选择可行区间内的默认量；正式UI可传入自己的合法大小选择。</summary>
        public int DefaultCost
        {
            get
            {
                var layer = Document.Current;
                if (layer.RemainingPaint <= 0) return 0;
                return Mathf.Clamp(Mathf.CeilToInt(layer.RemainingPaint/(float)(layer.PlannedStrokes-layer.Count)),layer.MinCost,layer.MaxCost);
            }
        }
        /// <summary>当前临时预览不进入领域，可在返回、失焦、UI捕获时取消。</summary>
        public void Preview(Vector2 uv, bool visible, int units = -1, int angle = 0)
        {
            previewUV = uv; preview = visible;
            owned.SetFloat("_Preview", visible ? 1 : 0);
            if (!visible) return;
            int layer = Document.Current.Id;
            owned.SetTexture("_Brush",Brushes[layer-1]);
            int selected = units < 0 ? DefaultCost : units;
            owned.SetVector("_Placement",new Vector4(uv.x,uv.y,0,angle*Mathf.Deg2Rad));
            SetBrushShape(owned,layer,selected);
            owned.SetFloat("_PreviewValid",IsPlacementValid(uv,selected,angle)?1:0);
        }
        /// <summary>从实际画布UV提交一枚；同一世代、版本、笔触ID交给应用层复核。</summary>
        public AtelierCanvasResult Commit(Vector2 uv, int units, int angle, string id = null)
        {
            if (!IsPlacementValid(uv,units,angle)) return new AtelierCanvasResult(false,"笔印需要更多地落在画布内。");
            var request = new AtelierStroke(id ?? Guid.NewGuid().ToString("N"),Document.Current.Id,
                Mathf.RoundToInt(uv.x*10000),Mathf.RoundToInt(uv.y*10000),angle,units);
            return session.CommitAtelierStroke(session.SessionEpoch,Document.Revision,request);
        }
        /// <summary>正式UI撤销按钮复用此入口，精确退款并重建当前作品。</summary>
        public AtelierCanvasResult Undo() => session.UndoAtelierStroke(session.SessionEpoch,Document.Revision);
        /// <summary>正式UI完成按钮复用此入口，不把用尽颜料等同于自动完成。</summary>
        public AtelierCanvasResult Finish() => session.FinishAtelierLayer(session.SessionEpoch,Document.Revision);
        private Vector2 Scale(int layer,int units)
        {
            var shape=calibration.brushes[layer-1];
            float span=shape.defaultSpan*Mathf.Sqrt(Mathf.Max(0,units)/18f);
            return new Vector2(span*Mathf.Min(1,shape.aspect),span*Mathf.Min(1,1/shape.aspect));
        }
        private void SetBrushShape(Material material,int layer,int units)
        {
            Vector2 scale=Scale(layer,units);
            material.SetVector("_StampScale",new Vector4(scale.x,scale.y,0,0));
            material.SetVector("_AlphaRect",calibration.brushes[layer-1].rect);
        }
        /// <summary>只防止笔印大半误落画外；重叠、构图和覆盖率不参与完成判定。</summary>
        public bool IsPlacementValid(Vector2 uv,int units,int angle)
        {
            if (uv.x<0||uv.x>1||uv.y<0||uv.y>1||units<=0) return false;
            var shape=calibration.brushes[Document.Current.Id-1];
            Vector2 scale=Scale(Document.Current.Id,units);
            float c=Mathf.Cos(angle*Mathf.Deg2Rad),s=Mathf.Sin(angle*Mathf.Deg2Rad);
            int total=0,visible=0;
            // 有限16平方Alpha质量样本与Shader共用裁边、大小和旋转，不按空白PNG边长计。
            for(int y=0;y<16;y++) for(int x=0;x<16;x++)
            {
                int weight=shape.alpha[y*16+x];total+=weight;
                Vector2 p=new Vector2(((x+.5f)/16-.5f)*scale.x,((y+.5f)/16-.5f)*scale.y);
                Vector2 onCanvas=uv+new Vector2(c*p.x-s*p.y,s*p.x+c*p.y);
                if(onCanvas.x>=0&&onCanvas.x<=1&&onCanvas.y>=0&&onCanvas.y<=1) visible+=weight;
            }
            return total>0&&visible>=total*.35f;
        }
        private void Rebuild(bool force=false)
        {
            if (!force && Document.Revision == renderedRevision) return;
            var old = RenderTexture.active;
            bool matrixPushed = false;
            try
            {
                RenderTexture.active = artwork;
                GL.Clear(true,true,new Color(.018f,.035f,.085f,1));
                GL.PushMatrix();
                matrixPushed = true;
                GL.LoadOrtho();
                foreach (var layer in Document.Layers)
                    foreach (var brush in layer.Strokes)
                    {
                        stamp.SetTexture("_Brush",Brushes[layer.Id-1]);
                        stamp.SetVector("_Placement",new Vector4(brush.U/10000f,brush.V/10000f,0,brush.Angle*Mathf.Deg2Rad));
                        SetBrushShape(stamp,layer.Id,brush.PaintUnits);
                        stamp.SetPass(0);
                        // 每枚由原始PNG透明叠加，纹理不能自动补成标准构图。
                        GL.Begin(GL.QUADS);
                        GL.TexCoord2(0,0);GL.Vertex3(0,0,0); GL.TexCoord2(1,0);GL.Vertex3(1,0,0);
                        GL.TexCoord2(1,1);GL.Vertex3(1,1,0); GL.TexCoord2(0,1);GL.Vertex3(0,1,0);
                        GL.End();
                    }
                renderedRevision = Document.Revision;
            }
            finally
            {
                // 异常时也恢复全局绘制状态，不能污染后续相机或UI的渲染。
                if (matrixPushed) GL.PopMatrix();
                RenderTexture.active = old;
            }
            if(spread!=null && Document.SpreadRevision==Document.Revision && Document.SpreadPath.Count>0)
            {
                var path=new Vector4[64];
                for(int n=0;n<Document.SpreadPath.Count;n++)
                {
                    var p=Document.SpreadPath[n];path[n]=new Vector4(p.U0/10000f,p.V0/10000f,p.U1/10000f,p.V1/10000f);
                }
                spread.SetVectorArray("_SpreadPath",path);
                spread.SetInt("_SpreadCount",Document.SpreadPath.Count);
                var temporary=RenderTexture.GetTemporary(artwork.descriptor);
                try { Graphics.Blit(artwork,temporary,spread);Graphics.Blit(temporary,artwork); }
                finally { RenderTexture.ReleaseTemporary(temporary); }
            }
            // 作品版本改变后取消旧预览，下一次由UI或世界输入根据新快照建立。
            Preview(Vector2.zero,false);
        }
        private void OnDestroy()
        {
            if (session != null) session.EventRaised -= OnEvent;
            if (Surface != null && original != null) Surface.sharedMaterial = original;
            if (artwork != null) { artwork.Release();Destroy(artwork); }
            if (owned != null) Destroy(owned);
            if (stamp != null) Destroy(stamp);
            if (spread != null) Destroy(spread);
        }
    }
}
