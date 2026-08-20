using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 3 見開き目。
///   左ページ … アンロック済みのローグライク要素をリスト表示。行を左クリックで選択。
///   右ページ … 選択中の要素の説明動画（無ければ静止画）を大きく表示し、
///               下側に有効化／無効化を切り替えるボタンを置く。
/// </summary>
public class BookRoguelikePage : IBookPage
{
    private readonly BookPagePalette _palette;
    private readonly RoguelikePreviewRegistry _previewRegistry;

    private RectTransform _listContent;
    private readonly List<Row> _rows = new List<Row>();

    private RawImage _previewVideo;
    private Image _previewImage;
    private VideoPlayer _videoPlayer;
    private RenderTexture _videoTexture;

    private TextMeshProUGUI _detailName;
    private TextMeshProUGUI _detailDescription;
    private TextMeshProUGUI _toggleLabel;
    private Image _toggleBack;

    private RoguelikeData _selected;
    private Language _language;

    public void SetLocalize(Language language)
    {
        _language = language;
        Refresh();
    }

    private class Row
    {
        public RoguelikeData Data;
        public GameObject Outline;
        public TextMeshProUGUI Label;
    }

    public BookRoguelikePage(BookPagePalette palette, RoguelikePreviewRegistry previewRegistry)
    {
        _palette = palette;
        _previewRegistry = previewRegistry;
    }

    public void Build(RectTransform left, RectTransform right)
    {
        BuildLeft(left);
        BuildRight(right);
    }

    private void BuildLeft(RectTransform page)
    {
        var title = BookUIBuilder.Text(page, "Title", "Roguelike", 56f,
                                       TextAlignmentOptions.TopLeft, _palette.Accent, _palette.Font, true,"BookUITable","RoguelikeTitle",true,60,18);
        BookUIBuilder.AnchorRect(title.rectTransform, 0f, 0.90f, 1f, 1f);

        var area = BookUIBuilder.Panel(page, "ScrollArea");
        BookUIBuilder.AnchorRect(area, 0f, 0f, 1f, 0.89f);

        _listContent = BookUIBuilder.ScrollArea(area, "Viewport", _palette.ScrollSensitivity, out _);
    }

    private void BuildRight(RectTransform page)
    {
        // 動画と静止画は同じ枠に重ねて置き、ある方だけを出す
        var frame = BookUIBuilder.Panel(page, "PreviewFrame");
        BookUIBuilder.AnchorRect(frame, 0.02f, 0.58f, 0.98f, 0.97f);

        var videoGo = new GameObject("PreviewVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        _previewVideo = videoGo.GetComponent<RawImage>();
        _previewVideo.rectTransform.SetParent(frame, false);
        BookUIBuilder.Stretch(_previewVideo.rectTransform);
        _previewVideo.raycastTarget = false;
        _previewVideo.enabled = false;

        _previewImage = BookUIBuilder.Sprite(frame, "PreviewImage", null);
        BookUIBuilder.Stretch(_previewImage.rectTransform);

        _detailName = BookUIBuilder.Text(page, "DetailName", "", 42f,
                                         TextAlignmentOptions.Top, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(_detailName.rectTransform, 0f, 0.48f, 1f, 0.57f);

        _detailDescription = BookUIBuilder.Text(page, "DetailDescription", "", 30f,
                                                TextAlignmentOptions.TopLeft, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(_detailDescription.rectTransform, 0f, 0.16f, 1f, 0.47f);

        var toggle = BookUIBuilder.LabelButton(page, "ToggleButton", "", new Vector2(340f, 84f), 34f,
                                               _palette.Text, new Color(0f, 0f, 0f, 0.08f), ToggleSelectedActive);
        var toggleRt = toggle.GetComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(0.5f, 0.04f);
        toggleRt.anchorMax = new Vector2(0.5f, 0.04f);
        toggleRt.pivot = new Vector2(0.5f, 0f);
        toggleRt.anchoredPosition = Vector2.zero;

        _toggleBack = toggle.GetComponent<Image>();
        _toggleLabel = toggle.GetComponentInChildren<TextMeshProUGUI>();

        ShowDetail(null);
    }

    public void Refresh()
    {
        RebuildList();
        ShowDetail(_selected);
    }

    private void RebuildList()
    {
        if (_listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(_listContent.GetChild(i).gameObject);
        }
        _rows.Clear();

        var manager = Object.FindFirstObjectByType<RoguelikeManager>();
        if (manager == null)
        {
            BookUIBuilder.Text(_listContent, "Empty", "RoguelikeManager not found", 28f,
                               TextAlignmentOptions.Top, _palette.Text, _palette.Font);
            return;
        }

        var unlocked = manager.GetUnlockSkillDictionary.Values
                              .Where(d => d != null)
                              .OrderBy(d => d.skillType)
                              .ThenBy(d => d.id)
                              .ToList();

        if (unlocked.Count == 0)
        {
            BookUIBuilder.Text(_listContent, "Empty", "Nothing acquired yet.", 28f,
                               TextAlignmentOptions.Top, _palette.Text, _palette.Font);
            return;
        }

        foreach (var data in unlocked) AddRow(data);
    }

    private void AddRow(RoguelikeData data)
    {
        var rowGo = new GameObject($"Row_{data.id}", typeof(RectTransform));
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.SetParent(_listContent, false);

        var element = rowGo.AddComponent<LayoutElement>();
        element.preferredHeight = 72f;

        var outline = BookUIBuilder.OutlineFrame(rowRt, "Outline", _palette.Selection, 3f);
        outline.gameObject.SetActive(false);

        var label = BookUIBuilder.Text(rowRt, "Label", "", 32f,
                                       TextAlignmentOptions.Left, _palette.Text, _palette.Font);
        BookUIBuilder.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(16f, 0f);
        label.rectTransform.offsetMax = new Vector2(-16f, 0f);

        var row = new Row { Data = data, Outline = outline.gameObject, Label = label };
        _rows.Add(row);
        UpdateRowLabel(row);

        BookUIBuilder.Clickable(rowGo, () => Select(row));
    }

    /// <summary>無効化中の要素はひと目で分かるよう、名前を薄くして印を添える。</summary>
    private void UpdateRowLabel(Row row)
    {
        if (row.Label == null) return;

        row.Label.text = row.Data.isActive ? row.Data.skillName : $"{row.Data.skillName}  (Disabled)";
        row.Label.color = row.Data.isActive
            ? _palette.Text
            : new Color(_palette.Text.r, _palette.Text.g, _palette.Text.b, 0.45f);
    }

    private void Select(Row row)
    {
        _selected = row.Data;

        foreach (var r in _rows)
        {
            if (r.Outline != null) r.Outline.SetActive(r == row);
        }

        ShowDetail(_selected);
    }

    private void ToggleSelectedActive()
    {
        if (_selected == null) return;

        _selected.isActive = !_selected.isActive;

        var row = _rows.FirstOrDefault(r => r.Data == _selected);
        if (row != null) UpdateRowLabel(row);

        UpdateToggleButton();
    }

    private void ShowDetail(RoguelikeData data)
    {
        if (_detailName == null) return;

        if (data == null)
        {
            _detailName.text = "";
            if (_detailDescription != null) _detailDescription.text = "Select an entry.";
            StopPreview();
            UpdateToggleButton();
            return;
        }

        _detailName.text = data.skillName;
        if (_detailDescription != null) _detailDescription.text = data.skillDescription;

        PlayPreview(data);
        UpdateToggleButton();
    }

    private void UpdateToggleButton()
    {
        if (_toggleLabel == null) return;

        if (_selected == null)
        {
            _toggleLabel.text = "—";
            if (_toggleBack != null) _toggleBack.color = new Color(0f, 0f, 0f, 0.04f);
            return;
        }

        _toggleLabel.text = _selected.isActive ? "Disable" : "Enable";
        if (_toggleBack != null)
        {
            _toggleBack.color = _selected.isActive
                ? new Color(_palette.Accent.r, _palette.Accent.g, _palette.Accent.b, 0.18f)
                : new Color(0f, 0f, 0f, 0.12f);
        }
    }

    /// <summary>動画があれば RenderTexture に流す。無ければフォールバック静止画を出す。</summary>
    private void PlayPreview(RoguelikeData data)
    {
        var entry = _previewRegistry != null ? _previewRegistry.Get(data.id) : null;

        if (entry != null && entry.previewClip != null)
        {
            EnsureVideoPlayer();

            _videoPlayer.clip = entry.previewClip;
            _videoPlayer.Play();

            _previewVideo.texture = _videoTexture;
            _previewVideo.enabled = true;
            if (_previewImage != null) _previewImage.enabled = false;
            return;
        }

        StopPreview();

        if (_previewImage != null)
        {
            Sprite fallback = entry != null ? entry.fallbackSprite : null;
            _previewImage.sprite = fallback;
            _previewImage.enabled = fallback != null;
        }
    }

    private void EnsureVideoPlayer()
    {
        if (_videoPlayer != null) return;

        _videoTexture = new RenderTexture(960, 540, 0) { name = "BookRoguelikePreview" };

        var host = new GameObject("BookPreviewVideoPlayer");
        Object.DontDestroyOnLoad(host);

        _videoPlayer = host.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _videoTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    private void StopPreview()
    {
        if (_videoPlayer != null) _videoPlayer.Stop();
        if (_previewVideo != null) _previewVideo.enabled = false;
    }
}
