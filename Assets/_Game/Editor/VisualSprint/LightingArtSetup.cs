// =============================================================================
// 文件：LightingArtSetup.cs
// 职责：为工作室建立局部暖光、月光方向与克制的色调、辉光和暗角。
// 边界：仅修改场景自身的灯和独立 VolumeProfile，不覆盖项目渲染设置或玩法状态。
// =============================================================================
using System.IO;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor.VisualSprint
{
    /// <summary>第五阶段具体光照调整，所有参数可在场景中继续人工调节。</summary>
    public static class LightingArtSetup
    {
        private const string ProfilePath = "Assets/_Game/Content/Art/Materials/VisualSprint/StudioAtmosphere.asset";
        /// <summary>增量安装独立氛围资产，保持房间模块和领域绑定。</summary>
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            var sun = root.Find("_Systems/Sun").GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(42, -24, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .78f;
            var fill = root.Find("_Systems/RoomFill").GetComponent<Light>();
            fill.transform.localPosition = new Vector3(-1.8f, 2.6f, -1.2f);
            fill.range = 7.5f;
            var lighting = PresentationArtPass.Ensure(root.Find("Presentation"), "Lighting");
            var node = PresentationArtPass.Ensure(lighting, "StudioAtmosphere");
            var volume = node.GetComponent<Volume>();
            if (volume == null) volume = node.gameObject.AddComponent<Volume>();
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // 保存子资产，使真实构建也具有同一色调；不引用用户正在修改的默认 Profile。
            var tone = Setting<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);
            var color = Setting<ColorAdjustments>(profile);
            color.postExposure.Override(.50f); color.contrast.Override(6); color.saturation.Override(-4);
            var bloom = Setting<Bloom>(profile);
            bloom.threshold.Override(1.15f); bloom.intensity.Override(.26f); bloom.scatter.Override(.48f);
            var vignette = Setting<Vignette>(profile);
            vignette.intensity.Override(.12f); vignette.smoothness.Override(.65f);
            var rimNode = PresentationArtPass.Ensure(lighting, "PlantRim");
            var rim = rimNode.GetComponent<Light>();
            if (rim == null) rim = rimNode.gameObject.AddComponent<Light>();
            rim.type = LightType.Point; rim.range = 3.1f; rim.intensity = 1.65f;
            rim.color = new Color(.90f, .82f, .65f);
            rim.transform.localPosition = new Vector3(-.10f, 2.25f, .50f);
            volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = profile;
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                // 检视和房间使用同一温和后处理，没有强制镜头或高成本实时反射。
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
            }
            EditorUtility.SetDirty(profile);
            PresentationArtPass.Save(root);
            Debug.Log("光照美术：局部暖光、独立色调与星空月光方向已保存。");
        }
        /// <summary>重用少量明确的后处理组件，避免重复子资产。</summary>
        private static T Setting<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var result)) return result;
            result = profile.Add<T>();
            AssetDatabase.AddObjectToAsset(result, profile);
            return result;
        }
    }
}
