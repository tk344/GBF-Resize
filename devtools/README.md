# devtools

コードを直す人向けの、動作確認用のスクリプトです。`GBF.exe` を使うだけなら必要ありません。

本物の GBF の窓に触れずに試せるよう、タイトルが `GBFTEST` のテスト用ページ(`test.html`)を使います。

## 試し方

1. `GBF.exe` を、このリポジトリとは別のフォルダにコピーします(`GBF.exe` は自分と同じフォルダの `GBF.ini` を読むため)
2. そのフォルダに、テスト用の `GBF.ini` を置きます

   ```ini
   browser = C:\Program Files\Google\Chrome\Application\chrome.exe
   profile = Default
   url = file:///<このリポジトリのパス>/devtools/test.html
   title = GBFTEST
   count = 2
   layout2 = 100, 100, 380, 600 | 466, 100, 380, 600
   ```

3. コピーした `GBF.exe` で `launch` / `resize` / `record` を実行し、`tframes.ps1` で結果を見ます。
   `nudge.ps1` で窓をわざとずらしてから `resize` や `record` を試せます
4. 終わったら `closetest.ps1` でテスト用の窓を閉じます

## スクリプト

| スクリプト | 内容 | 本物の GBF の窓 |
|---|---|---|
| `tframes.ps1` | GBFTEST の窓の位置・大きさ・見た目の枠を一覧する | 触らない |
| `nudge.ps1 -Hwnd <16進> -X -Y -W -H` | 指定した窓を動かす・大きさを変える | ハンドルを指定したときだけ |
| `closetest.ps1` | GBFTEST の窓をすべて閉じる | 触らない |
| `frames.ps1` | 本物の GBF の窓の位置・大きさ・見た目の枠を一覧する | 読むだけ |
| `sampler.ps1 -Out <ファイル> [-Seconds 17]` | 本物の GBF の窓の大きさを0.05秒ごとに見て、変わったときに記録する(ページが自分で大きさを変える様子を調べる用) | 読むだけ |
| `click.ps1 "<ボタン名>"` | 設定画面のボタンを押す | 設定画面しだい |
| `shotform.ps1 -Out <png>` | 設定画面を撮影する | 触らない |

## 注意

- 座標はすべて物理ピクセルです
- 設定画面の部品は、PowerShell の UI Automation からは Pane にしか見えず、InvokePattern が使えません。
  そのため `click.ps1` は、ボタンの窓ハンドルに `BM_CLICK` を送って押しています
- 日本語を含むスクリプトは、Windows PowerShell 5.1 で正しく読めるよう BOM 付き UTF-8 で保存しています
