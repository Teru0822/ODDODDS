using UnityEngine;

[System.Serializable]
public class DevilClawData : MonoBehaviour
{
    [Header("爪の各パーツ（指）を指定する場合")]
    [Tooltip("このアームの爪パーツ（finger.001〜.004）をインスペクターで登録できます。空欄の場合は自動的に子オブジェクトが登録されます。")]
    public Transform[] fingerParts;

    [Header("カスタム開閉設定（UFOArmControllerと同じ項目です）")]
    public float fingerOpenAngle = 40f;
    public bool[] invertFingerAngle;
    public Vector3[] fingerAngleOffsets;

    [Header("完全にカスタムな位置・角度を使う場合")]
    public bool useCustomOpenTransform;
    public Vector3[] customOpenLocalPositions;
    public Vector3[] customOpenLocalRotations;
}
