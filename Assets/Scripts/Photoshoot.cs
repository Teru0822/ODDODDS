using UnityEngine;

// 撮影用シーンに配置するコンポーネント
public class IconPhotographer : MonoBehaviour
{
    [Header("撮影設定")]
    [Tooltip("撮影に使う、背景をSolid Color(Alpha 0)にしたカメラ")]
    public Camera targetCamera;

    [Tooltip("出力画像の解像度 (512などがUIにはおすすめ)")]
    public int width = 512;
    public int height = 512;

    [Tooltip("保存先のフォルダ (Assets/ から始める)")]
    public string folderPath = "Assets/UI/Icons";

    [Tooltip("保存するファイル名 (末尾に自動で日時が付きます)")]
    public string fileName = "DiamondIcon";
}