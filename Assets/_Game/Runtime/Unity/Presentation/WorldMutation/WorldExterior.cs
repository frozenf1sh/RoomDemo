// =============================================================================
// 文件：WorldExterior.cs
// 职责：绑定初始隐藏的外景、天空、地板替换层以及按需生成的篝火资源。
// 边界：这里只引用表现内容；不在场景预埋一个已生成的篝火，也不保存事实。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    public sealed class WorldExterior : MonoBehaviour
    {
        [SerializeField] private GameObject environment, floorPatch, skyDome, campfirePrefab;
        [SerializeField] private Transform campfireAnchor;
        public GameObject Environment => environment;
        public GameObject FloorPatch => floorPatch;
        public GameObject SkyDome => skyDome;
        public GameObject CampfirePrefab => campfirePrefab;
        public Transform CampfireAnchor => campfireAnchor;
        public void Configure(GameObject field, GameObject deck, GameObject sky, GameObject fire, Transform anchor)
        {
            // 数据对象保持激活，视觉子组默认隐藏，由具体演出消费者决定何时显现。
            environment = field; floorPatch = deck; skyDome = sky; campfirePrefab = fire; campfireAnchor = anchor;
        }
    }
}
