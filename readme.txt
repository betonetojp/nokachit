◆ 動作環境

Windows11 22H2 (x64)
.NET 8.0
※ランタイムが必要です。インストールしていない場合は初回起動時の案内に従ってください。


◆ nokachit.exe

Nostrのユーザーの発言をそれぞれSSPのゴーストに割り当てることができます。

その他はnokaと同じです。
https://github.com/betonetojp/noka


◆ 更新履歴

2024/07/15 v0.0.2
OnNostrイベントのReference1を正しく送信するように修正しました。

2024/07/15 v0.0.2
kind:42 チャンネルメッセージを受信できるようにしました。
設定画面の開閉で個別ゴースト用の一覧が更新されるようにしました。
設定画面の開閉で soleghosts.json を読み込むようにしました。

2024/07/14 v0.0.1
初公開


◆ Nostrクライアントライブラリ

NNostr
https://github.com/Kukks/NNostr
内のNNostr.Client Ver0.0.49を一部変更して利用しています。


◆ DirectSSTP送信ライブラリ

DirectSSTPTester
https://github.com/nikolat/DirectSSTPTester
内のSSTPLib Ver4.0.0を利用しています。
