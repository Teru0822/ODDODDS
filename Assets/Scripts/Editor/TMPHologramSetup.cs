using UnityEditor;
using UnityEngine;

namespace FeverCapital.Editor
{
    public static class TMPHologramSetup
    {
        const string ShaderName = "Custom/TMP_Hologram";
        const string SavePath   = "Assets/Shaders/DSEG7_Hologram.mat";

        [MenuItem("FEVER CAPITAL/ホログラムマテリアル生成")]
        static void CreateHologramMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[TMPHologramSetup] シェーダーが見つかりません: " + ShaderName +
                               "\nUnity の再コンパイルを待ってから再試行してください。");
                return;
            }

            var mat = new Material(shader) { name = "DSEG7_Hologram" };

            mat.SetFloat("_HologramAlpha",     0.85f);
            mat.SetFloat("_EmissionIntensity",  2.0f);
            mat.SetFloat("_ScanlineSpeed",      1.5f);
            mat.SetFloat("_ScanlineFrequency", 30.0f);
            mat.SetFloat("_ScanlineContrast",   0.25f);
            mat.SetFloat("_RimIntensity",       2.5f);
            mat.SetFloat("_RimWidth",           0.15f);
            mat.SetFloat("_FlickerSpeed",       8.0f);
            mat.SetFloat("_FlickerIntensity",   0.08f);
            mat.SetFloat("_GlitchSpeed",        3.0f);
            mat.SetFloat("_GlitchIntensity",    0.02f);
            mat.SetFloat("_GlitchProbability",  0.05f);

            AssetDatabase.CreateAsset(mat, SavePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);

            Debug.Log("[TMPHologramSetup] マテリアルを生成しました: " + SavePath + "\n\n" +
                      "──── Inspector での設定手順 ────\n" +
                      "1. TimerText (TextMeshProUGUI) を選択\n" +
                      "2. Font Material 欄に DSEG7_Hologram をドラッグ\n" +
                      "3. TimerDisplay コンポーネントの\n" +
                      "   『警告時 Face Color』を 黒→赤 (#FF2619) に変更\n" +
                      "   ※ ホログラムシェーダーは頂点カラーを発光色に使うため、\n" +
                      "      黒のままだと警告時に文字が消えます\n" +
                      "────────────────────────────");
        }
    }
}
