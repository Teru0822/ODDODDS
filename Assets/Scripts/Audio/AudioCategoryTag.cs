using UnityEngine;

namespace App.Audio
{
    /// <summary>音量設定のどのつまみで音量を変えるか。</summary>
    public enum AudioCategory
    {
        /// <summary>BGM。曲・環境音</summary>
        Bgm = 0,
        /// <summary>効果音。操作音・演出音</summary>
        Se,
        /// <summary>ボイス。セリフ</summary>
        Voice,
        /// <summary>設定の影響を受けない。UI の試聴音など、常に一定にしたいもの</summary>
        Unmanaged,
    }

    /// <summary>
    /// この AudioSource がどの音量つまみに属するかを指定する。
    ///
    /// AudioSource と同じ GameObject に付けると、設定画面の BGM / SE / ボイスの
    /// どれで音量が変わるかを決められる。
    /// 付けなかった場合は AudioVolumeController の既定分類が使われる。
    ///
    /// 親に付けておけば、その配下の AudioSource すべてに効く（個別に付いていればそちらが優先）。
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioCategoryTag : MonoBehaviour
    {
        [Tooltip("この音がどのつまみで変わるか")]
        public AudioCategory category = AudioCategory.Se;

        [Tooltip("配下の AudioSource にもこの分類を適用する。" +
                 "個別に AudioCategoryTag が付いているものはそちらが優先される")]
        public bool applyToChildren = true;
    }
}
