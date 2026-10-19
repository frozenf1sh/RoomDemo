// 文件：AtelierMirrorContactRevision.cs
// 职责：增量修订静置镜面、物件脚边显色、洗笔说明与画笔离杯高度。
// 边界：只用Editor API修改可玩副本和本批材质，不重建房间或改UI母版。
using System;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>可重复运行的局部修订，所有位置采用绝对值避免重复叠加。</summary>
    public static class AtelierMirrorContactRevision
    {
        private const string Main = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private const string Root = "Assets/_Game/Content/ArtRebuild/FourStage";

        /// <summary>检查人工未保存内容后更新本批配置；不碰正式构建列表和全局质量档。</summary>
        [MenuItem("游戏/新四阶段流程/修订手镜与物件接触显色")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play。");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("存在未保存场景，停止局部修订。");
            var scene = EditorSceneManager.OpenScene(Main);
            var flow = Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if (flow == null || flow.Mirror == null) throw new InvalidOperationException("请先装配前两阶段。");
            Apply(flow);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("局部修订保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[四阶段] 手镜静置面、接触区和洗笔说明已修订。");
        }

        /// <summary>第二阶段装配器也调用同一修订，避免重新安装后退回旧配置。</summary>
        public static void Apply(AtelierFlowGameplay flow)
        {
            var matte = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/静置手镜灰色哑光.mat");
            if (matte == null)
            {
                matte = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/静置手镜哑光面.shader"));
                AssetDatabase.CreateAsset(matte,Root+"/Materials/静置手镜灰色哑光.mat");
            }
            var backing = flow.Mirror.Pivot.Find("Visual_镜面暗色背衬").GetComponent<Renderer>();
            backing.sharedMaterial = matte;
            Changed(backing);
            // 托起约两厘米，笔毛离开杯沿；洗笔仍由原有真实接触判断提交。
            var position = flow.Wash.Brush.position;
            position.y = 1.195f;
            flow.Wash.Brush.position = position;
            Changed(flow.Wash.Brush);
            foreach (var target in Object.FindObjectsByType<AtelierInteractionTarget>())
                if (target.Kind == AtelierTargetKind.WashWater)
                {
                    target.DisplayName = "洗笔杯";
                    Changed(target);
                }

            var root = flow.transform.Find("阶段承托面显色锚点");
            if (root == null)
            {
                root = new GameObject("阶段承托面显色锚点").transform;
                root.SetParent(flow.transform,false);
            }
            AtelierDraftContactRegion Region(string label,int stage,Vector3 center,Vector2 radii)
            {
                var anchor = root.Find(label);
                if (anchor == null) { anchor = new GameObject(label).transform; anchor.SetParent(root,false); }
                anchor.position = center;
                return new AtelierDraftContactRegion { Anchor=anchor,Stage=stage,Radii=radii,HeightTolerance=.025f };
            }
            // 每个锚点仅指向物件下方真实承托面，屋顶不配置任何接触区。
            flow.Draft.ContactRegions = new[] {
                Region("便签周围桌面",1,new Vector3(-.62f,.94f,-1.18f),new Vector2(.40f,.31f)),
                Region("洗笔杯周围桌面",1,new Vector3(-.17f,.94f,-1.18f),new Vector2(.34f,.30f)),
                Region("蜡笔画前窗台",1,new Vector3(2.24f,1.025f,2.02f),new Vector2(.27f,.24f)),
                Region("石膏像脚边",2,new Vector3(-2.30f,.78f,.05f),new Vector2(.32f,.29f)),
                Region("手镜周围桌面",2,new Vector3(-1.15f,.94f,-1.12f),new Vector2(.31f,.32f)),
                Region("干笔周围桌面",2,new Vector3(-2.015f,.94f,-1.295f),new Vector2(.26f,.14f)),
                Region("碎片周围桌面",2,new Vector3(-.80f,.94f,-1.59f),new Vector2(.30f,.16f))
            };
            Changed(flow.Draft);

            if (flow.WaterObservation == null)
            {
                var node = new GameObject("世界说明_洗笔杯与画笔",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
                node.transform.SetParent(flow.Voice.Opacity.transform.parent,false);
                flow.WaterObservation = node.GetComponent<Text>();
            }
            var text = flow.WaterObservation;
            text.font = flow.Voice.Label.font;
            text.fontSize = 26;
            text.lineSpacing = 1.3f;
            text.color = flow.Voice.Label.color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(36,-28);
            rect.sizeDelta = new Vector2(760,110);
            text.gameObject.SetActive(false);
            Changed(flow);
        }

        private static void Changed(Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
    }
}
