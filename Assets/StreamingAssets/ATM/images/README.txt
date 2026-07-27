ここに ATM 画面で使う画像(PNG)を置いてください。
ATMScreens.yaml の image 要素で "images/ファイル名.png" のように参照します。

例:
  - type: image
    sprite: "images/logo.png"
    width: 128
    height: 128
    x: 0
    y: -140

StreamingAssets 配下なので、画像を追加/差し替えしても Unity の再インポートは不要です。
