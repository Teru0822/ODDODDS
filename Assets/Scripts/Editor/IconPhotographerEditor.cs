using UnityEngine;
using UnityEditor;
using System.IO;
// URPの機能を使うために必要
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

[CustomEditor(typeof(IconPhotographer))]
public class IconPhotographerEditor : Editor
{
    public override void OnInspectorGUI()
        {
        // 元の設定項目（カメラや解像度）を表示
        DrawDefaultInspector();

        IconPhotographer photographer = (IconPhotographer)target;

        GUILayout.Space(15); // 少し隙間を空ける

        // インスペクター上に大きなボタンを作成
     if (GUILayout.Button("📸 透過PNGを撮影して保存", GUILayout.Height(40)))
         {
        TakeScreenshot(photographer);
        }
   }// ... (OnInspectorGUIなどは前のまま)

    private void TakeScreenshot(IconPhotographer config)
    {
        if (config.targetCamera == null) { Debug.LogError("Target camera not set!"); return; }
        if (!Directory.Exists(config.folderPath)) Directory.CreateDirectory(config.folderPath);

        // --- 1. 現在の設定を保存（撮影後に戻すため） ---
        CameraClearFlags originalFlags = config.targetCamera.clearFlags;
        Color originalColor = config.targetCamera.backgroundColor;
        Material originalSkybox = RenderSettings.skybox; // プロジェクト全体のSkyboxを保存

        // URPのカメラデータを取得
        var urpCameraData = config.targetCamera.GetComponent<UniversalAdditionalCameraData>();
        bool originalPostProcessing = false;
        if (urpCameraData != null)
        {
            originalPostProcessing = urpCameraData.renderPostProcessing;
        }

        // --- 2. 透過撮影用の設定に強制書き換え（上書き） ---
        config.targetCamera.clearFlags = CameraClearFlags.SolidColor;
        config.targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 完全透明な黒
        RenderSettings.skybox = null; // 🔥一時的に空を「なし」にする（黒上乗せの原因）

        // C#側からポストプロセスを強制OFFにする
        if (urpCameraData != null)
        {
            urpCameraData.renderPostProcessing = false;
        }

        // --- 3. 撮影処理（ここは前と同じ） ---
        RenderTexture rt = new RenderTexture(config.width, config.height, 32, RenderTextureFormat.ARGB32);
        config.targetCamera.targetTexture = rt;
        config.targetCamera.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(config.width, config.height, TextureFormat.RGBA32, false);
        screenShot.ReadPixels(new Rect(0, 0, config.width, config.height), 0, 0);
        screenShot.Apply();

        // --- 4. 後片付けと設定のリセット（ここが超重要） ---
        config.targetCamera.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);
        
        // カメラとプロジェクトの設定を元の状態に戻す
        config.targetCamera.clearFlags = originalFlags;
        config.targetCamera.backgroundColor = originalColor;
        RenderSettings.skybox = originalSkybox; // 🔥Skyboxを元に戻す

        if (urpCameraData != null)
        {
            urpCameraData.renderPostProcessing = originalPostProcessing;
        }

        // --- 5. 保存（ここは前と同じ） ---
        byte[] bytes = screenShot.EncodeToPNG();
        if (bytes == null || bytes.Length == 0) { Debug.LogError("Image generation failed."); return; }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fullPath = $"{config.folderPath}/{config.fileName}_{timestamp}.png";
        File.WriteAllBytes(fullPath, bytes);
        Debug.Log($"📸 撮影成功: {fullPath} に保存しました！");

        AssetDatabase.Refresh();
    }
}