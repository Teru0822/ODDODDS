using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 開いたドロップダウンのリストを、項目が全部見える高さに整える。
    ///
    /// 開いた時のリストの大きさは TMP_Dropdown が実行時に計算する。
    ///   Content の高さ = 項目の高さ × 項目数
    ///   リストの高さ   = Template の高さ から余った分を引いたもの
    /// 計算どおりなら項目数ぶんの高さになるはずだが、実際には
    ///   ・リストが ScrollView の Mask に切られる
    ///   ・Template の高さが項目数ぶんに足りない
    /// といった理由で途中までしか見えないことがある。
    ///
    /// このコンポーネントは開いた直後にリストを測り直し、
    /// 必要なら高さを詰め直したうえで、マスクの外へ逃がす。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Dropdown))]
    public class DropdownListFitter : MonoBehaviour
    {
        private const string ListName = "Dropdown List";

        [Tooltip("項目が全部見える高さに詰め直す")]
        [SerializeField] private bool _fitToItems = true;

        [Tooltip("高さの上限。これを超える項目数ならスクロールになる。0 で上限なし")]
        [SerializeField] private float _maxHeight = 0f;

        [Tooltip("リストを一番手前の Canvas 直下へ移し、ScrollView のマスクに切られないようにする")]
        [SerializeField] private bool _escapeMask = true;

        private TMP_Dropdown _dropdown;
        private bool _wasExpanded;

        private void Awake() => _dropdown = GetComponent<TMP_Dropdown>();

        private void Update()
        {
            if (_dropdown == null) return;

            bool expanded = _dropdown.IsExpanded;
            if (expanded && !_wasExpanded) StartCoroutine(AdjustNextFrame());
            _wasExpanded = expanded;
        }

        /// <summary>TMP がリストを組み立て終わるのを待ってから調整する。</summary>
        private IEnumerator AdjustNextFrame()
        {
            yield return null;
            Adjust();
        }

        private void Adjust()
        {
            var list = transform.Find(ListName) as RectTransform;
            if (list == null) return;

            var viewport = list.Find("Viewport") as RectTransform;
            var content = viewport != null ? viewport.Find("Content") as RectTransform : null;
            if (content == null) return;

            if (_fitToItems)
            {
                float needed = 0f;
                for (int i = 0; i < content.childCount; i++)
                {
                    var child = content.GetChild(i) as RectTransform;
                    if (child == null || !child.gameObject.activeSelf) continue;
                    needed += child.rect.height;
                }

                if (needed > 0f)
                {
                    // Viewport が List より小さい分（枠の余白）を足し戻す
                    float chrome = Mathf.Max(0f, list.rect.height - viewport.rect.height);
                    float target = needed + chrome;
                    if (_maxHeight > 0f) target = Mathf.Min(target, _maxHeight);
                    list.sizeDelta = new Vector2(list.sizeDelta.x, target);
                }
            }

            if (_escapeMask) EscapeFromMask(list);
        }

        /// <summary>
        /// ScrollView の Mask の内側にいるとリストが切り取られてしまう。
        /// 位置を保ったまま Canvas の直下へ移して、最前面に出す。
        /// </summary>
        private void EscapeFromMask(RectTransform list)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            if (list.parent == root.transform) return;

            // マスクの中に居ないなら動かす必要はない
            if (GetComponentInParent<Mask>() == null && GetComponentInParent<RectMask2D>() == null) return;

            var worldCorners = new Vector3[4];
            list.GetWorldCorners(worldCorners);

            list.SetParent(root.transform, true);
            list.SetAsLastSibling();

            // 親を変えるとアンカーの基準が変わるので、見た目の位置を戻す
            var after = new Vector3[4];
            list.GetWorldCorners(after);
            list.position += worldCorners[0] - after[0];
        }

#if UNITY_EDITOR
        /// <summary>リスキンツールからの配線用。</summary>
        public void Configure(bool fitToItems, float maxHeight, bool escapeMask)
        {
            _fitToItems = fitToItems;
            _maxHeight = maxHeight;
            _escapeMask = escapeMask;
        }
#endif
    }
}
