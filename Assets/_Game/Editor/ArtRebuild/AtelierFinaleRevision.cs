// =============================================================================
// 文件：AtelierFinaleRevision.cs
// 职责：在已保存的可玩副本中增量接入擦湿色、屋顶与整体调查及统一擦除。
// 边界：只通过 Editor API 创建本工具拥有的资源；不覆盖美术基线，不改 Build Settings。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>安装独立后半段工作簇，重复安装只更新本工具拥有的对象。</summary>
    public static class AtelierFinaleRevision
    {
        private const string Content = "Assets/_Game/Content/ArtRebuild/Gameplay/";
        /// <summary>拒绝未保存场景及运行态；仅修改 AtelierPlayable 的增量内容。</summary>
        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty ||
                scene.path != "Assets/_Game/Content/Scenes/AtelierPlayable.unity")
                throw new InvalidOperationException("请先保存并打开可玩副本，停止运行后再安装后半段。");
            var game = Object.FindAnyObjectByType<AtelierGameplay>();
            var art = GameObject.Find("独立夜间画室").transform;
            var previous = game.transform.Find("湿色与整体擦除工作簇");
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);
            var root = Node("湿色与整体擦除工作簇", game.transform);
            var finale = root.gameObject.AddComponent<AtelierFinalePresenter>();
            finale.ArtRoot = art;
            finale.GameplayRoot = game.transform;
            var state = art.GetComponent<AtelierArtState>();
            finale.Sky = state.LivingSky;
            finale.RoofSurfaces = new[] { state.ClosedRoof.GetComponent<Renderer>(),
                                          art.Find("建筑与木作/有厚度的擦开屋顶").GetComponent<Renderer>() };
            finale.FrameSurfaces =
                art.Find("建筑与木作/旧窗与亚麻帘")
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(r => !r.name.Contains("帘") &&
                                (r.name.Contains("窗") || r.name.Contains("Window") || r.name.Contains("分格木")))
                    .ToArray();
            finale.LocalLights = art.GetComponentsInChildren<Light>(true)
                                     .Concat(game.transform.GetComponentsInChildren<Light>(true))
                                     .Distinct()
                                     .ToArray();
            finale.ErasureLit = Shader.Find("RoomDemo/Atelier/ErasureLit");
            finale.ErasurePaper = Shader.Find("RoomDemo/Atelier/ErasureHandledPaper");
            finale.ErasureWater = Shader.Find("RoomDemo/Atelier/ErasureWashWater");
            finale.ErasurePainting = Shader.Find("RoomDemo/Atelier/ErasureTaskPainting");
            finale.FinaleSky = Shader.Find("RoomDemo/Atelier/FinaleSky");
            finale.ErasureFlame = Shader.Find("RoomDemo/Atelier/ErasureWickFlame");
            finale.ErasureTerrain = Shader.Find("RoomDemo/Atelier/ErasureDistantTerrain");
            if (new[] { finale.ErasureLit, finale.ErasurePaper, finale.ErasureWater, finale.ErasurePainting,
                        finale.ErasureFlame, finale.ErasureTerrain, finale.FinaleSky }
                    .Any(s => s == null))
                throw new InvalidOperationException("连续擦除 Shader 尚未导入。");
            var trial = root.gameObject.AddComponent<AtelierTrialPresenter>();
            var oak = art.Find("建筑与木作/墙根压脚木").GetComponent<Renderer>().sharedMaterial;
            var linen =
                art.GetComponentsInChildren<Renderer>(true).First(r => r.name == "桌沿旧亚麻抹布").sharedMaterial;
            var board = MeshNode(
                "沾湿色的旧木板", root,
                SaveMesh("湿色板_厚边倒角", AtelierMeshLibrary.Timber(new Vector3(.58f, .018f, .32f), .006f)),
                oak);
            board.transform.position = new Vector3(.42f, .972f, -1.35f);
            var paint =
                MeshNode("木纹上的蓝灰湿颜料", root, SaveMesh("湿颜料_贴合表面", Grid(.51f, .25f, 32, 16, false)),
                         Material("湿色薄层", Shader.Find("RoomDemo/Atelier/WetTrial")));
            paint.transform.position = trial.BoardCentre;
            trial.WetPaint = paint.GetComponent<Renderer>();
            var cloth = MeshNode("可以拿起的擦笔布", root,
                                 SaveMesh("擦笔布_卷边褶皱", Grid(.19f, .16f, 40, 28, true)), linen);
            cloth.transform.position = new Vector3(.81f, .985f, -1.36f);
            trial.Cloth = cloth.GetComponent<MeshFilter>();
            Target("Logic_湿色木板", root, new Vector3(.42f, .992f, -1.35f), new Vector3(.54f, .013f, .28f),
                   AtelierTargetKind.Trial, AtelierRules.Trial, "画边的湿色");
            Target("Logic_擦笔布", root, new Vector3(.81f, 1.005f, -1.36f), new Vector3(.20f, .055f, .17f),
                   AtelierTargetKind.TrialCloth, AtelierRules.Trial, "擦笔布");
            // 屋顶逻辑面贴着真实灰泥内侧；擦开后输入只跳过已完全显露的中央。
            Target("Logic_屋顶灰泥", root, new Vector3(0, 4.205f, 0), new Vector3(8.6f, .015f, 8.6f),
                   AtelierTargetKind.Ceiling, StudioRules.Ceiling, "屋顶");
            Target("Logic_擦开后天空", root, new Vector3(0, 4.60f, 0), new Vector3(5.4f, .015f, 6f),
                   AtelierTargetKind.Sky, StudioRules.Sky, "屋顶外的天空");
            // 拾取框贴在窗左侧灰泥实际笔迹上，不生成飘浮的钥匙或新图标。
            float markX = -1.42f;
            float markY = 3.19f + .115f * markX + .05f * Mathf.Sin(markX * 1.8f);
            Target("Logic_贯通的笔迹", root, new Vector3(markX, markY, 2.245f), new Vector3(.42f, .18f, .05f),
                   AtelierTargetKind.SharedMark, AtelierRules.SharedMark, "接连的笔迹");
            Target("Logic_看整间画室", root, new Vector3(4.18f, 2.15f, .40f), new Vector3(.04f, 2.5f, 3.2f),
                   AtelierTargetKind.Room, StudioRules.Room, "整间画室");
            var pose = Node("检视_湿色木板机位", root);
            pose.position = new Vector3(.42f, 1.55f, -2.14f);
            pose.LookAt(new Vector3(.48f, .985f, -1.35f));
            var input = root.gameObject.AddComponent<AtelierContinuationInput>();
            input.Finale = finale;
            input.Trial = trial;
            input.TrialFocusPose = pose;
            game.Finale = finale;
            game.Trial = trial;
            game.Continuation = input;
            // 定稿是玩家明确动作，不是洗笔等实体接触的替代按钮。
            var button = game.ContextAction.GetComponent<RectTransform>();
            button.sizeDelta = new Vector2(160, 38);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "已增量安装湿色接触手势、屋顶与天空亲点、连续笔迹和整体调查；正式入口未变。";
        }
        private static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }
        private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material)
        {
            var node = Node(name, parent);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node.gameObject;
        }
        private static void Target(string name, Transform parent, Vector3 position, Vector3 size,
                                   AtelierTargetKind kind, ObjectId id, string label)
        {
            var node = Node(name, parent);
            node.position = position;
            node.gameObject.layer = 2;
            node.gameObject.AddComponent<BoxCollider>().size = size;
            var t = node.gameObject.AddComponent<AtelierInteractionTarget>();
            t.Kind = kind;
            t.ObjectId = id.Value;
            t.DisplayName = label;
        }
        private static Material Material(string name, Shader shader)
        {
            string path = Content + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }
        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Content + "Meshes/" + name + ".asset";
            mesh.name = name;
            mesh.RecalculateTangents();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        private static Mesh Grid(float width, float depth, int nx, int ny, bool folded)
        {
            var vertices = new Vector3[(nx + 1) * (ny + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[nx * ny * 6];
            int k = 0;
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    int i = y * (nx + 1) + x;
                    float u = x / (float)nx, v = y / (float)ny;
                    float h = folded ? .003f + Mathf.Pow(Mathf.Sin(u * Mathf.PI * 3.4f + .4f), 2) * .012f +
                                           Mathf.Pow(Mathf.Abs(v - .5f) * 2, 8) * .008f
                                     : 0;
                    vertices[i] = new Vector3((u - .5f) * width, h, (v - .5f) * depth);
                    uv[i] = new Vector2(u, v);
                    if (x < nx && y < ny)
                    {
                        triangles[k++] = i;
                        triangles[k++] = i + nx + 1;
                        triangles[k++] = i + 1;
                        triangles[k++] = i + 1;
                        triangles[k++] = i + nx + 1;
                        triangles[k++] = i + nx + 2;
                    }
                }
            var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
