using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class GramophoneController : MonoBehaviour
{
    [Header("Music Settings")]
    [Tooltip("再生する楽曲のリスト")]
    public List<AudioClip> musicTracks;

    [Header("Interaction Settings")]
    [Tooltip("曲を進めるキー")]
    public Key interactKey = Key.F;
    
    [Tooltip("曲を止めるキー")]
    public Key stopKey = Key.Q;

    private AudioSource _audioSource;
    private int _currentTrackIndex = 0;
    private bool _isPlayerNear = false;

    // 動的UI要素 (Devilキャッチャーと同様の動的生成)
    private Canvas _dynamicCanvas;
    private TextMeshProUGUI _promptText;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        
        // BGM は 2D 再生にする（SurroundBGMAudioSource の panStereo による左右定位を効かせるため。
        // 3D にすると panStereo が無効になり、左右に振っても定位しない）
        _audioSource.spatialBlend = 0.0f;
        _audioSource.loop = true;

        // 動的UIの生成
        CreateDynamicUI();

        if (musicTracks != null && musicTracks.Count > 0)
        {
            PlayTrack(0);
        }
    }

    void Update()
    {
        if (_isPlayerNear && Keyboard.current != null)
        {
            // Fキーで次の曲へ
            if (Keyboard.current[interactKey].wasPressedThisFrame)
            {
                PlayNextTrack();
            }
            
            // Qキーで再生停止
            if (Keyboard.current[stopKey].wasPressedThisFrame)
            {
                StopTrack();
            }
        }
    }

    private void StopTrack()
    {
        if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    private void PlayNextTrack()
    {
        if (musicTracks == null || musicTracks.Count == 0) return;

        _currentTrackIndex = (_currentTrackIndex + 1) % musicTracks.Count;
        PlayTrack(_currentTrackIndex);
    }

    private void PlayTrack(int index)
    {
        if (musicTracks[index] != null)
        {
            _audioSource.clip = musicTracks[index];
            _audioSource.Play();
            Debug.Log($"[Gramophone] Now playing: {musicTracks[index].name}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーが範囲に入ったか判定
        if (other.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            _isPlayerNear = true;
            UpdateDynamicUI();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // プレイヤーが範囲から出たか判定
        if (other.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            _isPlayerNear = false;
            UpdateDynamicUI();
        }
    }

    private void CreateDynamicUI()
    {
        // 1. Canvasの生成
        GameObject canvasGo = new GameObject("GramophoneDynamicUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _dynamicCanvas = canvasGo.GetComponent<Canvas>();
        _dynamicCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _dynamicCanvas.sortingOrder = 31000;
        _dynamicCanvas.targetDisplay = 3; // デフォルト Display 4 (インデックス3)

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Prompt Text
        GameObject promptGo = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        promptGo.transform.SetParent(canvasGo.transform, false);
        _promptText = promptGo.GetComponent<TextMeshProUGUI>();
        _promptText.fontSize = 28;
        _promptText.alignment = TextAlignmentOptions.Center;
        _promptText.color = Color.white;
        _promptText.text = "[F] Next Track\n[Q] Stop Music";
        
        RectTransform promptRt = promptGo.GetComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(0.5f, 0f);
        promptRt.anchorMax = new Vector2(0.5f, 0f);
        promptRt.pivot = new Vector2(0.5f, 0f);
        promptRt.anchoredPosition = new Vector2(0f, 120f);
        promptRt.sizeDelta = new Vector2(600f, 120f); // 2行分表示するため高さを拡張

        var shadow = promptGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2f, -2f);

        _dynamicCanvas.gameObject.SetActive(false);
    }

    private void UpdateDynamicUI()
    {
        if (_dynamicCanvas != null)
        {
            _dynamicCanvas.gameObject.SetActive(_isPlayerNear);
        }
    }

    private void OnDestroy()
    {
        if (_dynamicCanvas != null)
        {
            Destroy(_dynamicCanvas.gameObject);
        }
    }
}
