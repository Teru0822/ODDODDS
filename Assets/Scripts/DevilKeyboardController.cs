using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Devilキャッチャーをキーボードで操作するためのコントローラー。
/// 1でStart Descent（下降開始）、2でToggle Claw（爪の手動開閉）を行う。
///
/// W/S/A/D によるアーム移動は、LeverController（レバーのマウス操作）側に統合されている
/// （レバーの見た目もキーボード操作に連動して倒れる）。このスクリプトは物理ボタン（1/2）の
/// キーボード版のみを担当する。
///
/// マウス操作との競合防止:
/// - 1/2キーは、対応する ButtonController の TriggerPress()/TriggerRelease() を直接呼ぶことで、
///   マウスクリック時と全く同じ押し込み演出・効果音・動作になるようにしている（ロジックの二重化を避ける）。
/// - StartDescentCycle() / ToggleClaw() は UFOArmController 側の IsInputLocked による排他制御が
///   そのまま効くため、マウスのボタンクリックと同時に押されても二重発火しない。
/// </summary>
public class DevilKeyboardController : MonoBehaviour
{
    [Header("連携（Start Descentボタン）")]
    [Tooltip("ButtonType = StartDescent が設定されている ButtonController")]
    public ButtonController startDescentButton;

    [Header("連携（Toggle Clawボタン）")]
    [Tooltip("ButtonType = ToggleClaw が設定されている ButtonController")]
    public ButtonController toggleClawButton;

    void Update()
    {
        // 実機/チュートリアルどちらのプレイ許可判定も ButtonController.TriggerPress() 側が
        // 自分のcontrolSourceOverride経由で正しく行うため、ここでは実機限定の判定はしない
        // （ここで実機のUFOCameraControllerだけを見てしまうと、チュートリアル中はキー操作が
        // 一切効かなくなる）。
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 下降開始 (1)
        if (startDescentButton != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) startDescentButton.TriggerPress();
            if (keyboard.digit1Key.wasReleasedThisFrame) startDescentButton.TriggerRelease();
        }

        // 爪の手動開閉 (2)
        if (toggleClawButton != null)
        {
            if (keyboard.digit2Key.wasPressedThisFrame) toggleClawButton.TriggerPress();
            if (keyboard.digit2Key.wasReleasedThisFrame) toggleClawButton.TriggerRelease();
        }
    }
}
