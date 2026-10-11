// =============================================================================
// 文件：AtelierOpeningRevision.cs
// 职责：在已保存的玩法副本上增量安装空画布、物件检视和可读便条。
// 边界：不重建获认可美术，不修改源 Prefab；分色时临时材质在 finally 中完整归还。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>本次开场修订唯一安装入口；重复执行仅更新本修订拥有的节点。</summary>
    public static class AtelierOpeningRevision
    {
        /// <summary>增量修改当前已保存场景并留存新的几何分色资产。</summary>
        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != AtelierGameplayBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("请先确认已保存的新玩法场景；本工具拒绝覆盖未保存内容。");
            var game = Object.FindAnyObjectByType<AtelierGameplay>();
            var root = game.transform;
            var art = GameObject.Find("独立夜间画室").transform;
            game.Wash.transform.position = new Vector3(-.17f,.948f,-1.18f);
            game.Wash.SupportedBrush.localRotation = Quaternion.FromToRotation(Vector3.right, new Vector3(-.85f,.48f,.21f).normalized);
            game.Wash.BrushSurface.transform.localRotation=Quaternion.Euler(0,180,0);
            float tipDistance=new Vector3(.0764f,.046f,.01872f).magnitude;
            game.Wash.BrushSurface.transform.localPosition=Vector3.right*(tipDistance+game.Wash.BrushSurface.sharedMesh.bounds.min.x*125);
            game.Wash.BristleStart=-.00095f;game.Wash.BristleEnd=-.00136f;
            var desk = Child(root,"检视_桌面机位"); desk.position = new Vector3(-.45f,1.62f,-2.12f);
            desk.LookAt(new Vector3(-.13f,1.11f,-1.14f)); game.DeskFocusPose = desk;
            var brushPick = Child(game.Wash.BrushSurface.transform,"Logic_画笔"); brushPick.gameObject.layer = 2;
            var brushCollider = brushPick.GetComponent<BoxCollider>();
            if(brushCollider==null) brushCollider=brushPick.gameObject.AddComponent<BoxCollider>();
            brushCollider.center = game.Wash.BrushSurface.sharedMesh.bounds.center;
            brushCollider.size = game.Wash.BrushSurface.sharedMesh.bounds.size + new Vector3(.0001f,.00018f,.00018f);
            Target(brushPick,AtelierTargetKind.Brush,AtelierRules.Brush,"画笔");
            AddBox(root,"Logic_桌上的灯",new Vector3(1.30f,1.40f,-.65f),new Vector3(.36f,.90f,.36f),AtelierTargetKind.StillLamp,AtelierRules.StillLamp,"桌上的灯");
            AddBox(root,"Logic_取景窗",new Vector3(1.35f,2.35f,2.13f),new Vector3(2.6f,2.5f,.12f),AtelierTargetKind.Window,StudioRules.Window,"窗外的夜色");
            InstallNote(root,art,game);
            var table=art.Find("室内家具/Quixel 厚木工作台").GetComponentInChildren<MeshFilter>();
            var tableLogic=Child(root,"Logic_扫描工作台遮挡");tableLogic.gameObject.layer=2;
            tableLogic.SetPositionAndRotation(table.transform.position,table.transform.rotation);tableLogic.localScale=table.transform.lossyScale;
            var meshCollider=tableLogic.GetComponent<MeshCollider>();if(meshCollider==null)meshCollider=tableLogic.gameObject.AddComponent<MeshCollider>();meshCollider.sharedMesh=table.sharedMesh;
            var text = game.ObservationText;
            text.rectTransform.anchoredPosition = Vector2.zero; text.rectTransform.sizeDelta = new Vector2(420,110); text.fontSize = 19;
            ((RectTransform)game.ContextPanel.transform).sizeDelta = new Vector2(454,125);
            ((RectTransform)game.ContextPanel.transform).anchoredPosition = new Vector2(-385,266);
            game.HoverText.rectTransform.sizeDelta = new Vector2(290,42);
            game.ContextAction.gameObject.SetActive(false);
            game.Painting.GetComponentInChildren<AtelierInteractionTarget>().DisplayName="画布";
            foreach (var t in root.GetComponentsInChildren<Text>(true).Where(t=>t.name=="操作说明"))
                t.text="按住右键或空格转头 · 滚轮看近\n点物件取景，点画布落笔 · N 笔记\n检视中右键或 Esc 返回";
            // 画的颜色只来自真实场景；遮罩是几何分类，不含概念图或外部天空图片。
            AtelierGameplayBuilder.BakePainting(art,game.Painting,game.Wash);
            BakeMasks(art,game);
            game.Painting.Surface.sharedMaterial.SetVector("_Sketch",Vector4.zero);
            game.Painting.Surface.sharedMaterial.SetFloat("_WaterPaint",0);
            EditorUtility.SetDirty(game.Painting.Surface.sharedMaterial);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "已安装：空白画布、四件静物落笔、杯笔实体检视、便条；美术基线和正式入口保持原状。";
        }
        private static void InstallNote(Transform root,Transform art,AtelierGameplay game)
        {
            var note = root.Find("Visual_交稿便条");
            if (note == null)
            {
                var source=art.GetComponentsInChildren<Renderer>().Single(r=>r.name=="微翘草稿纸_2");
                note=Object.Instantiate(source.gameObject,root).transform; note.name="Visual_交稿便条";
                note.position+=new Vector3(-.17f,.008f,-.88f);
                var material=new Material(source.sharedMaterial); material.name="便条_未涂色的旧纸";
                if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",null);
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",new Color(.77f,.72f,.59f));
                string path=AtelierGameplayBuilder.Content+"Materials/便条_旧纸.mat";
                AssetDatabase.CreateAsset(material,path); note.GetComponent<Renderer>().sharedMaterial=material;
            }
            note.position += new Vector3(0,0,-1.18f-note.GetComponent<Renderer>().bounds.center.z);
            AddBox(root,"Logic_交稿便条",note.GetComponent<Renderer>().bounds.center,new Vector3(.53f,.04f,.4f),AtelierTargetKind.Manuscript,AtelierRules.Manuscript,"桌上的便条");
            var canvas=game.Notebook.GetComponent<Canvas>();
            var panel=Child(canvas.transform,"检视_交稿便条").gameObject;
            var rect=panel.GetComponent<RectTransform>();
            if(rect==null) { Object.DestroyImmediate(panel); panel=new GameObject("检视_交稿便条",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(canvas.transform,false); rect=panel.GetComponent<RectTransform>(); }
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(650,474);rect.anchoredPosition=new Vector2(0,18);
            var image=panel.GetComponent<Image>();if(image==null)image=panel.AddComponent<Image>(); image.color=new Color(.86f,.82f,.70f,.995f);image.raycastTarget=true;
            var body=Child(panel.transform,"便条正文").gameObject;
            var tr=body.GetComponent<RectTransform>();
            if(tr==null){Object.DestroyImmediate(body);body=new GameObject("便条正文",typeof(RectTransform),typeof(Text));body.transform.SetParent(panel.transform,false);tr=body.GetComponent<RectTransform>();}
            tr.anchorMin=tr.anchorMax=new Vector2(.5f,.5f);tr.sizeDelta=new Vector2(530,386);
            var text=body.GetComponent<Text>();text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");text.fontSize=25;text.lineSpacing=1.35f;text.color=new Color(.22f,.20f,.16f);text.alignment=TextAnchor.UpperLeft;text.raycastTarget=false;
            text.text="明早交稿。\n\n试了三个题目，一个也画不下去。\n\n杯子，笔，灯。\n窗外还有几盏亮着的灯。\n\n不想了，就画眼前这些。";
            panel.SetActive(false);game.ManuscriptPanel=panel;
            if(note.Find("便条字迹")==null)
            {
                var world=new GameObject("便条字迹",typeof(RectTransform),typeof(Canvas));world.transform.SetParent(note,true);
                world.transform.position=note.GetComponent<Renderer>().bounds.center+Vector3.up*.012f;
                world.transform.rotation=Quaternion.Euler(90,0,0);world.transform.localScale=Vector3.one*.001f;
                var wc=world.GetComponent<Canvas>();wc.renderMode=RenderMode.WorldSpace;
                var wr=world.GetComponent<RectTransform>();wr.sizeDelta=new Vector2(340,235);
                var label=new GameObject("墨迹",typeof(RectTransform),typeof(Text));label.transform.SetParent(world.transform,false);
                var lr=label.GetComponent<RectTransform>();lr.sizeDelta=wr.sizeDelta;
                var lt=label.GetComponent<Text>();lt.font=text.font;lt.fontSize=28;lt.lineSpacing=1.5f;lt.color=text.color;lt.text="明早交稿。\n\n杯子，笔，灯……\n就画眼前这些。";lt.raycastTarget=false;
            }
        }
        private static void BakeMasks(Transform art,AtelierGameplay game)
        {
            const int width=1024,height=800;
            var materials=new[]{new Color(1,0,0,0),new Color(0,1,0,0),new Color(0,0,1,0),new Color(0,0,0,1)}.Select(c=>{var m=new Material(Shader.Find("Hidden/RoomDemo/Atelier/MotifID"));m.SetColor("_IDColour",c);return m;}).ToArray();
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var originals=renderers.Select(r=>r.sharedMaterials).ToArray();
            var oldQuality=QualitySettings.renderPipeline;
            var capturePipeline=Object.Instantiate((UniversalRenderPipelineAsset)art.GetComponent<AtelierArtState>().ScenePipeline);
            var pipelineData=new SerializedObject(capturePipeline);
            pipelineData.FindProperty("m_GPUResidentDrawerMode").intValue=0;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();


            var cameraObject=Object.Instantiate(game.ViewCamera.gameObject);var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(-.15f,1.98f,-3);camera.transform.LookAt(new Vector3(.65f,1.65f,1.6f));camera.fieldOfView=58;camera.aspect=(float)width/height;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0,0,0,1);
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var oldActive=RenderTexture.active;
            var easel=art.Find("室内家具/细木作画架");bool active=easel.gameObject.activeSelf;Texture2D capture=null;
            try
            {
                // 分色使用独立临时管线关闭 GPU 常驻绘制；源材质与共享管线在 finally 中完整归还。
                QualitySettings.renderPipeline=capturePipeline;
                for(int i=0;i<renderers.Length;i++)
                {
                    var r=renderers[i];int stage=3;string objectPath=PathOf(r.transform);
                    // 杯层只包含杯身和液面；桌面属于后续环境，不能跟着杯子显出一大片裁切。
                    if(objectPath.Contains("独立洗笔工作簇")&&!objectPath.Contains("杯口支承点"))stage=0;
                    if(objectPath.Contains("杯口支承点"))stage=1;
                    if(objectPath.Contains("扫描维多利亚旧油灯"))stage=2;
                    r.sharedMaterials=Enumerable.Repeat(materials[stage],originals[i].Length).ToArray();
                }
                easel.gameObject.SetActive(false);camera.targetTexture=target;camera.Render();
                RenderTexture.active=target;
                capture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);capture.ReadPixels(new Rect(0,0,width,height),0,0);capture.Apply();
                var pixels=capture.GetPixels();var totals=new int[4];
                for(int i=0;i<pixels.Length;i++)
                {
                    Color c=pixels[i];int stage=3;float max=.35f;
                    if(c.r>max){stage=0;max=c.r;}
                    if(c.g>max){stage=1;max=c.g;}if(c.b>max){stage=2;max=c.b;}
                    pixels[i]=stage==0?new Color(1,0,0,0):stage==1?new Color(0,1,0,0):stage==2?new Color(0,0,1,0):new Color(0,0,0,1);totals[stage]++;
                }
                if(totals.Take(3).Any(n=>n==0))throw new InvalidOperationException("静物分色缺少可见色层，拒绝保存。请检查捕获矩阵及深度。 ");
                capture.SetPixels(pixels);capture.Apply();
                string path=AtelierGameplayBuilder.Content+"Textures/静物真实几何分色.png";File.WriteAllBytes(path,capture.EncodeToPNG());AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                game.Painting.Surface.sharedMaterial.SetTexture("_MotifMask",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                Directory.CreateDirectory("Logs/AtelierOpening20261011");File.WriteAllText("Logs/AtelierOpening20261011/静物分色像素.txt",string.Join("\n",totals.Select((n,i)=>i+"="+n)));
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].sharedMaterials=originals[i];
                QualitySettings.renderPipeline=oldQuality;Object.DestroyImmediate(capturePipeline);
                easel.gameObject.SetActive(active);RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);
                if(capture!=null)Object.DestroyImmediate(capture);Object.DestroyImmediate(cameraObject);foreach(var m in materials)Object.DestroyImmediate(m);
            }
        }
        /// <summary>仅修复玩法副本的分色；不重建场景、便条或点灯工作簇。</summary>
        public static string RepairPaintingMasks()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=AtelierGameplayBuilder.ScenePath||scene.isDirty)
                throw new InvalidOperationException("请先保存独立玩法场景并退出运行模式。");
            var game=Object.FindAnyObjectByType<AtelierGameplay>();
            BakeMasks(GameObject.Find("独立夜间画室").transform,game);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "已修复杯层分色，保留当前人工场景。";
        }
        private static string PathOf(Transform t) => t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        private static Transform Child(Transform parent,string name)
        {
            var found=parent.Find(name);if(found!=null)return found;
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        private static void AddBox(Transform parent,string name,Vector3 centre,Vector3 size,AtelierTargetKind kind,ObjectId id,string label)
        {
            var t=Child(parent,name);t.position=centre;t.gameObject.layer=2;
            var box=t.GetComponent<BoxCollider>();if(box==null)box=t.gameObject.AddComponent<BoxCollider>();box.size=size;Target(t,kind,id,label);
        }
        private static void Target(Transform t,AtelierTargetKind kind,ObjectId id,string label)
        {
            var target=t.GetComponent<AtelierInteractionTarget>();if(target==null)target=t.gameObject.AddComponent<AtelierInteractionTarget>();target.Kind=kind;target.ObjectId=id.Value;target.DisplayName=label;
        }
    }
}
