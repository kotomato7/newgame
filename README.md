# newgame
3min game with Mr.Shimizu

## キーマウ如律令 (Unity 版)

Pygame 版 (`Pygame_wAI`) をそのまま Unity に移したもの。画面の大きさ(内部 480×270 を 2 倍で 960×540)・文字の大きさ・ルール・数値は Pygame 版と同じ。

### 動かし方
1. Unity 6 (6000.4) でこのフォルダを開く
2. `Assets/Scenes/Main.unity` を開いて再生する(Game ビューは 960x540 や 1920x1080 にすると、ドットが整数倍でくっきり表示される)

### フォルダの構成
| 場所 | 中身 | Pygame 版 |
| --- | --- | --- |
| `Assets/Scripts/Core/` | ゲーム本体(画面・バトル・QTE・データ読みこみ)。UnityEngine を使わない C# | `main.py` `settings.py` `game_data.py` `characters.py` `scenes/` `ui/` |
| `Assets/Scripts/Unity/` | Unity とのつなぎ(毎フレームの呼び出し・キー入力・画面表示・画像読みこみ・BGM) | `main.py` の `run()`、`bgm.py` |
| `Assets/StreamingAssets/data/` | パラメータの表(CSV)。Excel で書きかえるとそのまま反映 | `data/` |
| `Assets/StreamingAssets/assets/` | 画像・BGM・フォント。フォルダに入れるだけで使われる(README.txt あり) | `assets/` |
| `Assets/Scenes/Main.unity` | ゲームを動かすシーン(`Game` オブジェクトに `GameRoot`) | |
| `Tools/bake_font.py` | ドットフォントのデータ(`DotGothic16-bitmap.bytes`)を作りなおすスクリプト | |

### しくみのメモ
- Pygame 版と同じく、480×270 の小さな画面にドットを描いてから、補間なしで整数倍に拡大して表示している
- 四角・線・円・文字の描きかたや色の混ぜかたは pygame と同じ計算にしてあり、同じ操作をしたときの画面が Pygame 版とドット単位で一致することを確かめてある
- 文字は DotGothic16 をアンチエイリアスなしで描いたデータを使う(16 / 32 / 48 の3サイズ・フォントの全文字)。別のサイズを使うときは `Tools/bake_font.py` の `SIZES` に足して実行しなおす
- 画像・CSV・BGM は Pygame 版と同じく、ゲーム中にフォルダから直接読む。タイトルで「はじめる」を押すと表と画像を読みなおす
- Unity で読める画像は PNG / JPG、流せる BGM は .ogg / .mp3 / .wav
