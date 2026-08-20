using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Devilキャッチャーの機体にある引き出しの中で、獲得したアイテム（金/銀/銅コイン・黒ダイヤ）を見せる演出。
/// 4つの枠は常に固定で、各枠は「0個なら空、1個以上なら実物を上限数まで積んで残りは数字表示」という
/// 同一ルールを独立に適用するだけで、獲得パターンの組み合わせを個別に作り込む必要はない。
///
/// プレイ中、アイテムを1つ獲得するたびに AddItem() を呼ぶことで、その枠の決まった1箇所（slotTransform）
/// から実物が1つずつ積み上がっていく。セッション終了時に OpenDrawer() を呼ぶと、それまでに積み上がった
/// 中身をそのまま引き出しごと見せる。
/// </summary>
public class DevilDrawerRewardDisplay : MonoBehaviour
{
    public static DevilDrawerRewardDisplay Instance { get; private set; }

    [Header("練習機設定")]
    [Tooltip("ON: 練習用Devilキャッチャー（Practice_Cranegame）側のDevilDrawerRewardDisplayであることを示す。\n" +
             "実機・練習機は同じPrefab（new_devilcatcher 1）内のコンポーネントのため両方が有効な状態で存在してしまう。\n" +
             "練習機側インスタンスではこれをONにして、static Instanceを実機側から奪わないようにする")]
    [SerializeField] private bool isPracticeInstance = false;

    public enum RewardItemType { Gold, Silver, Bronze, BlackDiamond }

    [System.Serializable]
    private class RewardSlot
    {
        [Tooltip("このアイテムの定義（Prefabを使います）")]
        public ItemData itemData;
        [Tooltip("実物が湧いて出てくる基準位置（1箇所）。ここから積み上がっていきます")]
        public Transform slotTransform;
        [Tooltip("1個ごとに積み上げるオフセット（XYZ指定）。例: (0, 0.02, 0) なら真上に積む")]
        public Vector3 itemStackOffset = new Vector3(0f, 0.02f, 0f);
        [Tooltip("この個数だけ積んだら、次の列へ折り返す")]
        public int itemsPerRow = 4;
        [Tooltip("列が変わるごとに加算するオフセット（XYZ指定）。例: (0.05, 0, 0) なら横にズレていく")]
        public Vector3 rowOffset = new Vector3(0.05f, 0f, 0f);
        [Tooltip("生成時の向き（オイラー角）。このアイテムを置きたい向きに合わせて指定してください")]
        public Vector3 itemRotationEuler;
        [Tooltip("上記の向きを基準に、縦軸(Y軸)回りだけランダムに回転させて少しばらけさせるか")]
        public bool randomizeYawOnly = false;
        [Tooltip("枚数を表示するテキスト（未設定でも動作します）")]
        public TextMeshProUGUI countText;

        [System.NonSerialized] public List<GameObject> spawnedInstances = new List<GameObject>();
        [System.NonSerialized] public int count = 0;
    }

    [Header("4つの固定枠（金・銀・銅・黒ダイヤ）")]
    [SerializeField] private RewardSlot goldSlot;
    [SerializeField] private RewardSlot silverSlot;
    [SerializeField] private RewardSlot bronzeSlot;
    [SerializeField] private RewardSlot blackDiamondSlot;

    [Header("山積み表示設定")]
    [Tooltip("実物として並べる最大枚数。これを超えた分は数字表示のみになる")]
    [SerializeField] private int maxVisualCount = 8;
    [Tooltip("並べる実物の大きさの倍率（元のプレハブのスケールに対する倍率）")]
    [SerializeField] private float pileItemScale = 1f;

    [Header("引き出しの開閉")]
    [Tooltip("実際に動かす引き出しのTransform")]
    [SerializeField] private Transform drawerTransform;
    [Tooltip("閉じた状態のローカル座標からの開く距離（引き出し自身のローカルZ軸方向など）")]
    [SerializeField] private Vector3 drawerOpenLocalOffset = new Vector3(0f, 0f, 0.5f);
    [SerializeField] private float drawerMoveDuration = 1.0f;
    [Tooltip("開いた状態を何秒間見せ続けるか")]
    [SerializeField] private float drawerHoldSeconds = 4.0f;

    [Header("照明")]
    [Tooltip("引き出しを照らすライト。開くタイミングで点灯し、閉まると同時に消灯します")]
    [SerializeField] private Light drawerLight;

    [Header("カメラ")]
    [Tooltip("引き出しを見せる位置・向き（このTransformの位置・回転へカメラをLerpで遷移させます）")]
    [SerializeField] private Transform drawerCameraViewpoint;
    [Tooltip("カメラが移動する時間（秒）")]
    [SerializeField] private float cameraTransitionDuration = 1.0f;
    [Tooltip("カメラ移動の緩急カーブ")]
    [SerializeField] private AnimationCurve cameraTransitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>引き出しが開く～見せる～閉じるまでの一連の演出中かどうか</summary>
    public bool IsShowing => _drawerRoutine != null;

    private Vector3 _drawerClosedLocalPos;
    private bool _drawerClosedPosCaptured = false;
    private Coroutine _drawerRoutine;

    private void Awake()
    {
        // 練習機側（Practice_Cranegame）のインスタンスはstatic Instanceを絶対に奪わないようにする
        if (!isPracticeInstance)
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[DevilDrawerRewardDisplay] Multiple non-practice instances of DevilDrawerRewardDisplay detected in the scene.");
            }
        }

        if (drawerTransform != null)
        {
            _drawerClosedLocalPos = drawerTransform.localPosition;
            _drawerClosedPosCaptured = true;
        }

        if (drawerLight != null) drawerLight.enabled = false;
    }

    /// <summary>
    /// プレイ中、アイテムを1つ獲得するたびに呼ぶ。対応する枠の決まった位置に実物を1つ積み増す。
    /// </summary>
    public void AddItem(RewardItemType type)
    {
        RewardSlot slot = GetSlot(type);
        if (slot == null) return;

        slot.count++;

        if (slot.slotTransform != null && slot.itemData != null && slot.itemData.prefabData != null
            && slot.spawnedInstances.Count < maxVisualCount)
        {
            int index = slot.spawnedInstances.Count;
            int itemsPerRow = Mathf.Max(1, slot.itemsPerRow);
            int col = index % itemsPerRow;
            int row = index / itemsPerRow;

            Vector3 spawnPos = slot.slotTransform.position
                + slot.itemStackOffset * col
                + slot.rowOffset * row;

            Quaternion rotation = Quaternion.Euler(slot.itemRotationEuler);
            if (slot.randomizeYawOnly)
            {
                rotation = Quaternion.Euler(slot.itemRotationEuler.x, Random.Range(0f, 360f), slot.itemRotationEuler.z);
            }

            GameObject instance = Instantiate(slot.itemData.prefabData, spawnPos, rotation, slot.slotTransform);
            instance.transform.localScale *= pileItemScale;

            // 見た目だけの演出なので、物理・当たり判定は不要
            foreach (var itemCollider in instance.GetComponentsInChildren<Collider>())
            {
                itemCollider.enabled = false;
            }
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            slot.spawnedInstances.Add(instance);
        }

        if (slot.countText != null)
        {
            slot.countText.gameObject.SetActive(true);
            slot.countText.text = $"×{slot.count}";
        }
    }

    private RewardSlot GetSlot(RewardItemType type)
    {
        switch (type)
        {
            case RewardItemType.Gold: return goldSlot;
            case RewardItemType.Silver: return silverSlot;
            case RewardItemType.Bronze: return bronzeSlot;
            case RewardItemType.BlackDiamond: return blackDiamondSlot;
            default: return null;
        }
    }

    /// <summary>
    /// Devilキャッチャー終了時に呼ぶ。プレイ中に積み上がった中身をそのまま見せるため、引き出しを開くだけ。
    /// </summary>
    public void OpenDrawer()
    {
        if (_drawerRoutine != null) StopCoroutine(_drawerRoutine);
        _drawerRoutine = StartCoroutine(DrawerRoutine());
    }

    /// <summary>
    /// 新しいセッション開始時に呼ぶ。前回分の中身（実物・カウント）を空にする。
    /// </summary>
    public void ResetContents()
    {
        ClearSlot(goldSlot);
        ClearSlot(silverSlot);
        ClearSlot(bronzeSlot);
        ClearSlot(blackDiamondSlot);
    }

    private void ClearSlot(RewardSlot slot)
    {
        if (slot == null) return;

        foreach (var obj in slot.spawnedInstances)
        {
            if (obj != null) Destroy(obj);
        }
        slot.spawnedInstances.Clear();
        slot.count = 0;

        if (slot.countText != null) slot.countText.gameObject.SetActive(false);
    }

    private IEnumerator DrawerRoutine()
    {
        // ※ここから先、参照未設定の項目があっても必ず末尾の後片付け（フォーカス解除・カメラ復元・
        //   _drawerRoutineのリセット）まで到達するようにする。途中でyield breakすると
        //   フォーカスモードがONのまま戻らなくなる事故につながるため。

        if (drawerLight != null) drawerLight.enabled = true;

        // 引き出しを見せるためにカメラがスラープ（移動）し始めるこのタイミングで、
        // クレーンゲーム本体のライトを消す（ライト消灯音もSetPlaySpotlight側で再生される）
        UFOCameraController.Instance?.SetPlaySpotlight(false);

        // 今アクティブなDevilカメラ（frontCamera）自体を、引き出しを見せる位置までLerpで動かす。
        // ※この時点では既に IsPlayingUfo が false になっているため GetActiveCamera() ではなく
        //   FrontCamera を直接参照する（実際に有効なままなのは frontCamera のため）
        Camera cam = UFOCameraController.Instance != null ? UFOCameraController.Instance.FrontCamera : null;
        Vector3 originalCamPos = default;
        Quaternion originalCamRot = default;
        bool cameraMoved = false;

        if (cam != null && drawerCameraViewpoint != null)
        {
            originalCamPos = cam.transform.position;
            originalCamRot = cam.transform.rotation;
            cameraMoved = true;

            yield return MoveCamera(cam.transform, originalCamPos, originalCamRot, drawerCameraViewpoint.position, drawerCameraViewpoint.rotation);
        }

        // カメラがViewpointに到着したこのタイミングで、UnwashCoinのカウントアップを開始する
        // （フォーカス表示自体は GameUIManager.HandleDevilModeChanged 側で既に適用済み）
        // ※カウントアップ（DOTween）は独立して動くため、待たずにそのまま引き出しの開閉へ進み、
        //   カウントアップと引き出しアニメーションが同時に進行するようにする
        DevilItemGoal.Instance?.FinalizeSessionRewards();

        if (drawerTransform != null && _drawerClosedPosCaptured)
        {
            Vector3 openPos = _drawerClosedLocalPos + drawerOpenLocalOffset;
            DevilSEManager.Instance?.PlayDrawerOpen();
            yield return MoveDrawer(_drawerClosedLocalPos, openPos);
            yield return new WaitForSeconds(drawerHoldSeconds);
            DevilSEManager.Instance?.PlayDrawerClose();
            yield return MoveDrawer(openPos, _drawerClosedLocalPos);
        }
        else
        {
            Debug.LogWarning("[DevilDrawerRewardDisplay] Drawer Transform が未設定のため、引き出しの開閉演出をスキップしました。");
            yield return new WaitForSeconds(drawerHoldSeconds);
        }

        if (drawerLight != null) drawerLight.enabled = false;

        // UnwashCoinのフォーカス表示を終了し、通常のUI表示に戻す
        GameUIManager.Instance?.SetRewardFocusMode(false);

        // 元の位置・向きへカメラを戻す
        if (cameraMoved && cam != null)
        {
            yield return MoveCamera(cam.transform, cam.transform.position, cam.transform.rotation, originalCamPos, originalCamRot);
        }

        _drawerRoutine = null;
    }

    private IEnumerator MoveCamera(Transform cam, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
    {
        float elapsed = 0f;
        while (elapsed < cameraTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = cameraTransitionEase.Evaluate(Mathf.Clamp01(elapsed / cameraTransitionDuration));
            cam.position = Vector3.Lerp(fromPos, toPos, t);
            cam.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }
        cam.position = toPos;
        cam.rotation = toRot;
    }

    private IEnumerator MoveDrawer(Vector3 from, Vector3 to)
    {
        float elapsed = 0f;
        while (elapsed < drawerMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / drawerMoveDuration);
            drawerTransform.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }
        drawerTransform.localPosition = to;
    }
}
