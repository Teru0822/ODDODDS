using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "FeverCapital/Roguelike/Preview Registry", fileName = "RoguelikePreviewRegistry")]
public class RoguelikePreviewRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SkillId skillId;
        [Tooltip("プレビュー動画（null なら fallbackSprite を表示）")]
        public VideoClip previewClip;
        [Tooltip("動画なし時のフォールバック静止画")]
        public Sprite fallbackSprite;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();
    private Dictionary<SkillId, Entry> _map;

    private void OnEnable() => _map = null;

    public Entry Get(SkillId id)
    {
        if (_map == null)
        {
            _map = new Dictionary<SkillId, Entry>();
            foreach (var e in _entries)
                if (e != null) _map[e.skillId] = e;
        }
        return _map.TryGetValue(id, out var entry) ? entry : null;
    }
}
