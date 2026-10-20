// 文件：AtelierStageTwoVisibilityRevision.cs
// 职责：将干画笔和已有碎镜移入固定主画幅，补齐承托物、近景和实际拾取范围。
// 边界：只修改第二阶段视觉与Logic装配，不移动主镜头、不发放来源或词条。
using System;
using System.Linq;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>修复原目标中心位于画面下沿之外的问题，可重复安装。</summary>
    public static class AtelierStageTwoVisibilityRevision
    {
        private const string Root = "Assets/_Game/Content/ArtRebuild/FourStage";

        /// <summary>检查未保存内容后，只保存开发副本的本次摆放修订。</summary>
        [MenuItem("游戏/新四阶段流程/修正第二阶段物件可见性")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play。");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("有未保存场景，停止物件摆放修订。");
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/AtelierPlayable.unity");
            Apply(Object.FindAnyObjectByType<AtelierFlowGameplay>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("物件摆放保存失败。");
            AssetDatabase.SaveAssets();
        }

        /// <summary>装配器共用同一入口，避免重新安装第二阶段又恢复画外摆放。</summary>
        /// <param name="flow">已经装配前两阶段的场景组合根。</param>
        public static void Apply(AtelierFlowGameplay flow)
        {
            if (flow == null || flow.Mirror == null) throw new InvalidOperationException("缺少第二阶段装配。");
            var targets = Object.FindObjectsByType<AtelierInteractionTarget>();
            var dry = targets.Single(x => x.Kind == AtelierTargetKind.DryBrush);
            var broken = targets.Single(x => x.Kind == AtelierTargetKind.MirrorShards);
            var brushRenderer = dry.HoverVisual.Surfaces.Single();
            var brush = brushRenderer.transform.parent;
            // 扫描模型的局部原点不在包围体中心，平移整个视觉Prefab后按实际几何重建拾取盒。
            // 左侧桌面顶为0.78米；笔的真实最低点留约半毫米，不再沿主桌高度悬空。
            brush.position = new Vector3(-2.025f,.8005f,-.315f);
            Changed(brush);
            dry.DisplayName = "黑色干画笔";

            var fragments = flow.Draft.StageTwoObjects.Where(x => x != null && x.name.StartsWith("TA占位_已有碎片_"))
                .OrderBy(x => x.name).ToArray();
            var mesh = FragmentMesh();
            var glass = Material("已有碎镜冷灰面",new Color(.19f,.25f,.30f,1),true);
            for (int index = 0; index < fragments.Length; index++)
            {
                var part = fragments[index];
                part.transform.position = new Vector3(-1.42f+index*.10f,.956f,-.49f-index%2*.10f);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                part.sharedMaterial = glass;
                Changed(part.transform);
                Changed(part.GetComponent<MeshFilter>());
                Changed(part);
            }
            var paper = Material("第二阶段纸套与纸垫",new Color(.58f,.50f,.35f,1),false);
            var pocket = Support(flow.transform,"Visual_碎镜纸套",new Vector3(-1.26f,.951f,-.55f),new Vector3(.50f,.006f,.28f),paper);
            var oldPad = flow.transform.Find("Visual_干画笔纸垫");
            if (oldPad != null)
            {
                var oldSurfaces = oldPad.GetComponentsInChildren<Renderer>();
                flow.Draft.StageTwoObjects = flow.Draft.StageTwoObjects.Where(x=>x!=null && !oldSurfaces.Contains(x)).ToArray();
                Object.DestroyImmediate(oldPad.gameObject);
            }
            broken.HoverVisual.Surfaces = fragments.Concat(pocket.GetComponentsInChildren<Renderer>()).ToArray();
            Resize(dry,brushRenderer.bounds);
            var shardBounds = pocket.GetComponentInChildren<Renderer>().bounds;
            foreach (var part in fragments) shardBounds.Encapsulate(part.bounds);
            Resize(broken,shardBounds);
            flow.DryBrushFocus.SetPositionAndRotation(new Vector3(-2.10f,1.30f,-1.20f),
                Quaternion.LookRotation(brushRenderer.bounds.center-new Vector3(-2.10f,1.30f,-1.20f)));
            flow.ShardsFocus.SetPositionAndRotation(new Vector3(-1.24f,1.45f,-1.45f),
                Quaternion.LookRotation(shardBounds.center-new Vector3(-1.24f,1.45f,-1.45f)));
            flow.Draft.StageTwoObjects = flow.Draft.StageTwoObjects.Concat(pocket.GetComponentsInChildren<Renderer>()).Distinct().ToArray();
            foreach (var region in flow.Draft.ContactRegions)
            {
                if (region.Anchor.name == "干笔周围桌面")
                { region.Anchor.position = new Vector3(-2.10f,.78f,-.29f); region.Radii = new Vector2(.29f,.17f); }
                if (region.Anchor.name == "碎片周围桌面")
                { region.Anchor.position = new Vector3(-1.26f,.94f,-.55f); region.Radii = new Vector2(.35f,.23f); }
            }
            foreach (var value in new Object[] { dry,broken,broken.HoverVisual,flow,flow.Draft,flow.DryBrushFocus,flow.ShardsFocus }) Changed(value);
            // Editor安装后立即查询拾取时，也要刷新物理系统缓存的Collider边界。
            Physics.SyncTransforms();
        }

        private static void Resize(AtelierInteractionTarget target,Bounds bounds)
        {
            target.transform.position = bounds.center;
            target.GetComponent<BoxCollider>().size = bounds.size+new Vector3(.025f,.025f,.025f);
        }

        private static GameObject Support(Transform parent,string label,Vector3 position,Vector3 size,Material material)
        {
            var existing = parent.Find(label);
            var root = existing == null ? new GameObject(label) : existing.gameObject;
            root.transform.SetParent(parent,false);
            if (root.transform.childCount == 0)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Visual_薄纸承托面";
                Object.DestroyImmediate(slab.GetComponent<Collider>());
                slab.transform.SetParent(root.transform,false);
                slab.transform.localScale = size;
                slab.GetComponent<Renderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAssetAndConnect(root,Root+"/Prefabs/PF_"+label+".prefab",InteractionMode.AutomatedAction);
            }
            root.transform.position = position;
            Changed(root.transform);
            return root;
        }

        private static Material Material(string label,Color colour,bool metal)
        {
            string path = Root+"/Materials/"+label+".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null)
            {
                value = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(value,path);
            }
            value.SetColor("_BaseColor",colour);
            if (metal)
            {
                // 小碎片复用静置镜面的有限灰色宽高光，不给每片建立反射摄像机。
                value.shader = AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/静置手镜哑光面.shader");
            }
            else
            {
                value.SetFloat("_Metallic",0);
                value.SetFloat("_Smoothness",.08f);
            }
            EditorUtility.SetDirty(value);
            return value;
        }

        private static Mesh FragmentMesh()
        {
            const string path = Root+"/有厚度碎镜.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            Vector3 a = new Vector3(-.035f,.0015f,-.030f), b = new Vector3(.045f,.0015f,-.015f), c = new Vector3(-.018f,.0015f,.045f);
            var vertices = new[] { a,b,c,a-Vector3.up*.003f,b-Vector3.up*.003f,c-Vector3.up*.003f };
            // 上下两面与三个侧面都实际存在，低角度也保留薄边和正确法线。
            int[] triangles = { 0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5 };
            mesh = new Mesh { name="已有碎镜_三毫米薄边",vertices=vertices,triangles=triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }

        private static void Changed(Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
    }
}
