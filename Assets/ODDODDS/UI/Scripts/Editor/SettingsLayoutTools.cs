using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings.EditorTools
{
    /// <summary>
    /// 「Inspector で位置を動かせない」「動かしても保存すると戻る」を解消するための道具。
    ///
    /// 設定画面の各ページには Vertical Layout Group が付いており、その直下の要素は
    /// 位置とサイズをレイアウトに支配される（Inspector の RectTransform が灰色になる）。
    /// 自由に置きたい要素は Layout Element の Ignore Layout を立てて対象から外す。
    /// </summary>
    public static class SettingsLayoutTools
    {
        private const string MenuFree    = "ODD ODDS/UI/選択中のUIを自由配置にする";
        private const string MenuRestore = "ODD ODDS/UI/選択中のUIをレイアウトに戻す";
        private const string MenuBorder  = "ODD ODDS/UI/選択中の枠線を手動調整モードにする";
        private const string MenuKeep    = "ODD ODDS/UI/選択中のUIを手動配置にする (リスキン対象外)";
        private const string MenuUnkeep  = "ODD ODDS/UI/選択中のUIの手動配置を解除する";

        // ==================================================================
        // Layout Group からの解放
        // ==================================================================

        [MenuItem(MenuFree, false, 40)]
        private static void FreeFromLayout()
        {
            var targets = SelectedRects();
            if (targets.Length == 0) return;

            int n = 0;
            foreach (var rt in targets)
            {
                var parentGroup = rt.parent != null ? rt.parent.GetComponent<LayoutGroup>() : null;
                if (parentGroup == null)
                {
                    Debug.Log($"[LayoutTools] {rt.name} の親に Layout Group が無いので、元から自由に動かせます。", rt);
                    continue;
                }

                var element = rt.GetComponent<LayoutElement>();
                if (element == null)
                {
                    element = Undo.AddComponent<LayoutElement>(rt.gameObject);
                }
                else
                {
                    Undo.RecordObject(element, "自由配置にする");
                }

                element.ignoreLayout = true;
                EditorUtility.SetDirty(element);
                n++;
                Debug.Log($"[LayoutTools] {rt.name} をレイアウト制御から外しました。自由に動かせます。", rt);
            }

            if (n > 0) MarkSceneDirty(targets[0]);
        }

        [MenuItem(MenuRestore, false, 41)]
        private static void RestoreToLayout()
        {
            var targets = SelectedRects();
            foreach (var rt in targets)
            {
                var element = rt.GetComponent<LayoutElement>();
                if (element == null || !element.ignoreLayout) continue;

                Undo.RecordObject(element, "レイアウトに戻す");
                element.ignoreLayout = false;
                EditorUtility.SetDirty(element);
                Debug.Log($"[LayoutTools] {rt.name} をレイアウト制御に戻しました。", rt);
            }

            if (targets.Length > 0) MarkSceneDirty(targets[0]);
        }

        [MenuItem(MenuFree, true)]
        [MenuItem(MenuRestore, true)]
        private static bool ValidateSelection() => SelectedRects().Length > 0;

        // ==================================================================
        // 枠線の手動調整
        // ==================================================================

        [MenuItem(MenuBorder, false, 42)]
        private static void MakeBorderManual()
        {
            var borders = Selection.gameObjects
                .SelectMany(go => go.GetComponentsInChildren<UIRectBorder>(true))
                .Distinct()
                .ToArray();

            if (borders.Length == 0)
            {
                Debug.LogWarning("[LayoutTools] 選択物に UIRectBorder がありません。");
                return;
            }

            foreach (var border in borders)
            {
                Undo.RecordObject(border, "枠線を手動調整モードに");
                border.AutoLayout = false;
                EditorUtility.SetDirty(border);
            }

            Debug.Log($"[LayoutTools] {borders.Length} 個の枠線を手動調整モードにしました。" +
                      "各辺（__Border_T/B/L/R）の位置とサイズを自由に変えられます。");
            MarkSceneDirty(borders[0]);
        }

        // ==================================================================
        // リスキンから配置を守る
        // ==================================================================

        [MenuItem(MenuKeep, false, 60)]
        private static void KeepManual()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<KeepManualLayout>() != null) continue;
                Undo.AddComponent<KeepManualLayout>(go);
                Debug.Log($"[LayoutTools] {go.name} をリスキンの配置変更から保護しました。" +
                          "色とフォントは引き続きテーマに追従します。", go);
            }

            if (Selection.gameObjects.Length > 0) MarkSceneDirty(Selection.gameObjects[0]);
        }

        [MenuItem(MenuUnkeep, false, 61)]
        private static void UnkeepManual()
        {
            foreach (var go in Selection.gameObjects)
            {
                var marker = go.GetComponent<KeepManualLayout>();
                if (marker != null) Undo.DestroyObjectImmediate(marker);
            }

            if (Selection.gameObjects.Length > 0) MarkSceneDirty(Selection.gameObjects[0]);
        }

        [MenuItem(MenuKeep, true)]
        [MenuItem(MenuUnkeep, true)]
        [MenuItem(MenuBorder, true)]
        private static bool ValidateGameObjects() => Selection.gameObjects.Length > 0;

        // ==================================================================

        private static RectTransform[] SelectedRects()
            => Selection.gameObjects
                        .Select(go => go.GetComponent<RectTransform>())
                        .Where(rt => rt != null)
                        .ToArray();

        private static void MarkSceneDirty(Object context)
        {
            var component = context as Component;
            var go = component != null ? component.gameObject : context as GameObject;
            if (go == null) return;

            if (PrefabUtility.IsPartOfPrefabInstance(go) || go.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
