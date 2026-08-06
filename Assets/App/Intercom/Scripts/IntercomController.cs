using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// インターホンの操作シーケンスを管理するクラス (Issue #6)。
/// </summary>
public class IntercomController : MonoBehaviour
{
    public enum IntercomState { Idle, Calling, Talking, Trade}

    [Header("Settings")]
    [SerializeField] private ReactiveProperty<IntercomState> currentState = new ReactiveProperty<IntercomState>(IntercomState.Idle);
    [SerializeField] private Color lampColor = Color.green;
    [SerializeField] private float blinkSpeed = 2.0f;
    [SerializeField] private string emissionKeyword = "_EmissionColor";

    [Header("References")]
    [SerializeField] private Camera clickCamera;            // クリック判定に使用するカメラ
    [SerializeField] private GameObject displayObject;      // 画面のオブジェクト
    [SerializeField] private RenderTexture displayTexture;   // 玄関カメラのRenderTexture
    [SerializeField] private Renderer lampRenderer;        // ランプのRenderer
    [SerializeField] private AudioClip callingClip;        // チャイム音
    [SerializeField] private GameObject entranceCamera;   // 玄関カメラ（オプション・負荷軽減用）

    private Material lampMaterial;
    private Material runtimeDisplayMaterial;
    private AudioSource audioSource;

    //公開
    public IntercomState CurrentState
    {
        get{return currentState.Value;}
        set{currentState.Value = value;}
    }
    public IObservable<IntercomState> OnChangeCurrentState{get{return currentState;}}

    private bool _isCanPushIdle = false;
    public bool IsCanPushIdle{get{return _isCanPushIdle;} set{_isCanPushIdle = value;}}

    private void Start()
    {
        //カメラのアタッチ
        clickCamera = Camera.main;

        // 再生用 AudioSource の準備
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = callingClip;
        audioSource.loop = true;

        if (lampRenderer != null) lampMaterial = lampRenderer.material;

        // 画面マテリアルの生成とテクスチャ割り当て（URP対応）
        if (displayObject != null)
        {
            var r = displayObject.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                runtimeDisplayMaterial = new Material(shader);
                runtimeDisplayMaterial.name = "Intercom_Runtime_Material";
                
                if (displayTexture != null)
                {
                    runtimeDisplayMaterial.SetTexture("_BaseMap", displayTexture);
                    runtimeDisplayMaterial.mainTexture = displayTexture;
                    if (runtimeDisplayMaterial.HasProperty("_EmissionMap")) 
                        runtimeDisplayMaterial.SetTexture("_EmissionMap", displayTexture);
                }
                
                runtimeDisplayMaterial.color = Color.white;
                r.material = runtimeDisplayMaterial;
            }
        }
        
        UpdateState(IntercomState.Idle);
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            Debug.LogError("マウスがありません。");
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }

        // 着信中のランプ点滅
        if (currentState.Value == IntercomState.Calling)
        {
            bool blink = (Mathf.FloorToInt(Time.time * blinkSpeed) % 2 == 0);
            SetLampEmission(blink);
        }
    }

    private void HandleClick()
    {   
        if(clickCamera == null) clickCamera = Camera.main;

        Camera targetCam = clickCamera;
        if (targetCam == null)
        {
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy) { targetCam = cam; break; }
            }
        }
        if (targetCam == null) return;

        Ray ray = targetCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        foreach (var hit in hits)
        {
            IntercomButton btn = hit.collider.GetComponentInParent<IntercomButton>();
            if (btn == null) btn = hit.collider.GetComponent<IntercomButton>();
            if (btn == null) btn = hit.collider.GetComponentInChildren<IntercomButton>();

            if (btn != null)
            {
                btn.OnClick();
                return; 
            }
        }
    }

    public void UpdateState(IntercomState newState)
    {
        currentState.Value = newState;

        switch (currentState.Value)
        {
            case IntercomState.Idle:
                // 画面を消さずに黒くする
                if (displayObject != null) displayObject.SetActive(true);
                SetDisplayVisual(false);
                if (entranceCamera != null) entranceCamera.SetActive(false);
                SetLampEmission(false);
                if (audioSource != null) audioSource.Stop();
                break;

            case IntercomState.Calling:
                if (displayObject != null) displayObject.SetActive(true);
                if (entranceCamera != null) entranceCamera.SetActive(true);
                SetDisplayVisual(true);
                if (audioSource != null && !audioSource.isPlaying) audioSource.Play();
                break;

            case IntercomState.Talking:
                if (displayObject != null) displayObject.SetActive(true);
                if (entranceCamera != null) entranceCamera.SetActive(true);
                SetDisplayVisual(true);
                SetLampEmission(true);
                if (audioSource != null) audioSource.Stop();
                break;
        }
    }

    private void SetDisplayVisual(bool active)
    {
        if (runtimeDisplayMaterial != null)
        {
            // 基本色を白/黒に切り替えることで電源ON/OFFを表現
            Color targetColor = active ? Color.white : Color.black;
            runtimeDisplayMaterial.color = targetColor;
            
            // URP のプロパティ名にも一応セット
            if (runtimeDisplayMaterial.HasProperty("_BaseColor"))
                runtimeDisplayMaterial.SetColor("_BaseColor", targetColor);

            // 映像を表示させるための発光設定
            if (active)
            {
                // 白飛びしない程度の控えめな発光
                runtimeDisplayMaterial.SetColor("_EmissionColor", new Color(0.2f, 0.2f, 0.2f));
                runtimeDisplayMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                runtimeDisplayMaterial.SetColor("_EmissionColor", Color.black);
                runtimeDisplayMaterial.DisableKeyword("_EMISSION");
            }
        }
    }


    private void SetLampEmission(bool active)
    {
        if (lampMaterial != null)
        {
            if (active)
            {
                lampMaterial.SetColor(emissionKeyword, lampColor);
                lampMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                lampMaterial.SetColor(emissionKeyword, Color.black);
                lampMaterial.DisableKeyword("_EMISSION");
            }
        }
    }

    public void OnClickCentralButton() { if (currentState.Value == IntercomState.Calling) UpdateState(IntercomState.Talking); }
    public void OnClickRightButton() { if (currentState.Value != IntercomState.Idle && _isCanPushIdle) UpdateState(IntercomState.Idle); }
    public void OnClickLeftButton() { if (currentState.Value == IntercomState.Talking) UpdateState(IntercomState.Trade);/* 開錠ロジック用（将来） */ }
}
