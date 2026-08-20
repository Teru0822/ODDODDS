/// <summary>
/// LeverController / ButtonController が「今操作を受け付けてよいか」「どのカメラを基準にするか」を
/// 問い合わせるための差し替え可能な制御元。
/// 未設定の場合は UFOCameraController の静的な状態（実機側）を参照する（今まで通りの挙動）。
/// チュートリアル用の練習Devilキャッチャーなど、実機とは独立したセッションを制御する際に実装して使う。
/// </summary>
public interface ICraneControlSource
{
    /// <summary>このセッションが現在プレイ中かどうか</summary>
    bool IsPlayingCrane { get; }

    /// <summary>レバー/ボタン操作を受け付けてよい状態かどうか</summary>
    bool IsControlActive { get; }

    /// <summary>指定した種別のボタン操作を受け付けてよい状態かどうか（ボタン種別ごとに個別に許可/禁止したいステップ用）</summary>
    bool IsButtonTypeActive(ButtonController.ButtonType buttonType);

    /// <summary>レイキャストやレバーの向き計算の基準にするカメラ</summary>
    UnityEngine.Camera GetActiveCamera();

    /// <summary>レバー/ボタンが実際に操作された瞬間に呼ばれる</summary>
    void NotifyControlInputUsed();

    /// <summary>指定した種別のボタンが実際に押された瞬間に呼ばれる（NotifyControlInputUsedとは別に、
    /// ボタン種別込みで通知したいステップ制御側が使う。連打防止のロックを同フレームで即座にかけるため）</summary>
    void NotifyButtonPressed(ButtonController.ButtonType buttonType);
}
