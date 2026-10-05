// =============================================================================
// 文件：ContentFeedback.cs
// 职责：消费发现与组合事件，按内容表播放轻声音、局部光点和单行字幕。
// 边界：只读会话；序列 ID 指向已有导演，不新增事实、不发放线索。
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>可配置声音条目，资源与音量统一在场景安装器中绑定。</summary>
    [Serializable] public sealed class AudioCueDefinition
    {
        public string Id;
        public AudioClip Clip;
        [Range(0,1)] public float Volume=.15f;
    }
    /// <summary>三类通用反馈的小型路由器，不是通用事件框架。</summary>
    public sealed class ContentFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource voice;
        [SerializeField] private AudioCueDefinition[] cues;
        [SerializeField] private RecipePresentation[] definitions;
        [SerializeField] private OneLineSubtitle subtitles;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private InspectionController inspection;
        [SerializeField] private StarryWorldSequence worldSequence;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private ParticleSystem motes;
        private GameSession session;
        private readonly HashSet<ClueId> recipeOutputs = new HashSet<ClueId>();
        private readonly Dictionary<string, Action> sequences = new Dictionary<string, Action>();
        private readonly Dictionary<ObjectId, Transform> sources = new Dictionary<ObjectId, Transform>();
        /// <summary>绑定项目内容、明确资源和已有消费者；声音片段不散落在热点中。</summary>
        public void Configure(AudioSource audio, AudioCueDefinition[] sounds, RecipePresentation[] content,
            OneLineSubtitle caption, NotebookView book, InspectionController closeup, StarryWorldSequence director, Camera camera, ParticleSystem glints)
        { voice=audio; cues=sounds; definitions=content; subtitles=caption; notebook=book; inspection=closeup; worldSequence=director; viewCamera=camera; motes=glints; }
        /// <summary>重绑清理上一局声音、字幕、微粒和监听；预设只恢复视觉，不播获得反馈。</summary>
        public void Bind(GameSession game)
        {
            if(session!=null) session.EventRaised-=OnEvent;
            StopAllCoroutines(); voice.Stop(); subtitles.Clear(); motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            recipeOutputs.Clear(); sources.Clear(); sequences.Clear(); session=game;
            foreach(var entity in transform.root.GetComponentsInChildren<SceneEntity>(true)) sources[entity.Id]=entity.transform;
            sequences.Add("ceiling_starry",worldSequence.PlayCeilingFromContent);
            sequences.Add("forest_world",worldSequence.PlayForestFromContent);
            session.EventRaised+=OnEvent;
        }
        private void OnEvent(DomainEvent value)
        {
            if(value is CombinationSucceeded success)
            {
                foreach(var produced in success.ProducedClues) recipeOutputs.Add(produced);
                var content=definitions.FirstOrDefault(x=>x.RecipeId==success.RecipeId);
                if(content!=null)
                {
                    PlayCue(content.AudioCue);
                    if(!string.IsNullOrEmpty(content.VfxCue)) EmitGlints(2);
                    if(sequences.TryGetValue(content.SequenceId ?? "",out var run)) run();
                    if(!string.IsNullOrEmpty(content.Subtitle)) StartCoroutine(AfterNotebook(content.Subtitle));
                }
            }
            if(value is ClueDiscovered clue && !recipeOutputs.Contains(clue.Clue))
            {
                PlayCue("clue_discover_soft"); EmitGlints(3,clue.Clue);
            }
        }
        private IEnumerator AfterNotebook(string text)
        {
            // 字幕在合页后进入场景，避免概念书写与解释文字争夺注意力。
            yield return new WaitForSecondsRealtime(.8f);
            while(notebook.IsOpen) yield return null;
            subtitles.Show(text);
        }
        /// <summary>根据稳定 Cue ID 找到声音；不存在时静默略过可选反馈。</summary>
        public void PlayCue(string id)
        {
            var cue=cues.FirstOrDefault(x=>x.Id==id);
            if(cue!=null && cue.Clip!=null) voice.PlayOneShot(cue.Clip,cue.Volume);
        }
        private void EmitGlints(int count, ClueId? discovered=null)
        {
            Vector3 point=viewCamera.transform.position+viewCamera.transform.forward*1.5f;
            int layer=inspection.IsOpen ? InspectionController.InspectionLayer : 0;
            if(discovered.HasValue && !inspection.IsOpen)
            {
                var source=session.Rules.Observations.FirstOrDefault(x=>x.Grants.Contains(discovered.Value))?.Source;
                if(!source.HasValue) source=session.Rules.Discoveries.FirstOrDefault(x=>x.Grants.Contains(discovered.Value))?.Source;
                if(source.HasValue && sources.TryGetValue(source.Value,out var target)) point=target.position;
            }
            motes.gameObject.layer=layer; motes.transform.position=point; motes.Emit(count);
        }
        private void OnDestroy() { if(session!=null) session.EventRaised-=OnEvent; }
    }
}
