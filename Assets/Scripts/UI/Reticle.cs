using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面中央に表示するレティクル（照準）。
/// 任意の GameObject にアタッチすると Start で自動的に Canvas + Image を生成する。
/// targetCanvas を指定した場合はそこへ追加する（自動 Canvas は生成しない）。
/// </summary>
[DisallowMultipleComponent]
public class Reticle : MonoBehaviour
{
    public enum ReticleShape { Dot, Cross, Plus }

    [Header("外観")]
    [Tooltip("レティクル形状")]
    public ReticleShape shape = ReticleShape.Plus;

    [Tooltip("色（α が 0 だと見えないので注意）")]
    public Color color = new Color(1f, 1f, 1f, 0.9f);

    [Header("Dot 設定")]
    [Tooltip("中央ドットのサイズ (px)")]
    public float dotSize = 6f;

    [Header("Cross / Plus 設定")]
    [Tooltip("各腕の長さ (px)")]
    public float armLength = 12f;

    [Tooltip("各腕の太さ (px)")]
    public float armThickness = 2f;

    [Tooltip("中央の隙間 (px)。Plus 形状でのみ有効")]
    public float centerGap = 4f;

    [Tooltip("Plus 形状の時に中央にもドットを置く")]
    public bool plusWithCenterDot = false;

    [Header("Canvas 設定")]
    [Tooltip("追加先 Canvas (null なら新規 Canvas を自動生成)")]
    public Canvas targetCanvas;

    [Tooltip("表示するディスプレイ番号 (0=Display 1, 1=Display 2, 2=Display 3, 3=Display 4)")]
    [Range(0, 7)]
    public int targetDisplay = 3;

    [Tooltip("Sorting Order (UI 重ね順 / 既存 UI より大きい値にする)")]
    public int sortingOrder = 32000;

    [Tooltip("シーン内の他 Canvas を走査し、その最大 sortingOrder より +1 を自動採用する")]
    public bool autoBringToFront = true;

    [Tooltip("自動生成した Canvas をシーン遷移後も残す")]
    public bool persistAcrossScenes = false;

    [Header("デバッグ")]
    [Tooltip("起動時の生成内容を Console に出力")]
    public bool logOnStart = true;

    private GameObject _autoCanvas;
    private GameObject _reticleContainer;
    private Sprite _sprite;

    private void Start()
    {
        CreateReticle();
    }

    private void OnDestroy()
    {
        if (_autoCanvas != null) Destroy(_autoCanvas);
        else if (_reticleContainer != null) Destroy(_reticleContainer);
    }

    private void CreateReticle()
    {
        _sprite = CreateWhiteSprite();

        Canvas canvas = targetCanvas;
        if (canvas == null)
        {
            // 自動生成: RectTransform + Canvas + Scaler を明示的に追加
            _autoCanvas = new GameObject(
                "ReticleCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            if (persistAcrossScenes) DontDestroyOnLoad(_autoCanvas);
            canvas = _autoCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ResolveSortingOrder();
            canvas.targetDisplay = targetDisplay;
            var scaler = _autoCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
        }

        _reticleContainer = new GameObject("Reticle", typeof(RectTransform));
        _reticleContainer.transform.SetParent(canvas.transform, false);
        var crt = _reticleContainer.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;

        int partCount = 0;
        switch (shape)
        {
            case ReticleShape.Dot:
                AddPart(crt, Vector2.zero, new Vector2(dotSize, dotSize));
                partCount = 1;
                break;

            case ReticleShape.Cross:
                AddPart(crt, Vector2.zero, new Vector2(armLength * 2f, armThickness));
                AddPart(crt, Vector2.zero, new Vector2(armThickness, armLength * 2f));
                partCount = 2;
                break;

            case ReticleShape.Plus:
                float half = centerGap * 0.5f + armLength * 0.5f;
                AddPart(crt, new Vector2(0f, half), new Vector2(armThickness, armLength));
                AddPart(crt, new Vector2(0f, -half), new Vector2(armThickness, armLength));
                AddPart(crt, new Vector2(half, 0f), new Vector2(armLength, armThickness));
                AddPart(crt, new Vector2(-half, 0f), new Vector2(armLength, armThickness));
                partCount = 4;
                if (plusWithCenterDot)
                {
                    AddPart(crt, Vector2.zero, new Vector2(dotSize, dotSize));
                    partCount = 5;
                }
                break;
        }

        if (logOnStart)
        {
            Debug.Log($"[Reticle] 生成完了: shape={shape}, parts={partCount}, " +
                      $"canvas='{canvas.name}' (sortingOrder={canvas.sortingOrder}, " +
                      $"targetDisplay={canvas.targetDisplay} (= Display {canvas.targetDisplay + 1}), " +
                      $"autoBringToFront={autoBringToFront}), color={color}", this);
        }
    }

    private void AddPart(RectTransform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject("Part", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = _sprite;
        img.color = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private void Update()
    {
        // プレイヤーが自分で照準できない演出・操作中はレティクルを隠す。
        // どのフラグも演出の終了時に false へ戻るので、終わればそのまま復帰する
        bool showReticle = true;
        if (UFOCameraController.IsPlayingUfo
            || RewardSelectionUI.IsTypewriterUIShowing
            || App.ATM.ATMController.IsInteracting          // ATM 操作中
            || VisitorSystem.IsTalkingWithVisitor           // インターホンで訪問者と対応中
            || DebtCollectionManager.IsCollecting          // 悪魔の取り立て中
            || BookOpenController.IsBookVisible            // 本を開いている間
            || SettingUIManager.IsMenuOpen                 // Escape で設定画面を開いている間
            || TypewriterUsageGate.IsPanelShowing          // 使用ゲートの忠告パネル表示中
            || MiniGames.Transitions.LoadingScreenManager.IsShowing) // ロード画面表示中
        {
            showReticle = false;
        }

        GameObject targetGo = _autoCanvas != null ? _autoCanvas : _reticleContainer;
        if (targetGo != null && targetGo.activeSelf != showReticle)
        {
            targetGo.SetActive(showReticle);
        }
    }

    private int ResolveSortingOrder()
    {
        if (!autoBringToFront) return sortingOrder;
        int maxOrder = sortingOrder;
        var all = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c == null) continue;
            if (_autoCanvas != null && c.gameObject == _autoCanvas) continue;
            if (c.sortingOrder >= maxOrder) maxOrder = c.sortingOrder + 1;
        }
        return maxOrder;
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
    }
}
