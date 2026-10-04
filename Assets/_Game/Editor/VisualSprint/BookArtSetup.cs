// =============================================================================
// 文件：BookArtSetup.cs
// 职责：以 Editor API 制作近景装帧、纸页曲面、植物速写与“向光”墨迹灵感效果。
// 边界：只替换书本检视 Prefab 与近景灯光，不改动作 ID、有限页状态或任何领域内容。
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor.VisualSprint
{
    /// <summary>第五阶段书本 Hero Shot 的具体美术安装器；重复运行保持资产 GUID 与绑定。</summary>
    public static class BookArtSetup
    {
        private const string Art = "Assets/_Game/Content/Art/Inspection/Book/";
        private const string PrefabPath = "Assets/_Game/Content/Prefabs/Inspectables/PF_InspectBook.prefab";
        private static Material leather, paperEdge, gold, pencil, bloom, pagePaper, fiber;
        private static Mesh coverMesh, pageMesh;

        /// <summary>增量更新实际书本检视资产与展示灯光，不重建美术主场景。</summary>
        /// <remarks>调用前场景必须已保存；保存沿用 PresentationArtPass 的人工修改保护。</remarks>
        public static void Apply()
        {
            var room = PresentationArtPass.Room();
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            BuildMaterials();
            coverMesh = SaveMesh("BookCover", BeveledBox(1.64f, 1.89f, .09f, .05f));
            pageMesh = SaveMesh("FlexiblePage", Sheet(1.47f, 1.73f, 48, 16));
            var root = new GameObject("PF_InspectBook");
            var closed = ClosedCover(root.transform);
            var pages = new GameObject[3];
            for (int i = 0; i < pages.Length; i++) pages[i] = OpenSpread(root.transform, i + 1);
            var turn = Node("PagePivot", root.transform);
            turn.localPosition = new Vector3(.016f, 0, -.145f);
            var turning = MeshObject("TurningPage", turn, pageMesh, pagePaper, Vector3.zero);
            turning.AddComponent<PaperBendVisual>().Configure(turning.GetComponent<Renderer>());
            root.AddComponent<InspectionView>().Configure(new[] { "closed", "page1", "page2", "page3" },
                new[] { closed, pages[0], pages[1], pages[2] }, turn);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            var book = room.GetComponentsInChildren<SceneEntity>(true).Single(x => x.Id.Value == "book");
            book.GetComponent<InspectableObject>().Configure(book, prefab);
            DressInspectionLight(room);
            AssetDatabase.SaveAssets();
            PresentationArtPass.Save(room);
            Debug.Log("[美术] 书本装帧、柔性纸页、植物速写与墨迹灵感效果已更新。");
        }

        private static void BuildMaterials()
        {
            // 所有纹理为本项目原创近景表面；它们不替代第三方家具和自然模型的素材记录。
            var paperTexture = Texture("PaperFibers", 768, (u, v) =>
            {
                float noise = Mathf.PerlinNoise(u * 190, v * 190) * .055f;
                float fibers = Mathf.PerlinNoise(u * 85, v * 480) * .022f;
                float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1-u), Mathf.Min(v, 1-v)) * 18);
                float value = .93f + noise + fibers - (1-edge) * .06f;
                return new Color(value, value * .98f, value * .91f);
            });
            var coverTexture = Texture("WornCloth", 512, (u, v) =>
            {
                float weave = (Mathf.Sin(u * 1250) * Mathf.Sin(v * 1250)) * .022f;
                float wear = Mathf.PerlinNoise(u * 40, v * 40) * .15f;
                float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1-u), Mathf.Min(v, 1-v)) * 15);
                float value = .68f + wear + weave + (1-edge)*.09f;
                return new Color(value, value, value);
            });
            leather = Lit("M_BookCloth", new Color(.19f,.25f,.23f), .12f);
            leather.SetTexture("_BaseMap", coverTexture);
            paperEdge = Lit("M_PageEdges", new Color(.64f,.57f,.42f), .05f);
            gold = Lit("M_BookFoil", new Color(.58f,.40f,.16f), .34f);
            gold.SetFloat("_Metallic", .45f);
            pencil = Material("M_Graphite", "Universal Render Pipeline/Unlit", new Color(.25f,.25f,.19f));
            pagePaper = Material("M_FlexiblePage", "Game/Art/BookPaper", new Color(.96f,.92f,.83f));
            pagePaper.SetTexture("_BaseMap", paperTexture);
            pagePaper.SetFloat("_PageWidth",1.47f);
            pagePaper.SetFloat("_BendStrength",1);
            pagePaper.SetFloat("_StaticCurl", .022f);
            bloom = Material("M_InspirationInk", "Game/Art/InspirationInk", new Color(.22f,.13f,.05f));
            fiber = Material("M_PaperMotes", "Universal Render Pipeline/Particles/Unlit", Color.white);
            fiber.SetFloat("_Surface",1);
            fiber.SetFloat("_Blend",0);
            fiber.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            fiber.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            fiber.SetFloat("_ZWrite",0);
            fiber.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fiber.renderQueue = 3000;
            fiber.SetTexture("_BaseMap", Texture("FiberGlow",128,(u,v) =>
            {
                float r = new Vector2(u-.5f,v-.5f).magnitude*2;
                return new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));
            }));
        }

        private static GameObject ClosedCover(Transform parent)
        {
            var root = Node("Cover",parent);
            var cover = MeshObject("Cover",root,coverMesh,leather,new Vector3(0,0,-.10f));
            var collider = cover.AddComponent<BoxCollider>(); collider.size = new Vector3(1.64f,1.89f,.09f);
            cover.AddComponent<InspectionHotspot>().Configure("cover",cover.GetComponent<Renderer>(),Vector3.back);
            MeshObject("BackCover",root,coverMesh,leather,new Vector3(0,0,.12f));
            Cube("PageBlock",root,new Vector3(.01f,0,.015f),new Vector3(1.52f,1.78f,.16f),paperEdge);
            Cube("ClothSpine",root,new Vector3(-.78f,0,.01f),new Vector3(.15f,1.89f,.23f),leather);
            // 真正的装帧由封面、书脊、页层、压印和磨损构成，热点碰撞盒不承担美术。
            Border(root, new Vector2(1.42f,1.63f), -.157f, gold, .006f);
            Border(root, new Vector2(1.35f,1.56f), -.158f, gold, .0025f);
            Text("CoverTitle",root,"关于光的习作",new Vector3(0,.32f,-.159f),.135f,TextAnchor.MiddleCenter,new Color(.75f,.59f,.31f));
            Text("CoverSubtitle",root,"植物与房间的札记",new Vector3(0,.08f,-.159f),.049f,TextAnchor.MiddleCenter,new Color(.64f,.51f,.32f));
            DrawBotanical(root, new Vector3(0,-.36f,-.161f), .43f, gold, .004f);
            Text("CoverDate",root,"未完成 · 手稿",new Vector3(0,-.73f,-.159f),.047f,TextAnchor.MiddleCenter,new Color(.60f,.47f,.27f));
            for(int i=0;i<14;i++)
                Cube("PaperEdge"+i,root,new Vector3(.04f,-.82f,.095f-i*.011f),new Vector3(1.43f,.008f,.002f),i%3==0 ? gold : paperEdge);
            return root.gameObject;
        }

        private static GameObject OpenSpread(Transform parent,int index)
        {
            var root = Node("Page"+index,parent);
            var left = MeshObject("LeftCover",root,coverMesh,leather,new Vector3(-.825f,0,.10f));
            var right = MeshObject("RightCover",root,coverMesh,leather,new Vector3(.825f,0,.10f));
            left.transform.localRotation=Quaternion.Euler(0,-2,0);
            right.transform.localRotation=Quaternion.Euler(0,2,0);
            for(int side=-1;side<=1;side+=2)
            {
                Cube("PageBlock"+side,root,new Vector3(side*.755f,0,.01f),new Vector3(1.46f,1.76f,.13f),paperEdge);
                var sheet=MeshObject(side<0 ? "PaperLeft" : "PaperRight",root,pageMesh,pagePaper,new Vector3(side*.02f,0,-.082f));
                sheet.transform.localScale=new Vector3(side,1,1);
                for(int layer=0;layer<11;layer++)
                    Cube("Edge"+side+"_"+layer,root,new Vector3(side*.755f,-.87f,.06f-layer*.012f),new Vector3(1.46f,.004f,.003f),layer%3==0 ? gold : paperEdge);
            }
            Cube("SpineValley",root,new Vector3(0,0,-.064f),new Vector3(.021f,1.75f,.03f),paperEdge);
            // 书签与装订缝线是近景尺度参考，不参与互动状态。
            Cube("Ribbon",root,new Vector3(.06f,-.76f,-.107f),new Vector3(.034f,.46f,.009f),leather);
            Text("PageNumber",root,index.ToString(),new Vector3(1.26f,-.76f,-.115f),.044f,TextAnchor.MiddleCenter,new Color(.43f,.34f,.21f));
            Text("NotebookChapter",root,"光与枝叶",new Vector3(-1.24f,.72f,-.115f),.04f,TextAnchor.UpperLeft,new Color(.47f,.40f,.28f));
            Line(root,"HeaderRule",new[]{new Vector3(-1.24f,.64f,-.115f),new Vector3(-.27f,.64f,-.115f)},pencil,.002f);
            if(index<3) Corner(root,"next"+index,1.34f);
            if(index>1) Corner(root,"previous"+index,-1.34f);
            var close=Hotspot(root,"Close","close",new Vector3(0,.79f,-.12f),new Vector3(.26f,.10f,.03f),pagePaper);
            Text("CloseHint",close,"合上",Vector3.back*.025f,.035f,TextAnchor.MiddleCenter,new Color(.43f,.34f,.21f));
            if(index==1)
            {
                Text("FirstText",root,"一盆静默的绿。\n\n土壤并没有停下。",new Vector3(-1.22f,.39f,-.12f),.085f,TextAnchor.UpperLeft,new Color(.24f,.20f,.14f));
                Text("Margin",root,"它没有离开，\n却每天都更靠近什么。",new Vector3(.27f,-.43f,-.12f),.058f,TextAnchor.UpperLeft,new Color(.41f,.35f,.24f));
                DrawBotanical(root,new Vector3(.72f,.17f,-.126f),.61f,pencil,.005f);
            }
            if(index==2) QuoteSpread(root);
            if(index==3)
            {
                Text("HorizonText",root,"页边没有尽头。",new Vector3(-1.22f,.39f,-.12f),.084f,TextAnchor.UpperLeft,new Color(.24f,.20f,.14f));
                Text("HorizonNote",root,"也许，远方并不在窗外。",new Vector3(-1.22f,.09f,-.12f),.047f,TextAnchor.UpperLeft,new Color(.43f,.36f,.25f));
                var horizon=Hotspot(root,"Horizon","horizon",new Vector3(.75f,.03f,-.123f),new Vector3(1.09f,.48f,.02f),pagePaper);
                var points=new Vector3[32];
                for(int i=0;i<points.Length;i++) points[i]=new Vector3(.20f+i/31f*1.14f,.06f+.028f*Mathf.Sin(i*.7f),-.129f);
                Line(root,"HorizonLine",points,pencil,.006f);
                var horizonAccent=Cube("HorizonHover",horizon,Vector3.zero,new Vector3(.97f,.016f,.004f),paperEdge);
                horizon.GetComponent<InspectionHotspot>().Configure("horizon",horizonAccent.GetComponent<Renderer>(),Vector3.back);
                Text("HorizonCaption",root,"这一页，明天也许会不同。",new Vector3(.22f,-.43f,-.12f),.046f,TextAnchor.UpperLeft,new Color(.43f,.36f,.25f));
            }
            return root.gameObject;
        }

        private static void QuoteSpread(Transform root)
        {
            Text("QuoteStart",root,"枝叶仍",new Vector3(-1.22f,.36f,-.122f),.10f,TextAnchor.UpperLeft,new Color(.23f,.20f,.15f));
            var word=Text("QuoteWord",root,"向光",new Vector3(-1.22f,.07f,-.135f),.15f,TextAnchor.UpperLeft,new Color(.23f,.20f,.15f));
            Text("QuoteEnd",root,"处伸去。",new Vector3(-1.22f,-.24f,-.122f),.10f,TextAnchor.UpperLeft,new Color(.23f,.20f,.15f));
            var quote=Hotspot(root,"Quote","quote",new Vector3(-1.105f,.004f,-.127f),new Vector3(.30f,.22f,.045f),pagePaper);
            var bloomObject=Quad("InkBloom",quote,new Vector3(0,-.03f,-.023f),new Vector2(.43f,.21f),bloom);
            quote.GetComponent<InspectionHotspot>().Configure("quote",bloomObject.GetComponent<Renderer>(),Vector3.back);
            var line=Cube("ObservedUnderline",quote,new Vector3(0,-.093f,-.028f),new Vector3(.25f,.003f,.002f),gold);
            line.SetActive(false);
            var motes=Fibers(quote);
            quote.gameObject.AddComponent<InspirationVisual>().Configure(word,bloomObject.GetComponent<Renderer>(),line.transform,motes);
            DrawBotanical(root,new Vector3(.76f,.13f,-.128f),.68f,pencil,.004f);
            Line(root,"LightRays",new[]{new Vector3(.96f,.58f,-.128f),new Vector3(.47f,.08f,-.128f)},gold,.003f);
            Line(root,"LightRays2",new[]{new Vector3(1.12f,.45f,-.128f),new Vector3(.67f,-.01f,-.128f)},gold,.002f);
            Text("SketchNote",root,"新叶还很小，\n却有自己的方向。",new Vector3(.24f,-.49f,-.122f),.058f,TextAnchor.UpperLeft,new Color(.40f,.34f,.24f));
        }

        private static ParticleSystem Fibers(Transform parent)
        {
            var node=Node("PaperFiberMotes",parent); node.localPosition=new Vector3(0,0,-.055f);
            var particle=node.gameObject.AddComponent<ParticleSystem>();
            var main=particle.main; main.playOnAwake=false; main.loop=true; main.startLifetime=new ParticleSystem.MinMaxCurve(1.5f,2.4f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.025f,.075f); main.startSize=new ParticleSystem.MinMaxCurve(.004f,.014f); main.maxParticles=20;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.77f,.38f,.32f),new Color(1,.95f,.76f,.70f));
            main.simulationSpace=ParticleSystemSimulationSpace.Local; main.gravityModifier=-.003f;
            var emission=particle.emission; emission.rateOverTime=3;
            var shape=particle.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(.30f,.16f,.009f);
            var velocity=particle.velocityOverLifetime;
            velocity.enabled=true;
            // 三轴统一为双常量模式，避免 Unity 在播放墨迹光点时每帧报告曲线错误。
            velocity.x=new ParticleSystem.MinMaxCurve(-.018f,.018f);
            velocity.y=new ParticleSystem.MinMaxCurve(.025f,.025f);
            velocity.z=new ParticleSystem.MinMaxCurve(-.025f,-.025f);
            var fade=particle.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.20f),new GradientAlphaKey(0,1)}); fade.color=gradient;
            var renderer=particle.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=fiber; renderer.sortingOrder=10;
            particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            return particle;
        }

        private static void DressInspectionLight(Transform room)
        {
            var space=room.Find("_Systems/InspectionSpace");
            var key=space.Find("InspectionLight").GetComponent<Light>();
            key.transform.localPosition=new Vector3(-2.1f,2.0f,-2.4f); key.color=new Color(1,.86f,.65f); key.intensity=6.4f; key.range=10;
            var fill=NodeOrExisting("BookSoftFill",space); var light=fill.GetComponent<Light>(); if(light==null) light=fill.gameObject.AddComponent<Light>();
            fill.localPosition=new Vector3(2.6f,-.2f,-1.5f); light.type=LightType.Point; light.color=new Color(.62f,.70f,.73f); light.intensity=2.3f; light.range=8;
            light.cullingMask=1<<InspectionController.InspectionLayer; light.shadows=LightShadows.None;
            var ui=room.Find("UI/InspectionCanvas/InspectionOverlay");
            var title=ui.Find("Title").GetComponent<Text>(); title.fontSize=16; title.color=new Color(.76f,.70f,.59f);
            title.rectTransform.anchorMin=new Vector2(.055f,.935f); title.rectTransform.anchorMax=new Vector2(.40f,.98f);
            title.rectTransform.offsetMin=title.rectTransform.offsetMax=Vector2.zero;
            var hint=ui.Find("Hint").GetComponent<Text>(); hint.fontSize=15; hint.color=new Color(.61f,.58f,.51f);
            var feedback=ui.Find("Feedback").GetComponent<Text>(); feedback.fontSize=17; feedback.color=new Color(.85f,.75f,.53f);
            var back=ui.Find("ExitInspection"); var image=back.GetComponent<Image>(); image.color=Color.clear;
            var backRect=(RectTransform)back; backRect.anchorMin=new Vector2(.84f,.94f); backRect.anchorMax=new Vector2(.95f,.98f);
            backRect.offsetMin=backRect.offsetMax=Vector2.zero;
            var caption=back.GetComponentInChildren<Text>(); caption.fontSize=14; caption.text="Esc 返回"; caption.color=new Color(.74f,.68f,.57f);
        }

        private static void Corner(Transform root,string action,float x)
        {
            var corner=Hotspot(root,action,action,new Vector3(x,-.71f,-.123f),new Vector3(.25f,.23f,.04f),pagePaper);
            // 页角不是 UI 箭头；两条短笔迹提示纸张可以翻动。
            Line(corner,"PageCornerStroke",new[]{new Vector3(-.055f,.04f,-.02f),new Vector3(.045f,-.04f,-.02f),new Vector3(.060f,.04f,-.02f)},gold,.004f);
        }

        private static Transform Hotspot(Transform parent,string name,string action,Vector3 position,Vector3 size,Material material)
        {
            var node=Node(name,parent); node.localPosition=position;
            var collider=node.gameObject.AddComponent<BoxCollider>(); collider.size=size;
            var accent=Cube("HoverAccent",node,new Vector3(0,-size.y*.38f,-.008f),new Vector3(size.x*.72f,.002f,.002f),material);
            node.gameObject.AddComponent<InspectionHotspot>().Configure(action,accent.GetComponent<Renderer>(),Vector3.back);
            return node;
        }

        private static TextMesh Text(string name,Transform parent,string value,Vector3 position,float size,TextAnchor anchor,Color color)
        {
            var text=ExplorationSceneSetup.Text3D(name,parent,value,position,size,anchor); text.color=color;
            text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            text.GetComponent<Renderer>().receiveShadows=false;
            return text;
        }

        private static void DrawBotanical(Transform parent,Vector3 center,float scale,Material material,float width)
        {
            var stem=new Vector3[20];
            for(int i=0;i<stem.Length;i++) { float t=i/19f; stem[i]=center+new Vector3(Mathf.Sin(t*2.6f)*.05f,Mathf.Lerp(-.5f,.45f,t),0)*scale; }
            Line(parent,"PencilStem",stem,material,width);
            for(int leaf=0;leaf<6;leaf++)
            {
                float y=-.32f+leaf*.12f; int side=leaf%2==0 ? 1 : -1;
                var outline=new Vector3[33];
                for(int i=0;i<outline.Length;i++)
                {
                    float t=i/32f*Mathf.PI*2;
                    outline[i]=center+new Vector3(side*(.025f+.22f*(1-Mathf.Cos(t))*.5f),y+.13f*Mathf.Sin(t)+.10f*(1-Mathf.Cos(t))*.5f,0)*scale;
                }
                Line(parent,"PencilLeaf"+leaf,outline,material,width*.65f);
                Line(parent,"LeafVein"+leaf,new[]{center+new Vector3(.02f,y,0)*scale,center+new Vector3(side*.23f,y+.10f,0)*scale},material,width*.5f);
            }
        }

        private static void Border(Transform parent,Vector2 size,float z,Material material,float width)
        {
            var points=new[]{new Vector3(-size.x/2,-size.y/2,z),new Vector3(-size.x/2,size.y/2,z),new Vector3(size.x/2,size.y/2,z),new Vector3(size.x/2,-size.y/2,z),new Vector3(-size.x/2,-size.y/2,z)};
            Line(parent,"FoilBorder",points,material,width);
        }

        private static LineRenderer Line(Transform parent,string name,Vector3[] points,Material material,float width)
        {
            var line=Node(name,parent).gameObject.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.positionCount=points.Length; line.SetPositions(points);
            line.widthMultiplier=width; line.numCapVertices=4; line.sharedMaterial=material; line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
            return line;
        }

        private static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var node=GameObject.CreatePrimitive(PrimitiveType.Cube); node.name=name; node.transform.SetParent(parent,false); node.transform.localPosition=position; node.transform.localScale=scale;
            Object.DestroyImmediate(node.GetComponent<Collider>()); node.GetComponent<Renderer>().sharedMaterial=material;
            return node;
        }

        private static GameObject Quad(string name,Transform parent,Vector3 position,Vector2 scale,Material material)
        {
            var node=GameObject.CreatePrimitive(PrimitiveType.Quad); node.name=name; node.transform.SetParent(parent,false); node.transform.localPosition=position; node.transform.localScale=new Vector3(scale.x,scale.y,1);
            Object.DestroyImmediate(node.GetComponent<Collider>()); var renderer=node.GetComponent<Renderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off;
            return node;
        }

        private static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material,Vector3 position)
        {
            var node=Node(name,parent); node.localPosition=position; node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            return node.gameObject;
        }

        private static Transform Node(string name,Transform parent)
        {
            var node=new GameObject(name).transform; node.SetParent(parent,false); return node;
        }
        private static Transform NodeOrExisting(string name,Transform parent) => parent.Find(name) ?? Node(name,parent);

        private static Material Lit(string name,Color color,float smoothness)
        {
            var material=Material(name,"Universal Render Pipeline/Lit",color); material.SetFloat("_Smoothness",smoothness); return material;
        }
        private static Material Material(string name,string shader,Color color)
        {
            var path=Art+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var found=Shader.Find(shader); if(found==null) throw new InvalidOperationException("书本 Shader 缺失："+shader);
            if(material==null) { material=new Material(found); AssetDatabase.CreateAsset(material,path); } else material.shader=found;
            material.SetColor("_BaseColor",color); EditorUtility.SetDirty(material); return material;
        }

        private static Texture2D Texture(string name,int resolution,Func<float,float,Color> pixel)
        {
            string path=Art+name+".png";
            var previous=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(previous!=null) return previous;
            var image=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false); var colors=new Color[resolution*resolution];
            for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++) colors[y*resolution+x]=pixel(x/(float)(resolution-1),y/(float)(resolution-1));
            image.SetPixels(colors); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode=TextureWrapMode.Repeat; importer.maxTextureSize=1024; importer.textureCompression=TextureImporterCompression.CompressedHQ;
            if(name=="FiberGlow") importer.alphaIsTransparency=true;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Mesh SaveMesh(string name,Mesh data)
        {
            data.name=name; string path=Art+name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            // 更新同一 Mesh 资产，已绑定的 Prefab 引用不会因重复安装丢失。
            if(existing==null) { AssetDatabase.CreateAsset(data,path); return data; }
            existing.Clear();
            existing.vertices=data.vertices;
            existing.uv=data.uv;
            existing.triangles=data.triangles;
            existing.normals=data.normals;
            existing.bounds=data.bounds;
            // 显式上传同时刷新已有 Renderer 的图形缓冲，保留翻页所需的扩大包围盒。
            existing.UploadMeshData(false);
            EditorUtility.SetDirty(existing); Object.DestroyImmediate(data); return existing;
        }

        private static Mesh Sheet(float width,float height,int columns,int rows)
        {
            var vertices=new Vector3[(columns+1)*(rows+1)]; var uv=new Vector2[vertices.Length]; var triangles=new int[columns*rows*6]; int cursor=0;
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            { int i=y*(columns+1)+x; uv[i]=new Vector2(x/(float)columns,y/(float)rows); vertices[i]=new Vector3(uv[i].x*width,(uv[i].y-.5f)*height,0); }
            for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
            { int i=y*(columns+1)+x; triangles[cursor++]=i; triangles[cursor++]=i+columns+1; triangles[cursor++]=i+1; triangles[cursor++]=i+1; triangles[cursor++]=i+columns+1; triangles[cursor++]=i+columns+2; }
            var mesh=new Mesh {vertices=vertices,uv=uv,triangles=triangles}; mesh.RecalculateNormals();
            // Shader 的最大弯曲越出平面，离线扩大包围盒保证近景卷曲期间不会被错误剔除。
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(width*2.4f,height*1.1f,width*2.4f)); return mesh;
        }

        private static Mesh BeveledBox(float width,float height,float depth,float radius)
        {
            const int steps=6; int ringSize=4*(steps+1); var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            for(int ring=0;ring<4;ring++)
            {
                float inset=ring==0 || ring==3 ? .012f : 0; float z=ring==0 ? -depth*.5f : ring==1 ? -depth*.5f+.012f : ring==2 ? depth*.5f-.012f : depth*.5f;
                for(int corner=0;corner<4;corner++) for(int step=0;step<=steps;step++)
                {
                    float angle=(corner*90+step/(float)steps*90)*Mathf.Deg2Rad;
                    float x=(corner==0 || corner==3 ? 1 : -1)*(width*.5f-radius-inset)+Mathf.Cos(angle)*(radius-inset);
                    float y=(corner<2 ? 1 : -1)*(height*.5f-radius-inset)+Mathf.Sin(angle)*(radius-inset);
                    vertices.Add(new Vector3(x,y,z)); uv.Add(new Vector2(x/width+.5f,y/height+.5f));
                }
            }
            for(int ring=0;ring<3;ring++) for(int i=0;i<ringSize;i++)
            { int a=ring*ringSize+i,b=ring*ringSize+(i+1)%ringSize,c=a+ringSize,d=b+ringSize; triangles.AddRange(new[]{a,c,b,b,c,d}); }
            int front=vertices.Count; vertices.Add(new Vector3(0,0,-depth*.5f)); uv.Add(Vector2.one*.5f);
            int back=vertices.Count; vertices.Add(new Vector3(0,0,depth*.5f)); uv.Add(Vector2.one*.5f);
            for(int i=0;i<ringSize;i++) { int next=(i+1)%ringSize; triangles.AddRange(new[]{front,next,i,back,i+3*ringSize,next+3*ringSize}); }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
