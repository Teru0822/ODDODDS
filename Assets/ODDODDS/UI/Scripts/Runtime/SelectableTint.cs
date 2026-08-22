using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>Color Tint の当て方。部品の地の色が黒か白かで使い分ける。</summary>
    public enum TintMode
    {
        /// <summary>黒地の部品（ボタン・タブ・ドロップダウン本体）</summary>
        Dark = 0,
        /// <summary>白いつまみ（スライダー・スクロールバー）</summary>
        Handle,
        /// <summary>ドロップダウンの選択項目</summary>
        DropdownItem,
    }

    /// <summary>
    /// この Selectable にどの Color Tint を当てるかの指定。
    /// 付いていない場合は型から自動判定される（Slider / Scrollbar は Handle、それ以外は Dark）。
    /// 自動判定で意図と違うときだけ付ければよい。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public class SelectableTint : MonoBehaviour
    {
        public TintMode mode = TintMode.Dark;

        /// <summary>コンポーネントが無い場合の既定値を型から決める。</summary>
        public static TintMode Resolve(Selectable selectable)
        {
            var explicitTint = selectable.GetComponent<SelectableTint>();
            if (explicitTint != null) return explicitTint.mode;

            return selectable is Slider or Scrollbar ? TintMode.Handle : TintMode.Dark;
        }
    }
}
