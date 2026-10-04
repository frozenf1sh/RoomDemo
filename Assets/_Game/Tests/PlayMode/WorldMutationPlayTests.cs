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
    }
}
