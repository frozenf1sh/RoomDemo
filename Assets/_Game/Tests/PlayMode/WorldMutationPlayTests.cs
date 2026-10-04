// =============================================================================
// 文件：WorldMutationPlayTests.cs
// 职责：验证场景表现操作、生成物去重和复原，不把画面质量编码为像素测试。
// 边界：在实际房间测试表现 API；不能借此宣称玩家认出了因果关系。
// =============================================================================
using System.Collections;
using Game.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
namespace Game.Tests
{
    public sealed class WorldMutationPlayTests
    {
        private GameBootstrap bootstrap;
        private RoomModules room;
        private WorldExterior exterior;
        private WorldMutation mutation;
        [UnitySetUp] public IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Content/Scenes/DemoRoom.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            bootstrap = Object.FindAnyObjectByType<GameBootstrap>(); room = Object.FindAnyObjectByType<RoomModules>(); exterior = Object.FindAnyObjectByType<WorldExterior>(); mutation = Object.FindAnyObjectByType<WorldMutation>();
        }
        [UnityTest] public IEnumerator DirectVisualOperationsRestoreAndSpawnOnlyOneInstance()
        {
            var position = room.Ceiling.localPosition; var scale = room.Ceiling.localScale; var rotation = room.Ceiling.localRotation;
            mutation.Hide(room.Ceiling.gameObject); Assert.That(room.Ceiling.gameObject.activeSelf, Is.False);
            mutation.Show(room.Ceiling.gameObject); mutation.Move(room.Ceiling, position, position + Vector3.up * 2, 1);
            mutation.Scale(room.Ceiling, scale, scale * .5f, 1); mutation.Rotate(room.Ceiling, rotation, Quaternion.Euler(0, 20, 0), 1);
            mutation.ReplaceVisual(room.Floor.gameObject, exterior.FloorPatch); Assert.That(exterior.FloorPatch.activeSelf, Is.True);
            var fire = mutation.Spawn("Campfire", exterior.CampfirePrefab, exterior.CampfireAnchor);
            Assert.That(mutation.Spawn("Campfire", exterior.CampfirePrefab, exterior.CampfireAnchor), Is.SameAs(fire)); Assert.That(mutation.SpawnedCount, Is.EqualTo(1));
            var renderer = room.Floor.GetComponent<Renderer>(); mutation.MaterialTransition(renderer, renderer.sharedMaterial.color, Color.cyan, .5f);
            Assert.That(bootstrap.Session.State.Facts, Is.Empty); Assert.That(bootstrap.Session.State.KnownClues, Is.Empty);
            mutation.ResetCapturedState(); yield return null;
            Assert.That(room.Ceiling.gameObject.activeSelf, Is.True); Assert.That(room.Ceiling.localPosition, Is.EqualTo(position)); Assert.That(room.Ceiling.localScale, Is.EqualTo(scale)); Assert.That(room.Ceiling.localRotation, Is.EqualTo(rotation));
            Assert.That(room.Floor.gameObject.activeSelf, Is.True); Assert.That(exterior.FloorPatch.activeSelf, Is.False); Assert.That(mutation.SpawnedCount, Is.Zero); Assert.That(fire == null, Is.True);
        }
        /// <summary>真实事实先成立，建筑按不同节拍打开，原桌子与玩法身份继续保留。</summary>
        [UnityTest] public IEnumerator StarryFactIsImmediateButArchitectureAndExteriorAreStaged()
        {
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.BeforeStarryWorld);
            var sequence = Object.FindAnyObjectByType<StarryWorldSequence>();
            var table = room.transform.parent.Find("Table");
            // 保留对象直接从主场景取值，不能把外景当成下一张场景加载。
            var tablePosition = table.position;
            bootstrap.PlayStarryBenchmark();
            Assert.That(bootstrap.Session.State.HasFact(Game.Domain.ExplorationRules.WorldStarry), Is.True);
            Assert.That(sequence.IsPlaying, Is.True);
            Assert.That(room.Ceiling.gameObject.activeSelf && room.WallBack.gameObject.activeSelf, Is.True);
            Assert.That(exterior.Environment.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.That(room.Ceiling.gameObject.activeSelf, Is.False);
            Assert.That(room.WallBack.gameObject.activeSelf, Is.True);
            Assert.That(exterior.Environment.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(sequence.Duration - 4.1f + .3f);
            Assert.That(sequence.IsPlaying, Is.False); Assert.That(sequence.IsFinalPresenting, Is.True);
            Assert.That(room.WallBack.gameObject.activeSelf || room.WindowBoundary.gameObject.activeSelf || room.Floor.gameObject.activeSelf, Is.False);
            Assert.That(exterior.Environment.activeSelf && exterior.SkyDome.activeSelf && exterior.FloorPatch.activeSelf, Is.True);
            Assert.That(mutation.SpawnedCount, Is.EqualTo(1)); Assert.That(table.position, Is.EqualTo(tablePosition));
            Assert.That(Object.FindObjectsByType<SceneEntity>().Length, Is.EqualTo(5));
        }
        /// <summary>独立演出预览不能写世界；重开中断后旧协程不能继续隐藏新房间。</summary>
        [UnityTest] public IEnumerator PreviewIsReadOnlyAndRestartCancelsAllBorrowedVisuals()
        {
            var sequence = Object.FindAnyObjectByType<StarryWorldSequence>(); sequence.PlayBenchmarkPreview();
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.That(bootstrap.Session.State.Facts, Is.Empty); Assert.That(bootstrap.Session.State.KnownClues, Is.Empty);
            Assert.That(room.Ceiling.gameObject.activeSelf, Is.False);
            bootstrap.Restart(); yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(sequence.IsPlaying || sequence.IsFinalPresenting, Is.False);
            Assert.That(room.Ceiling.gameObject.activeSelf && room.WallBack.gameObject.activeSelf && room.Floor.gameObject.activeSelf, Is.True);
            Assert.That(exterior.Environment.activeSelf || exterior.SkyDome.activeSelf, Is.False);
            Assert.That(mutation.SpawnedCount, Is.Zero);
        }
        /// <summary>最终预设反复恢复也只有一个篝火；返回前置状态归还建筑与相机范围。</summary>
        [UnityTest] public IEnumerator FinalPresetRestoresImmediatelyAndNeverAccumulatesCampfires()
        {
            var far = Camera.main.farClipPlane;
            var sequence = Object.FindAnyObjectByType<StarryWorldSequence>();
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.StarryWorldFinal); yield return null;
            Assert.That(sequence.IsPlaying, Is.False); Assert.That(sequence.IsFinalPresenting, Is.True);
            Assert.That(mutation.SpawnedCount, Is.EqualTo(1));
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.StarryWorldFinal); yield return null;
            Assert.That(mutation.SpawnedCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<CampfireVisual>().Length, Is.EqualTo(1));
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.BeforeStarryWorld); yield return null;
            Assert.That(sequence.IsPlaying || sequence.IsFinalPresenting, Is.False);
            Assert.That(room.Ceiling.gameObject.activeSelf && room.WindowBoundary.gameObject.activeSelf, Is.True);
            Assert.That(mutation.SpawnedCount, Is.Zero); Assert.That(Camera.main.farClipPlane, Is.EqualTo(far));
        }
    }
}
