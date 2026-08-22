# 設定画面 UI

**画像素材を一切使わず**、Unity 標準 UI（Image / TextMeshPro / Button / Toggle / Slider /
Dropdown / Scrollbar / Layout Group）だけで白黒のデザインを組むための仕組み。

`Image` にスプライトを割り当てなければ単色の矩形として描かれる。これを組み合わせるだけなので、
解像度が変わっても崩れず、9-Slice もスプライト編集も不要。

用途の違う 2 つの入口がある。

| やりたいこと | メニュー |
|---|---|
| **既存の SettingCanvas の見た目だけ変える**（機能はそのまま） | ODD ODDS / UI / **既存の設定画面をリスキン** |
| 白紙から新しい設定画面の骨組みを作る | ODD ODDS / UI / 設定画面の骨組みを生成 |

どちらも配色の出どころは同じ `SettingsTheme.asset` ひとつ。

---

## 既存の設定画面をリスキンする

`Assets/Resources/Prefab/SettingCanvas.prefab`（3D_Title_Sample と GameScene に配置）の
**見た目だけ**を塗り替える。`SettingUIManager` が持つ参照には一切触らないので機能は変わらない。

1. メニュー → **ODD ODDS / UI / 既存の設定画面をリスキン**
2. 色を変えたくなったら `SettingsTheme.asset` を編集して、もう一度同じメニューを実行

何度実行しても同じ結果になる（枠線などは作り直さず設定だけ更新する）。

### リスキンで行っていること

- 全 Image のスプライトを外して単色の矩形にする
- 背景・パネル・設定行をテーマの色で塗る
- ボタン・ドロップダウン・トグル・行に **枠線** を足す（後述）
- スライダーの溝を細くし、つまみを白い四角にする
- スクロールバーを細い線にする
- ドロップダウンの矢印を画像から「▼」の文字に置き換える
- トグルのチェックを白い塗り＋黒い「✓」にする
- フォントをテーマのものに差し替える
- `SettingUIManager` の `_theme` にテーマを自動で割り当てる

### 文字サイズについて

既存レイアウトを壊さないため、**既定では文字サイズを変更しない**。
サイズも一括で流し込みたい場合は `SettingsTheme.asset` の
**Apply Font Sizes On Restyle** をオンにしてから実行する。

---

## 枠線の作り方（重要）

「白い矩形の上に一回り小さい黒を重ねる」方式は、**既存の UI には使えない**。
子は必ず親の Image より手前に描かれるため、白い枠を子として足すと塗りを覆い隠してしまう。

そこで `UIRectBorder` を使う。上下左右 4 本の細い Image を子として置き、外周だけを描く方式。
中央が空くので、親の塗り・Color Tint・スクリプトによる色変更をそのまま活かせる。

太さと色は Inspector で直接変えられる。辺ごとに表示/非表示もできるので、
見出しの下線のように「1 辺だけ」という使い方もできる。

---

## SettingUIManager と色の分担

タブの選択色とキーバインドの色は、Color Tint ではなく **スクリプトが直接代入**している。
そのためテーマ側に専用の項目を用意し、`SettingUIManager` がそこから読むようにしてある。

| 項目 | 制御している場所 |
|---|---|
| タブ 選択中 / 非選択 の塗りと文字 | `SettingUIManager.SettingButtonAnimation()` |
| キーバインド 通常時（白） / 入力待ち（赤） | `SettingUIManager` のリバインド処理 |
| それ以外のホバー・押下 | Unity の Color Tint（テーマの `btn*` / `handle*` / `item*`） |

`_theme` が未設定でも従来のハードコード値で動くので、リスキン前の状態でも壊れない。

### タブとキーバインドは Color Tint を切ってある

スクリプトが `targetGraphic.color` に直接代入しているため、Color Tint を残すと
ホバー・離脱のたびに上書きされて選択表示が消える。リスキン時に `Transition = None` にしている。
その代わりホバーの色変化は無くなる。

---

## 白紙から骨組みを作る

新しい設定画面をゼロから作る場合は **ODD ODDS / UI / 設定画面の骨組みを生成**。
タブ 4 枚・スクロール領域・フッターと、複製元の `SettingRow_*.prefab` が生成される。

- 設定項目を増やす → `Prefabs/SettingRow_*.prefab` を複製して `Page_*` の下に置く
- テーマを変えたら → `SettingsScreen` の右クリック →「テーマを適用」
- Escape で開閉 → `SettingsScreen` の Open Action に `InputSystem_Actions` の OpenSetting を割り当てる

> 既存の `SettingUIManager` も Escape を見ているため、両方有効だと同時に開く。
> 併用する場合はどちらかを無効にすること。

---

## ファイル構成

```
Assets/ODDODDS/UI/
├── SettingsTheme.asset          色・サイズ・フォントの定義（初回実行時に作られる）
├── Prefabs/                     骨組み生成時の複製元
└── Scripts/
    ├── Runtime/
    │   ├── SettingsTheme.cs         テーマ定義と適用ロジック
    │   ├── UIRectBorder.cs          4 本の線で描く枠線（既存UIに後付けできる）
    │   ├── SelectableTint.cs        Color Tint の当て方（黒地 / 白つまみ / 項目）
    │   ├── ButtonLabelInvert.cs     背景反転時の文字色入れ替え
    │   ├── UIButtonScale.cs         押下時の縮小
    │   ├── ThemedElement.cs         テーマ上の役割マーカー（骨組み生成側で使用）
    │   ├── SettingsScreen.cs        骨組み側のルート。開閉とタブ切り替え
    │   ├── SettingsTabButton.cs     骨組み側のタブ
    │   ├── SettingRow.cs            骨組み側の設定行
    │   └── ToggleGraphicFollower.cs Toggle の ON/OFF 追従表示
    └── Editor/
        ├── SettingsCanvasRestyler.cs  既存 SettingCanvas のリスキン
        ├── SettingsUIBuilder.cs       骨組みの生成
        └── SettingsUIFactory.cs       UI 部品の組み立てヘルパー
```

---

## 設計上の要点

**Color Tint は色を「上書き」する** — 白いつまみに黒地用の設定を当てると黒く潰れる。
`TintMode`（Dark / Handle / DropdownItem）で使い分けている。

**チェックマークと矢印は文字** — `✓` と `▼` を TextMeshPro で描いているので画像がいらない。

**線の太さを揃えるのが一番効く** — 大きな枠 2px / 小さな枠・装飾線 1px / スライダー 3px。
テーマの `borderThick` / `borderThin` / `sliderThickness` で一括管理している。
