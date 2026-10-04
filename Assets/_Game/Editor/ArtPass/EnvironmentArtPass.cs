// =============================================================================
// 文件：EnvironmentArtPass.cs
// 职责：为艺术家房间补充天花板、支架、旧草稿与创作工具，保留主物体空间。
// 边界：全部是没有 Collider、SceneEntity 或规则的装饰；仅增量修改已保存场景。
// =============================================================================
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>具体环境布置入口，不建设通用装饰生成器。</summary>
    public static class EnvironmentArtPass
    {
        /// <summary>应用有限的生活痕迹与视觉修正，重复执行复用同名节点。</summary>
        [MenuItem("游戏/美术/布置艺术家房间")]
        public static void Apply()
        {
            var room = PresentationArtPass.Room();
            var decor = PresentationArtPass.Ensure(room.Find("Environment"), "Decoration");
            var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Environment/M_Wall.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Environment/M_WoodDark.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Props/M_Paper.mat");
            var pigment = SurfaceArtPass.Material("Props", "M_DryPigment", new Color(.28f, .24f, .21f));
            var fabric = SurfaceArtPass.Material("Environment", "M_Fabric", new Color(.45f, .39f, .31f));
            var pot = SurfaceArtPass.Material("Props", "M_Ceramic", new Color(.41f, .30f, .21f), .4f);
            // 补上封闭空间的顶面，避免观察上沿露出相机清屏背景。
            SurfaceArtPass.Cube("Ceiling", decor, new Vector3(0, 4.05f, 0), new Vector3(8, .12f, 10), wall);
            room.Find("Effects/WindPaper").localPosition = new Vector3(3.1f, .86f, -.5f);
            // 支架脚必须真的落地，主画周围不再堆放其他画以保证异常可读。
            var visual = room.GetComponentInChildren<PaintingPresenter>(true).transform.Find("Visual");
            visual.Find("EaselLeg").gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                var leg = SurfaceArtPass.Cube("SupportLeg" + i, visual,
                    new Vector3(i == 0 ? -.42f : i == 1 ? .42f : 0, -1.12f, i == 2 ? .36f : .12f), new Vector3(.075f, 1.50f, .075f), wood);
                leg.localRotation = Quaternion.Euler(i == 2 ? -18 : 0, 0, i == 0 ? -12 : i == 1 ? 12 : 0);
            }
            SurfaceArtPass.Cube("EaselRest", visual, new Vector3(0, -.72f, -.015f), new Vector3(1.42f, .09f, .24f), wood);
            // 窗帘仅贴边，始终留出完整海平线；初始静止，有风时参与轻摆。
            var window = room.GetComponentInChildren<WindowPresenter>(true).transform.Find("Visual");
            var curtains = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                curtains[i] = SurfaceArtPass.Cube("Curtain" + i, window, new Vector3(i == 0 ? -1.48f : 1.48f, .02f, -.16f), new Vector3(.19f, 2.39f, .09f), fabric);
                for (int j = 0; j < 3; j++) SurfaceArtPass.Cube("Fold" + j, curtains[i], new Vector3(-.32f + j * .32f, 0, -.35f), new Vector3(.07f, 1, .30f), fabric);
            }
            var wind = room.GetComponentInChildren<WindPresenter>(true);
            var serialized = new SerializedObject(wind);
            var existing = serialized.FindProperty("papers");
            var sheets = Enumerable.Range(0, existing.arraySize).Select(i => (Transform)existing.GetArrayElementAtIndex(i).objectReferenceValue)
                .Where(x => !x.name.StartsWith("Curtain")).Concat(curtains).ToArray();
            wind.Configure((Transform)serialized.FindProperty("plantVisual").objectReferenceValue, sheets, wind.WindAudio);
            // 反复失败的旧画放在左侧背景，既讲述停滞也不挡住核心点击目标。
            for (int i = 0; i < 3; i++)
            {
                var sketch = SurfaceArtPass.Cube("AbandonedCanvas" + i, decor, new Vector3(-3.62f + i * .19f, .57f + i * .06f, .30f + i * .32f), new Vector3(.65f, 1.08f, .045f), paper);
                sketch.localRotation = Quaternion.Euler(-9, 38 + i * 5, i * 3);
                SurfaceArtPass.Cube("UnfinishedStroke", sketch, new Vector3(-.08f, -.10f, -.58f), new Vector3(.58f, .06f, .03f), pigment);
                SurfaceArtPass.Cube("SecondStroke", sketch, new Vector3(.13f, .12f, -.58f), new Vector3(.21f, .24f, .03f), pigment);
            }
            var table = room.Find("Environment/Table");
            Shape("DryCup", table, PrimitiveType.Cylinder, new Vector3(.46f, .95f, .22f), new Vector3(.14f, .12f, .14f), pot);
            Shape("CupInterior", table, PrimitiveType.Cylinder, new Vector3(.46f, 1.074f, .22f), new Vector3(.10f, .003f, .10f), wood);
            var palette = Shape("Palette", table, PrimitiveType.Cylinder, new Vector3(-.44f, .846f, .16f), new Vector3(.43f, .01f, .30f), wood);
            for (int i = 0; i < 3; i++)
            {
                Shape("DryPaint" + i, palette, PrimitiveType.Sphere, new Vector3(-.18f + i * .18f, 1.1f, 0), new Vector3(.12f, .09f, .13f), pigment);
                var brush = SurfaceArtPass.Cube("Brush" + i, table, new Vector3(.34f + i * .07f, 1.08f, .22f), new Vector3(.014f, .36f, .014f), wood);
                brush.localRotation = Quaternion.Euler(i * 8, 0, -15 + i * 15);
                Shape("CrushedPaper" + i, decor, PrimitiveType.Sphere, new Vector3(2.9f + i * .25f, .07f, -.94f - i * .18f), new Vector3(.12f, .10f, .13f), paper);
            }
            // 波纹减弱、湿痕变暗：让水侵入具有质地，而不是亮蓝色的地面标记。
            foreach (string name in new[] { "M_PaintingSea", "M_WindowOcean" })
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Effects/" + name + ".mat").SetFloat("_WaveStrength", .25f);
            room.Find("UI/DebugCanvas/Ending/EndingDetail").GetComponent<Text>().text = "先前留下的笔迹，还在继续。";
            PresentationArtPass.Save(room);
        }
        /// <summary>创建或复用小装饰形状；只用于静态视觉，不带交互组件。</summary>
        private static Transform Shape(string name, Transform parent, PrimitiveType kind, Vector3 position, Vector3 scale, Material material)
        {
            var result = parent.Find(name);
            if (result == null)
            {
                result = GameObject.CreatePrimitive(kind).transform; result.name = name; result.SetParent(parent, false);
                Object.DestroyImmediate(result.GetComponent<Collider>());
            }
            result.localPosition = position; result.localScale = scale; result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }
    }
}
