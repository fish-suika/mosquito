# 蚊のステルスゲーム

夜の寝室で蚊になって、人間に見つからないように血を吸う 3D ステルスアクションです。血のメーターを満タンにすればクリア、叩かれたらゲームオーバーです。

**遊ぶ（ブラウザ）**: https://fish-suika.github.io/mosquito/

## 遊び方

| 操作 | キー |
|---|---|
| 移動 | WASD |
| 視点 | マウス（最初にクリックするとマウスが固定されます） |
| 上昇 / 下降 | Space / Ctrl |
| 吸血 | 左クリック長押し（肌の近くで） |
| 回避ダッシュ | Shift または右クリック |
| ポーズ | Esc |

- 吸う場所が危ないほど多く吸えます（顔・首・腕・足首の順）
- かゆみ・視界・羽音で「気づき」が溜まり、満タンで見つかると手で叩いてきます
- ダッシュでよけて、物陰に隠れて5秒たてば逃げ切りです
- 音量はタイトルとポーズ画面で調節できます

## 作りかた

- Unity 6000.6.5f1（Built-in レンダーパイプライン）、WebGL
- シーンは `Assets/Editor/SceneBuilder.cs`（メニュー **Mosquito → Build Main Scene**）で自動生成されます
- ゲームの決まりごと（点数・ゲージ・視界）は `Assets/Scripts/StealthRules.cs` に集めてあり、`Assets/Tests/EditMode` にテストがあります
- 音はすべてコードで作った波形で、音声ファイルは使っていません
- 設定資料は `docs/制作設定資料.md`

## クレジット

- 日本語フォント: [Noto Sans JP](https://fonts.google.com/noto/specimen/Noto+Sans+JP)（SIL Open Font License 1.1、`Assets/Fonts/OFL.txt`）
