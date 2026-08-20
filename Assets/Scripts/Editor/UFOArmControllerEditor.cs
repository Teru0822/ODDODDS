using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(UFOArmController))]
public class UFOArmControllerEditor : Editor
{
    // マウスで画面上の大きさを調整できるハンドル機能を用意
    private BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();

    // Sceneビューでの描画・操作イベント
    private void OnSceneGUI()
    {
        UFOArmController arm = (UFOArmController)target;

        // 基準座標の決定
        Vector3 basePos = arm.transform.position;
        if (arm.machineRoot != null)
        {
            basePos = arm.machineRoot.position;
        }

        // レバー等のプレイ中ではない時の高さ基準として少し浮かせる（あればアームルートの高さに合わせる）
        float y = (arm.armRoot != null) ? arm.armRoot.position.y : basePos.y;
        Vector3 center = new Vector3(basePos.x + arm.playAreaCenter.x, y, basePos.z + arm.playAreaCenter.y);
        Vector3 size = new Vector3(arm.playAreaSize.x, 0.1f, arm.playAreaSize.y);

        _boundsHandle.center = center;
        _boundsHandle.size = size;

        // ハンドルの色を視認しやすい赤に設定
        Handles.color = Color.red;

        // 変更を監視開始
        EditorGUI.BeginChangeCheck();
        
        // ハンドルをSceneビューに描画＆操作可能にする
        _boundsHandle.DrawHandle();
        
        // ユーザーがハンドルをドラッグしてサイズや位置を変えたら
        if (EditorGUI.EndChangeCheck())
        {
            // Ctrl+Z（Undo）に対応させる
            Undo.RecordObject(arm, "Change UFO Arm Play Area");
            
            // 変更された数値を本体のControllerに適用（ベースからの相対的なズレとして保存）
            arm.playAreaCenter = new Vector2(_boundsHandle.center.x - basePos.x, _boundsHandle.center.z - basePos.z);
            arm.playAreaSize = new Vector2(_boundsHandle.size.x, _boundsHandle.size.z);
        }
    }
}
