// 文件：AtelierPolish.cs
// 职责：按实际主镜头修正摆放接触、夜间天空、窗灯和绘画滤镜，并为新布局准备光照。
// 架构边界：只处理独立美术场景及其专属副本，不修改第三方原件和领域规则。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly;

namespace Game.Editor.ArtRebuild
{
    /// <summary>对真实运行画面中出现的问题做可重复的局部修正。</summary>
    public static class AtelierPolish
    {
        private const string Art = "Assets/_Game/Content/ArtRebuild/";

        /// <summary>用贴合连续坡面的土路连接房屋，不让房子只是分散摆在地面上。</summary>
        public static void ConnectVillagePaths()
        {
            var landscape = Root().Find("窗外山谷");
            var group = landscape.Find("返工_村落土路") ?? AtelierSceneBuilder.Group(landscape, "返工_村落土路");
            var source = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/扫描泥地与碎叶.mat");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/村落踩实土路.mat");
            if (mat == null) { mat = new Material(source); AssetDatabase.CreateAsset(mat, Art + "Materials/村落踩实土路.mat"); }
            mat.SetColor("_BaseColor", new Color(.65f, .54f, .38f));
            mat.SetFloat("_BumpScale", .32f);
            mat.SetFloat("_Smoothness", .04f);
            EditorUtility.SetDirty(mat);
            // 主路绕开屋体；三条短支路接到模型真实门板的中心。
            PathStrip(group, "谷地弯曲主路", new[] {new Vector2(3,18),new Vector2(5,25),new Vector2(9,29.5f),new Vector2(17,40),new Vector2(33,45),new Vector2(35,58),new Vector2(38,73)}, 1.25f, mat);
            var houses = landscape.Find("返工_完整旧屋");
            var junctions = new[] {new Vector2(9,29.5f),new Vector2(34,50),new Vector2(37,68)};
            for (int i = 0; i < houses.childCount; i++)
            {
                var r = houses.GetChild(i).GetComponentInChildren<MeshRenderer>();
                var f = r.GetComponent<MeshFilter>();
                int slot = Array.FindIndex(r.sharedMaterials, m => m.name == "旧屋扫描_Door");
                var indices = f.sharedMesh.GetTriangles(slot).Distinct().ToArray();
                Vector3 centre = Vector3.zero;
                foreach (int v in indices) centre += f.transform.TransformPoint(f.sharedMesh.vertices[v]);
                centre /= indices.Length;
                var door = new Vector2(centre.x, centre.z);
                PathStrip(group, "通向旧屋的支路_" + i, new[] {junctions[i],Vector2.Lerp(junctions[i],door,.5f) + new Vector2(.3f,-.4f),door}, .85f, mat);
            }
            AtelierSceneBuilder.Save();
        }

        /// <summary>沿平滑曲线创建起伏土路，边缘轻微不齐且每个顶点均取同一坡地高度。</summary>
        private static void PathStrip(Transform parent, string name, Vector2[] knots, float width, Material material)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            const int steps = 120;
            Vector2 Curve(float u)
            {
                float q = Mathf.Clamp(u, 0, .99999f) * (knots.Length - 1); int k = Mathf.FloorToInt(q); float t = q - k;
                var a = knots[Mathf.Max(0,k-1)]; var b = knots[k]; var c = knots[k+1]; var d = knots[Mathf.Min(knots.Length-1,k+2)];
                return .5f * ((2*b) + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t);
            }
            float length = 0; Vector2 last = knots[0];
            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps; var p = Curve(u); length += Vector2.Distance(last,p); last = p;
                var direction = (Curve(Mathf.Min(1,u+.003f))-Curve(Mathf.Max(0,u-.003f))).normalized;
                var side = new Vector2(-direction.y,direction.x);
                for (int j = 0; j <= 8; j++)
                {
                    float across = j / 8f; float edge = width * (.9f + .09f * Mathf.Sin(i*.73f) + .05f * Mathf.Sin(i*.19f));
                    var point = p + side * ((across-.5f)*edge);
                    vertices.Add(new Vector3(point.x,AtelierRevision.Height(point.x,point.y)+.028f,point.y));
                    uv.Add(new Vector2(across,length*.5f));
                }
            }
            for (int i = 0; i < steps; i++) for (int j = 0; j < 8; j++)
            { int a=i*9+j; triangles.AddRange(new[] {a,a+10,a+9,a,a+1,a+10}); }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals();
            var node = parent.Find(name);
            if (node == null) AtelierSceneBuilder.Node(parent,name,AtelierMeshLibrary.Save(name,mesh),material,Vector3.zero);
            else node.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save(name,mesh);
        }

        /// <summary>按主镜头检查结果校准织物、书架尺度和真正落在窗边的局部受光。</summary>
        public static void RefineLightAndSurfaces()
        {
            var root = Root();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Art + "Rendering/画室专用管线.asset");
            // 保留八盏局部灯的可见贡献，避免默认四灯上限把窗边冷光挤掉。
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);
            var linen = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/旧亚麻.mat");
            linen.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/亚麻去色织纹.png"));
            linen.SetColor("_BaseColor", new Color(.78f, .72f, .60f));
            linen.SetFloat("_BumpScale", .55f);
            linen.SetFloat("_Smoothness", .04f);
            EditorUtility.SetDirty(linen);
            var lights = root.Find("照明");
            var cold = lights.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>();
            cold.intensity = 5.8f;
            cold.range = 3.4f;
            cold.color = new Color(.32f, .49f, .88f);
            var moonSpot = lights.Find("从窗洞进入的冷月光").GetComponent<Light>();
            moonSpot.intensity = 6.2f;
            lights.Find("夜间微弱环境方向光").GetComponent<Light>().intensity = .28f;
            lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>().intensity = 2.5f;
            lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>().range = 4.0f;
            var scans = root.Find("返工_扫描工作簇");
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float x = 3.0f;
                for (int i = 0; i < 9; i++)
                {
                    var book = scans.Find("书架皮面旧卷_" + shelf + "_" + i);
                    book.localScale *= (.35f + .025f * (i % 3)) / BoundsOf(book).size.y;
                    book.position += Vector3.right * (x - BoundsOf(book).min.x);
                    RestOn(book, .89f + shelf * .64f);
                    x = BoundsOf(book).max.x + .007f + (i == 5 ? .065f : 0);
                }
            }
            var rag = root.Find("桌面与创作痕迹/桌沿旧亚麻抹布");
            rag.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("不对称落桌抹布", DrapedCloth());
            var ragMat = rag.GetComponent<MeshRenderer>().sharedMaterial;
            ragMat.SetTexture("_BaseMap", linen.GetTexture("_BaseMap"));
            ragMat.SetColor("_BaseColor", new Color(.75f, .71f, .62f));
            EditorUtility.SetDirty(ragMat);
            AtelierSceneBuilder.Save();
        }

        /// <summary>增加有实际用途的辅助工作簇与弱灯源，补足画架和左墙的阅读层次。</summary>
        public static void SecondaryWorkCluster()
        {
            var root=Root();
            if(root.Find("返工_辅助工作簇")!=null)throw new InvalidOperationException("辅助工作簇已存在。");
            var group=AtelierSceneBuilder.Group(root,"返工_辅助工作簇");
            var table=UnityEngine.Object.Instantiate(root.Find("室内家具/有磨损的扫描工作台").gameObject,group).transform;
            table.name="旧扫描辅助工作台";table.gameObject.SetActive(true);
            table.localScale*=.78f/BoundsOf(table).size.y;
            table.position=new Vector3(-3.15f,0,-.12f);RestOn(table,0);
            float top=BoundsOf(table).max.y;
            var lamp=UnityEngine.Object.Instantiate(root.Find("返工_扫描工作簇/扫描维多利亚旧油灯").gameObject,group).transform;
            lamp.name="辅助工作台上的小油灯";lamp.localScale*=.70f;
            lamp.position=new Vector3(-3.60f,top,-.10f);RestOn(lamp,top+.003f);
            var light=new GameObject("辅助油灯的局部暖光",typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(root.Find("照明"));light.transform.position=new Vector3(-3.60f,BoundsOf(lamp).max.y-.20f,-.10f);
            light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Mixed;
            light.intensity=1.7f;light.range=3.2f;light.color=new Color(1,.73f,.45f);light.shadows=LightShadows.Soft;light.shadowStrength=.7f;
            var props=root.Find("桌面与创作痕迹");
            var palette=UnityEngine.Object.Instantiate(props.Find("带颜料的真实调色板").gameObject,group).transform;
            palette.name="辅助调色板";palette.position=new Vector3(-3.10f,top,-.32f);RestOn(palette,top+.003f);
            var scans=root.Find("返工_扫描工作簇");
            for(int shelf=0;shelf<2;shelf++)
            {
                float y=.89f+shelf*.64f;
                for(int i=0;i<3;i++)
                {
                    var book=UnityEngine.Object.Instantiate(scans.Find("桌面摊放皮卷_"+i).gameObject,group).transform;
                    book.name="书架右侧横叠皮卷_"+shelf+"_"+i;
                    book.position=new Vector3(4.02f,0,.77f+i*.008f);RestOn(book,y);
                    y=BoundsOf(book).max.y-.001f;
                }
            }
            var vase=UnityEngine.Object.Instantiate(props.Find("备用陶器").gameObject,group).transform;
            vase.name="辅助台的旧颜料陶罐";vase.position=new Vector3(-2.70f,top,.03f);RestOn(vase,top);
            var sky=root.GetComponent<AtelierArtState>().LivingSky.Surface.sharedMaterial;
            sky.SetVector("_ProjectionForward",new Vector4(0,.515f,.857f,0));
            sky.SetVector("_ProjectionOffset",new Vector4(.39f,.47f,0,0));
            sky.SetFloat("_ProjectionRotation",20);
            sky.SetColor("_CloudTint",new Color(.30f,.44f,.45f));EditorUtility.SetDirty(sky);
            AtelierSceneBuilder.Save();
        }

        /// <summary>以实际窗口视锥修正房屋视差，并把坡地物件重新落在相同高度场。</summary>
        public static void ReframeExterior()
        {
            var root = Root();
            var landscape = root.Find("窗外山谷");
            var houses = landscape.Find("返工_完整旧屋");
            var points = new[] {new Vector2(9.0f,36),new Vector2(27.0f,53),new Vector2(29.8f,72)};
            for (int i=0;i<3;i++)
            {
                var p=points[i];
                houses.GetChild(i).position=new Vector3(p.x,AtelierRevision.Height(p.x,p.y)-.23f,p.y);
                // 由原模型门板位置识别正面，不猜测 GLTF 转 OBJ 后的朝向。
                FaceDoorTowardsCamera(houses.GetChild(i), root.GetComponentInChildren<Camera>(), i==0 ? -10 : i==1 ? 17 : -19);
            }
            var plants=landscape.Find("返工_自然坡地");
            var left=plants.Find("窗左扫描阔叶树");
            left.position=new Vector3(.8f,AtelierRevision.Height(.8f,16)-.2f,16);
            var right=plants.Find("窗右扫描阔叶树");
            right.position=new Vector3(16.8f,AtelierRevision.Height(16.8f,23)-.2f,23);
            var shrubs=plants.Cast<Transform>().Where(t=>t.name.StartsWith("坡面自然灌丛_")).Skip(16).ToArray();
            for (int i=0;i<shrubs.Length;i++)
            {
                var p=points[i%3]; float angle=i*2.399f;
                float x=p.x+Mathf.Cos(angle)*(2.2f+i%3*.5f),z=p.y+Mathf.Sin(angle)*(2.1f+i%4*.4f);
                shrubs[i].position=new Vector3(x,AtelierRevision.Height(x,z)-.12f,z);
            }
            foreach (Transform t in plants)
            {
                if (!t.name.StartsWith("护坡基石_")) continue;
                var parts=t.name.Split('_');int house=int.Parse(parts[1]), index=int.Parse(parts[2]);
                float x=points[house].x-2.8f+index*.8f,z=points[house].y-2.4f+.18f*Mathf.Sin(index*2);
                t.position=new Vector3(x,AtelierRevision.Height(x,z)-.12f,z);
            }
            for (int i=0;i<3;i++)
            {
                var layer=landscape.Find("远山轮廓/远山层_"+i);
                string path=Art+"Materials/有岩脊明暗的远山_"+i+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("RoomDemo/Atelier/DistantTerrain"));AssetDatabase.CreateAsset(mat,path);}
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Imports/Revision/forest_ground_04/forest_ground_04_diff_2k.jpg"));
                mat.SetColor("_BaseColor",new Color(.012f+i*.009f,.023f+i*.012f,.041f+i*.018f));
                mat.SetColor("_RidgeColor",new Color(.042f+i*.012f,.070f+i*.015f,.115f+i*.019f));
                EditorUtility.SetDirty(mat);layer.GetComponent<MeshRenderer>().sharedMaterial=mat;
            }
            var feature=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Art+"Rendering/画室渲染器.asset").rendererFeatures.OfType<RealtimePainterlyFeature>().Single();
            feature.ConfigureInfluence(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Content/TechnicalArt/LivingPainting/Shaders/InfluenceComposite.shader"));
            EditorUtility.SetDirty(feature);
            var sky=root.GetComponent<AtelierArtState>().LivingSky;
            sky.Surface.sharedMaterial.SetVector("_ProjectionForward",new Vector4(0,.515f,.857f,0));
            sky.Surface.sharedMaterial.SetVector("_ProjectionScale",new Vector4(1.46f,2.60f,0,0));
            sky.Surface.sharedMaterial.SetVector("_ProjectionOffset",new Vector4(.39f,.47f,0,0));
            sky.Surface.sharedMaterial.SetFloat("_ProjectionRotation",20);
            sky.Surface.sharedMaterial.SetColor("_CloudTint",new Color(.30f,.44f,.45f));
            EditorUtility.SetDirty(sky.Surface.sharedMaterial);
            AtelierSceneBuilder.Save();
        }

        /// <summary>从门所在分区推导房屋正面，并留少量转角表达体积。</summary>
        private static void FaceDoorTowardsCamera(Transform house, Camera camera, float offset)
        {
            var renderer=house.GetComponentInChildren<MeshRenderer>();
            var filter=renderer.GetComponent<MeshFilter>();
            int door=Array.FindIndex(renderer.sharedMaterials,m=>m.name=="旧屋扫描_Door");
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;
            var indices=mesh.GetTriangles(door).Distinct().ToArray();
            Vector3 centre=Vector3.zero;
            foreach(int index in indices)centre+=vertices[index];
            centre/=indices.Length;
            var direction=house.InverseTransformDirection(filter.transform.TransformPoint(centre)-renderer.bounds.center);
            var target=camera.transform.position-house.position;
            float yaw=Mathf.Atan2(target.x,target.z)*Mathf.Rad2Deg-Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg+offset;
            house.rotation=Quaternion.Euler(0,yaw,0);
        }

        /// <summary>菜单只在运行独立画室时有效，方便对照开局与后期，不保存领域状态。</summary>
        [MenuItem("RoomDemo/独立画室/预览后期流动星夜")]
        public static void PreviewLateSky() => Root().GetComponent<AtelierArtState>().SetLateSky(true);

        [MenuItem("RoomDemo/独立画室/预览后期流动星夜",true)]
        private static bool CanPreview() => UnityEngine.Application.isPlaying && GameObject.Find("独立夜间画室")!=null;

        /// <summary>回到普通黑夜及完整屋顶，不触发任何玩法重开。</summary>
        [MenuItem("RoomDemo/独立画室/预览开局黑夜")]
        public static void PreviewOrdinarySky() => Root().GetComponent<AtelierArtState>().SetLateSky(false);

        [MenuItem("RoomDemo/独立画室/预览开局黑夜",true)]
        private static bool CanPreviewOrdinary() => CanPreview();

        /// <summary>根据概念图调整斜向构图、局部材质与灯具发光关系，保留可观察的表面细节。</summary>
        public static void FinalComposition()
        {
            var root = Root();
            var camera = root.GetComponentInChildren<Camera>();
            camera.transform.position = new Vector3(-1.15f, 1.96f, -4.3f);
            camera.transform.LookAt(new Vector3(.08f, 2.83f, 1.65f));
            camera.fieldOfView = 58;
            var state = root.GetComponent<AtelierArtState>();
            state.NormalHdri.SetFloat("_Exposure", .11f);
            state.NormalHdri.SetFloat("_Rotation", 215);
            EditorUtility.SetDirty(state.NormalHdri);
            var plants = root.Find("窗外山谷/返工_自然坡地");
            var tree = plants.Find("左侧真实枝叶树");
            if (tree != null) tree.position += Vector3.left * 2.7f;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Art + "Materials" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat.name.Contains("枝叶") || mat.name.Contains("leaves") || mat.name.Contains("twigs") || mat.name.Contains("shrub"))
                {
                    mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Metallic", 0);
                    mat.SetFloat("_Smoothness", .07f);
                    EditorUtility.SetDirty(mat);
                }
            }
            var ceramic = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/灰绿陶釉.mat");
            ceramic.SetColor("_BaseColor", new Color(.72f,.69f,.59f));
            ceramic.SetFloat("_Smoothness", .36f);
            ceramic.SetFloat("_PaintStains", .85f);
            ceramic.SetFloat("_WearStrength", .08f);
            EditorUtility.SetDirty(ceramic);
            var cloth = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/旧亚麻.mat");
            // 帘布保持朴素亚麻，染料仅给工作台抹布的独立材质副本。
            var rag = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/工作抹布干颜料.mat");
            if (rag == null) { rag = new Material(cloth); AssetDatabase.CreateAsset(rag,Art + "Materials/工作抹布干颜料.mat"); }
            rag.SetFloat("_PaintStains", .65f); rag.SetColor("_StainColor",new Color(.065f,.15f,.20f));
            root.Find("桌面与创作痕迹/桌沿旧亚麻抹布").GetComponent<MeshRenderer>().sharedMaterial = rag;
            EditorUtility.SetDirty(rag);
            var painting = root.Find("室内家具/细木作画架/绷布与木框");
            painting.localScale = new Vector3(1.2f,.86f,1);
            var study = painting.Find("原创程序绘制风景习作");
            var paint = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/塞尚溪流临摹参考.mat");
            if (paint == null) { paint = new Material(Shader.Find("RoomDemo/Atelier/AgedPigmentLit")); AssetDatabase.CreateAsset(paint,Art+"Materials/塞尚溪流临摹参考.mat"); }
            paint.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Imports/ClevelandBrook/TheBrook_Cezanne_CC0.jpg"));
            paint.SetColor("_BaseColor",new Color(.86f,.82f,.71f));
            paint.SetFloat("_Smoothness",.13f); paint.SetFloat("_PigmentStrength",.14f); paint.SetFloat("_WearStrength",0);
            study.GetComponent<MeshRenderer>().sharedMaterial = paint;
            study.name = "塞尚溪流临摹参考_CC0";
            EditorUtility.SetDirty(paint);
            var lights = root.Find("照明");
            var bounce = lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>();
            bounce.color = new Color(1,.76f,.50f); bounce.intensity = 4.0f;
            bounce.transform.position = new Vector3(1.30f,1.47f,-.65f);
            var down = lights.Find("桌灯向下的暖工作区").GetComponent<Light>();
            down.color = bounce.color; down.intensity = 1.0f;
            var area = lights.Find("桌灯烘焙柔光").GetComponent<Light>();
            area.color = bounce.color; area.intensity = 16;
            var up = lights.Find("油灯向墙面的柔和散射");
            if (up == null) { up = new GameObject("油灯向墙面的柔和散射",typeof(Light)).transform; up.SetParent(lights); }
            up.position = new Vector3(1.3f,1.48f,-.65f); up.rotation = Quaternion.Euler(-38,0,0);
            var fill = up.GetComponent<Light>();
            fill.type = LightType.Rectangle; fill.lightmapBakeType = LightmapBakeType.Baked;
            fill.areaSize = new Vector2(.26f,.32f); fill.color = bounce.color; fill.intensity = 32;
            var moon = lights.Find("夜间微弱环境方向光").GetComponent<Light>();
            moon.intensity = .16f;
            var cold = lights.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>();
            cold.intensity = 2.3f; cold.color = new Color(.34f,.48f,.78f);
            var rim = AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/屋顶干刷边缘.mat");
            rim.SetColor("_BaseColor",new Color(.56f,.51f,.43f,.62f)); EditorUtility.SetDirty(rim);
            var volume = root.GetComponentInChildren<Volume>().sharedProfile;
            if (volume.TryGet<ColorAdjustments>(out var grade)) grade.postExposure.value = .50f;
            EditorUtility.SetDirty(volume);
            AtelierSceneBuilder.Save();
        }

        /// <summary>正常开局绑定真实月夜，后期星夜仍由单独的显式表现入口控制。</summary>
        public static void SetNight()
        {
            var state = Root().GetComponent<AtelierArtState>();
            var material = state.NormalHdri;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Imports/Revision/qwantani_moon_noon_puresky/qwantani_moon_noon_puresky_2k.hdr"));
            material.SetColor("_Tint", new Color(.68f, .76f, .94f));
            material.SetFloat("_Exposure", .11f);
            material.SetFloat("_Rotation", 215);
            EditorUtility.SetDirty(material);
            state.SetLateSky(false);
            AtelierSceneBuilder.Save();
        }

        /// <summary>用真实包围盒连续堆放书本，调整画笔轴向，消除首版悬空与等间距摆放。</summary>
        public static void ContactAndCloth()
        {
            var root = Root();
            var scan = root.Find("返工_扫描工作簇");
            float top = BoundsOf(root.Find("室内家具/Quixel 厚木工作台")).max.y + .004f;
            float y = top;
            for (int i = 0; i < 4; i++)
            {
                var book = scan.Find("桌面摊放皮卷_" + i);
                book.position = new Vector3(-.62f + i * .015f, book.position.y, -.52f + i * .011f);
                RestOn(book, y);
                y = BoundsOf(book).max.y - .001f;
            }
            for (int i = 0; i < 3; i++)
            {
                var brush = scan.Find("平放的蘸色画笔_" + i);
                var filter = brush.GetComponentInChildren<MeshFilter>();
                Vector3 size = filter.sharedMesh.bounds.size;
                Vector3 axis = size.x > size.y && size.x > size.z ? Vector3.right : size.y > size.z ? Vector3.up : Vector3.forward;
                var direction = new Vector3(.7f + i * .1f, 0, .6f - i * .25f).normalized;
                brush.rotation = Quaternion.FromToRotation(filter.transform.TransformDirection(axis), direction) * brush.rotation;
                brush.position = new Vector3(.18f + i * .05f, brush.position.y, -1.02f + i * .05f);
                RestOn(brush, top + .004f * i);
            }
            // 皮卷按实际厚度聚集成两组，保留少量空档和倾斜书，避免陈列样品般的重复。
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float x = 3.00f;
                for (int i = 0; i < 9; i++)
                {
                    var book = scan.Find("书架皮面旧卷_" + shelf + "_" + i);
                    book.rotation = Quaternion.Euler(0, 174 + (i % 3) * 4, i == 8 ? -9 : 0);
                    float scale = .90f + (i % 4) * .07f;
                    book.localScale = Vector3.one * scale;
                    var bounds = BoundsOf(book);
                    book.position += new Vector3(x - bounds.min.x, 0, 0);
                    RestOn(book, .89f + shelf * .64f);
                    x = BoundsOf(book).max.x + .006f + (i == 5 ? .12f : 0);
                }
            }
            var props = root.Find("桌面与创作痕迹");
            var cloth = props.Find("桌沿旧亚麻抹布");
            cloth.position = new Vector3(1.20f, top, -1.39f);
            cloth.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("自然落桌的褶皱抹布", DrapedCloth());
            var linen = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/旧亚麻.mat");
            linen.SetColor("_BaseColor", new Color(.69f, .63f, .52f));
            linen.SetFloat("_BumpScale", .42f);
            linen.SetFloat("_PigmentStrength", .28f);
            linen.SetFloat("_WearStrength", .12f);
            EditorUtility.SetDirty(linen);
            // 纸页与台面仅留毫米间隙；厚木表面的局部起伏由真实法线与 AO 表达。
            for (int i = 0; i < 3; i++) RestOn(props.Find("微翘草稿纸_" + i), top + i * .002f);
            RestOn(props.Find("带颜料的真实调色板"), top);
            var notes = scan.Find("工作台扫描便笺");
            notes.localScale *= 1.7f;
            RestOn(notes, top + .002f);
            AtelierSceneBuilder.Save();
        }

        /// <summary>保留 OBJ 的原始多材质分区，从玻璃分区里提取少数发光窗片。</summary>
        public static void HouseWindows()
        {
            var houses = Root().Find("窗外山谷/返工_完整旧屋");
            var roof = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/扫描风化石板瓦.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/扫描旧石墙.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/旧屋局部窗灯.mat");
            if (glow == null)
            {
                glow = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "旧屋局部窗灯" };
                AssetDatabase.CreateAsset(glow, Art + "Materials/旧屋局部窗灯.mat");
            }
            glow.SetColor("_BaseColor", new Color(.50f, .27f, .09f));
            glow.SetColor("_EmissionColor", new Color(2.0f, .65f, .08f));
            // 清除默认的 EmissiveIsBlack 标记，否则材质导入校验会撤销发光关键词。
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            glow.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(glow);
            for (int house = 0; house < houses.childCount; house++)
            {
                var renderer = houses.GetChild(house).GetComponentInChildren<MeshRenderer>();
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Imports/JimbogiesHouse/House.obj").GetComponentInChildren<MeshFilter>().sharedMesh;
                var materials = renderer.sharedMaterials.Take(original.subMeshCount).ToList();
                int glass = materials.FindIndex(m => m.name == "旧屋扫描_Glass01");
                for (int i = 0; i < materials.Count; i++)
                {
                    if (materials[i].name == "旧屋扫描_roof") materials[i] = roof;
                    if (materials[i].name == "旧屋扫描_Block02") materials[i] = stone;
                }
                if (glass < 0) throw new InvalidOperationException("完整旧屋缺少玻璃材质分区。");
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = UnityEngine.Object.Instantiate(original);
                var triangles = mesh.GetTriangles(glass);
                var vertices = mesh.vertices;
                var parents = Enumerable.Range(0, vertices.Length).ToArray();
                int Find(int v) { while (parents[v] != v) { parents[v] = parents[parents[v]]; v = parents[v]; } return v; }
                var welded = new Dictionary<Vector3Int, int>();
                foreach (int v in triangles.Distinct())
                {
                    var p = vertices[v] * 10000;
                    var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                    if (welded.TryGetValue(key, out int previous)) parents[Find(v)] = Find(previous);
                    else welded[key] = v;
                }
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    parents[Find(triangles[t + 1])] = Find(triangles[t]);
                    parents[Find(triangles[t + 2])] = Find(triangles[t]);
                }
                var components = triangles.Distinct().Select(Find).Distinct().OrderBy(v => vertices[v].x).ToArray();
                // 排除门上狭小玻璃条，再从面积足够的窗中选最靠近主镜头的两片。
                // 直接按距离选择会点亮低处门条，主镜头中几乎看不到暖窗。
                var visible = components.Where(component =>
                {
                    var points = triangles.Distinct().Where(v => Find(v) == component).Select(v => filter.transform.TransformPoint(vertices[v])).ToArray();
                    var bounds = new Bounds(points[0], Vector3.zero);
                    foreach (var point in points) bounds.Encapsulate(point);
                    float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
                    return bounds.size.y * horizontal > .05f && horizontal / Mathf.Max(.01f,bounds.size.y) > .55f;
                }).OrderByDescending(component =>
                {
                    var group = triangles.Distinct().Where(v => Find(v) == component).ToArray();
                    Vector3 centre = Vector3.zero;
                    foreach (int v in group) centre += vertices[v];
                    centre /= group.Length;
                    var toCamera = Root().GetComponentInChildren<Camera>().transform.position - filter.transform.TransformPoint(centre);
                    float facing = Mathf.Abs(Vector3.Dot(filter.transform.TransformDirection(mesh.normals[group[0]]), toCamera.normalized));
                    // 朝向作为有限加分；不能让背墙仅因法线更接近视线就压过真正的前窗。
                    return facing * 8 - toCamera.magnitude;
                }).Take(2).ToHashSet();
                var dark = new List<int>(); var warm = new List<int>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    var target = visible.Contains(Find(triangles[t])) ? warm : dark;
                    target.AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
                }
                int added = mesh.subMeshCount;
                mesh.subMeshCount = added + 1;
                mesh.SetTriangles(dark, glass); mesh.SetTriangles(warm, added);
                filter.sharedMesh = AtelierMeshLibrary.Save("完整旧屋稀疏窗灯_" + house, mesh);
                materials.Add(glow);
                renderer.sharedMaterials = materials.ToArray();
                Debug.Log($"独立画室旧屋 {house}：{components.Length} 组窗片，暖窗 {warm.Count / 3} 个三角形。");
            }
            AtelierSceneBuilder.Save();
        }

        /// <summary>在本场景复制管线上接入已存在的绘画实现，使用较小核和有限笔触强度。</summary>
        public static void PainterlyAndLight()
        {
            var root = Root();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Art + "Rendering/画室渲染器.asset");
            var feature = renderer.rendererFeatures.OfType<RealtimePainterlyFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RealtimePainterlyFeature>();
                feature.name = "画室各向异性笔触整理";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.Configure(Shader.Find("Hidden/RoomDemo/RealtimePainterly/Kuwahara"), Shader.Find("Hidden/RoomDemo/RealtimePainterly/WorldBrushPatches"));
            EditorUtility.SetDirty(feature); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
            var camera = root.GetComponentInChildren<Camera>();
            var effect = camera.GetComponent<PainterlyCamera>() ?? camera.gameObject.AddComponent<PainterlyCamera>();
            effect.PainterlyIntensity = .38f;
            effect.KernelSize = 3;
            effect.Anisotropy = 1.1f;
            effect.Sharpness = 8;
            effect.Hardness = 1.8f;
            effect.PatchSize = .038f;
            effect.PatchBlend = .20f;
            effect.PigmentVariation = .07f;
            effect.HighlightStrength = .12f;
            var lights = root.Find("照明");
            effect.DirectionalLight = lights.Find("夜间微弱环境方向光").GetComponent<Light>();
            effect.DirectionalLight.intensity = .38f;
            lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>().intensity = 2.5f;
            lights.Find("桌灯烘焙柔光").GetComponent<Light>().intensity = 24;
            lights.Find("桌灯向下的暖工作区").GetComponent<Light>().intensity = 1.1f;
            var cool = lights.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>();
            cool.intensity = 1.8f;
            cool.color = new Color(.38f, .49f, .72f);
            var plaster = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/暖灰灰泥.mat");
            plaster.SetColor("_BaseColor", new Color(.93f, .85f, .73f));
            plaster.SetFloat("_BumpScale", .50f);
            plaster.SetFloat("_PigmentStrength", .38f);
            plaster.SetFloat("_WearStrength", .15f);
            EditorUtility.SetDirty(plaster);
            AtelierSceneBuilder.Save();
        }

        /// <summary>布面在真实桌沿处下落，横向细褶与不齐的边缘分别控制。</summary>
        private static Mesh DrapedCloth()
        {
            const int nx = 64, ny = 96;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, v = (float)y / ny;
                float drop = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.57f, 1, v));
                float wrinkle = .005f * Mathf.Sin(u * 14 + v * 7) + .003f * Mathf.Sin(u * 29 - v * 5);
                float xx = (u - .5f) * .68f + .025f * Mathf.Sin(v * 7 + u * 5);
                float yy = .007f + wrinkle * (1 - drop) - drop * (.38f + .065f * Mathf.Sin(u * 7 + .8f));
                float zz = Mathf.Lerp(.60f, -.065f, Mathf.Clamp01(v / .65f)) + drop * (.024f * Mathf.Sin(u * 17 + v * 5) + wrinkle);
                vertices.Add(new Vector3(xx, yy, zz)); uv.Add(new Vector2(u * 1.7f, v * 2.4f));
            }
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int a = y * (nx + 1) + x;
                triangles.AddRange(new[] { a, a + 1, a + nx + 2, a, a + nx + 2, a + nx + 1 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); return mesh;
        }

        private static Transform Root() => GameObject.Find("独立夜间画室").transform;
        private static Bounds BoundsOf(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }
        private static void RestOn(Transform t, float y) => t.position += Vector3.up * (y - BoundsOf(t).min.y);
    }
}
