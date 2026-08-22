using UnityEngine;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 「この要素の位置とサイズは手で決めたので、ツールに触らせない」という目印。
    ///
    /// リスキンを実行しても、これが付いている GameObject の RectTransform は変更されない。
    /// 色やフォントは引き続きテーマに追従する（見た目の統一は保ったまま、配置だけ自分で決められる）。
    ///
    /// 付け方: 対象を選んで Add Component、または
    /// メニューの「ODD ODDS / UI / 選択中のUIを手動配置にする」。
    /// </summary>
    [DisallowMultipleComponent]
    public class KeepManualLayout : MonoBehaviour
    {
        [Tooltip("色やフォントの適用も含めて、リスキンの対象から完全に外す")]
        public bool excludeFromRestyleEntirely = false;

        /// <summary>この GameObject（または先祖）が配置の手動管理を宣言しているか。</summary>
        public static bool IsLocked(Component c)
            => c != null && c.GetComponentInParent<KeepManualLayout>(true) != null;

        /// <summary>リスキンから完全に除外されているか。</summary>
        public static bool IsFullyExcluded(Component c)
        {
            if (c == null) return false;
            var marker = c.GetComponentInParent<KeepManualLayout>(true);
            return marker != null && marker.excludeFromRestyleEntirely;
        }
    }
}
