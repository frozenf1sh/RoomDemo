// =============================================================================
// 文件：NarrativePainterlyPass.cs
// 职责：统一房间与外景的轻笔触表面、星夜色彩、流动光点和画架焦点。
// 边界：仅创建表现材质资产和增量绑定，排除 UI、文字、粒子与已有专用 Hero Shader。
// =============================================================================
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    /// <summary>两条路线打通后执行的具体色彩与材质调整，不新增下载资源或玩法。</summary>
    public static class NarrativePainterlyPass
    {
        /// <summary>保留原贴图与色彩，为普通家具加入与森林、植物一致的笔触响应。</summary>
        [MenuItem("游戏/叙事/统一流动星夜表现")]
        public static void Apply()
        {
            var root=PresentationArtPass.Room();
            const string folder="Assets/_Game/Content/Art/Materials/Environment/Narrative";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // TextMesh 也有 MeshRenderer，必须排除；专用 Shader 的参数仍由其 Presenter 管理。
                if(renderer.GetComponent<TextMesh>()!=null || renderer.sharedMaterial==null) continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var old=materials[i];
                    if(old==null || (old.shader.name!="Universal Render Pipeline/Lit" && old.shader.name!="Universal Render Pipeline/Simple Lit" && old.shader.name!="Standard")) continue;
                    if(old.HasProperty("_Surface") && old.GetFloat("_Surface")>0) continue;
                    string name=old.name.Replace(" ","_").Replace("/","_")+"_Pigment";
                    string path=folder+"/"+name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null)
                    { material=new Material(Shader.Find("Game/PainterlyWind")); AssetDatabase.CreateAsset(material,path); }
                    var color=old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : old.color;
                    material.SetColor("_BaseColor",color);
                    material.SetColor("_ShadowColor",new Color(.12f,.17f,.23f));
                    material.SetFloat("_WindStrength",0); material.SetFloat("_BrushStrength",.08f);
                    material.SetFloat("_TrunkHeight",-1); material.SetFloat("_Dissolve",0);
                    if(old.HasProperty("_BaseMap"))
                    {
                        material.SetTexture("_BaseMap",old.GetTexture("_BaseMap"));
                        material.SetTextureScale("_BaseMap",old.GetTextureScale("_BaseMap"));
                        material.SetTextureOffset("_BaseMap",old.GetTextureOffset("_BaseMap"));
                    }
                    EditorUtility.SetDirty(material); materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
            var field=root.GetComponentInChildren<WorldExterior>(true);
            var sky=field.SkyDome.GetComponentInChildren<Renderer>(true).sharedMaterial;
            const string skyPath="Assets/_Game/Content/Art/Textures/Hero/NarrativeNightSky.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(skyPath);
            importer.wrapModeU=TextureWrapMode.Repeat; importer.wrapModeV=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048; importer.sRGBTexture=true; importer.mipmapEnabled=true; importer.SaveAndReimport();
            // 重导入会使先前持有的 Texture2D 失效；必须重新取资产再写入持久化材质。
            var skyTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(skyPath);
            sky.SetTexture("_PainterlySky",skyTexture); sky.SetFloat("_SkyTextureBlend",.96f);
            sky.SetFloat("_Stars",.92f); sky.SetFloat("_Nebula",1.25f); sky.SetFloat("_Moon",1.10f);
            sky.SetColor("_Zenith",new Color(.032f,.047f,.085f));
            sky.SetColor("_Horizon",new Color(.13f,.23f,.27f));
            sky.SetColor("_CloudColor",new Color(.19f,.31f,.37f)); EditorUtility.SetDirty(sky);
            var profile=AssetDatabase.LoadAssetAtPath<VisualThemeProfile>("Assets/_Game/Content/Art/WorldExterior/Themes/StarryWorld.asset");
            profile.Ambient=new Color(.21f,.28f,.32f); profile.Fog=new Color(.105f,.18f,.23f);
            profile.Sun=new Color(.67f,.77f,.91f); profile.SunIntensity=1.12f;
            profile.Fill=new Color(1,.68f,.40f); profile.FillIntensity=.65f; profile.FogDensity=.018f;
            EditorUtility.SetDirty(profile);

            var node=PresentationArtPass.Ensure(root.Find("Presentation"),"PainterlyWorld");
            var glints=PresentationArtPass.Ensure(node,"FlowingLights"); glints.position=new Vector3(0,1.6f,4.5f);
            var particles=glints.GetComponent<ParticleSystem>(); if(particles==null) particles=glints.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main; main.playOnAwake=false; main.loop=true; main.startLifetime=12;
            main.startSpeed=0; main.startSize=new ParticleSystem.MinMaxCurve(.018f,.035f); main.maxParticles=24;
            main.startColor=new Color(.98f,.83f,.52f,.42f); main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission; emission.rateOverTime=0;
            var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(9,3,11);
            var velocity=particles.velocityOverLifetime; velocity.enabled=true;
            velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(.07f,.13f); velocity.y=new ParticleSystem.MinMaxCurve(.015f,.045f); velocity.z=new ParticleSystem.MinMaxCurve(-.02f,.03f);
            var fade=particles.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.84f,.52f),0),new GradientColorKey(new Color(.55f,.81f,.83f),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.40f,.25f),new GradientAlphaKey(.30f,.7f),new GradientAlphaKey(0,1)}); fade.color=gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=root.GetComponentsInChildren<ParticleSystemRenderer>(true)
                .First(x=>x!=particles.GetComponent<ParticleSystemRenderer>() && x.sharedMaterial!=null && x.sharedMaterial.shader.name=="Game/AtmosphereParticle" && x.sharedMaterial.GetFloat("_Mode")<.5f).sharedMaterial;
            var painterly=node.GetComponent<PainterlyWorldPresenter>(); if(painterly==null) painterly=node.gameObject.AddComponent<PainterlyWorldPresenter>();
            painterly.Configure(particles); root.GetComponentInChildren<GameBootstrap>(true).ConfigurePainterly(painterly);
            // 初始视线稍向画架偏转；转头范围仍固定，不新增自动电影镜头。
            root.Find("CameraRig").localRotation=Quaternion.Euler(0,11,0);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials.Where(x=>x!=null && x.shader.name=="Game/WorldBoundary"))
                { material.SetColor("_EdgeColor",new Color(.85f,1.0f,.77f)); material.SetFloat("_EdgeWidth",.13f); EditorUtility.SetDirty(material); }
            PresentationArtPass.Save(root);
            Debug.Log("[叙事美术] 家具、植被、海与星空采用统一笔触，空气光点保持稀疏缓慢。");
        }
    }
}
