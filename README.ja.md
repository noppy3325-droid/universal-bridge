# Universal Bridge

<img src="docs/images/app-icon.png" width="96" height="96" alt="Universal Bridge アイコン">

**[English](README.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)**

Windows PCのキーボード・マウスと対応するトラックパッド操作を、Bluetooth経由でiPadと共有するWindows 11アプリです。C# / .NET 10 / WPFで実装し、[MITライセンス](LICENSE)のOSSとして公開しています。

iPad用アプリ、追加機器、使用中のインターネットは不要です。Bluetoothアダプターとドライバーには **BLE Peripheral** 対応が必要です。

**プレビュー v0.3.2。** 基本のキーボード入力・マウス操作はiPadでの成功報告があります。ジェスチャー変換、アプリ固有キー、異なる機器は実機確認が残ります。Apple・Microsoftの公式製品ではありません。

![接続ダッシュボード](docs/images/dashboard-ja.png)

## ダウンロード

**[Releases — v0.3.2](https://github.com/noppy3325-droid/universal-bridge/releases/tag/v0.3.2)** から取得します。

- **[UniversalBridge-0.3.2-win-x64.zip](https://github.com/noppy3325-droid/universal-bridge/releases/download/v0.3.2/UniversalBridge-0.3.2-win-x64.zip)**：展開して `UniversalControl.exe` を起動。.NET同梱で別途ランタイムは不要です。
- `UniversalBridge-0.3.2-source.zip`：SDK・キャッシュ・設定・診断ログを含まないソースです。
- `SHA256SUMS.txt`：exeとZIPのSHA-256チェックサムです。

Windows x64専用、署名なしのexeです。インストーラー・MSIX・コード署名は提供していません。

## 主な機能

| 機能 | 内容 |
|---|---|
| Bluetoothキーボード・マウス | ペア済みの選択端末1台へ標準HIDを送信。GATTは暗号化必須です。 |
| 画面端切り替え | ディスプレイ・方向・範囲・押し続け時間を設定。隣接Windowsモニターとの境界を考慮します。 |
| ショートカット編集 | 初期値はiPadへ **Ctrl+Alt+I**、Windowsへ **Ctrl+Alt+Q**。入力設定から変更可能。**Escを3回**の緊急復帰は常に有効です。 |
| 細かいマウス操作 | Raw InputでWindowsの加速を回避。感度 **0.02～4.00**、0.01刻み、小数移動量の保持に対応します。 |
| トラックパッド変換 | 2本指の縦横スクロール感度を専用の **0.25～12.00倍**（初期値 **3.00倍**）で調整。ピンチのキー変換、3～5本指スワイプも対応します。 |
| 3言語のGUI | 日本語・英語・中国語（簡体字）・システム言語。即時反映し次回起動時も保持します。 |
| 常駐と診断 | 専用アプリ／トレイアイコン、ライト／ダーク、任意のサインイン時起動、接続診断、回数制限付き再接続。 |

**iPadネイティブのマルチタッチ接点やMagic Trackpad固有プロトコルは転送しません。** 標準HIDのスクロール・対応キーへ変換します。拡大縮小・ホーム等はiPadOSやアプリに依存します。

## 使い始める

1. 旧版を「詳細・診断 → アプリを終了」で終了します。ZIPを展開し、通常権限で `UniversalControl.exe` を起動します。
2. 右上で **日本語 / English / 简体中文 / システムの言語** を選べます。
3. 「Bluetooth HIDを公開」を押します。非対応機器やポリシーの制限は画面に表示されます。
4. iPadの「設定 → Bluetooth」で **Windows PCのコンピューター名** を選び、両端末で人がペアリング確認を行います。
5. アプリでiPadを選び、ペア済み・キーボードとマウスの両購読が揃ったことを確認します。
6. **Ctrl+Alt+Iを押して全キーを離す**、または切り替えボタンでiPadへ移ります。メモ等で入力・クリック・スクロールを試し、**Ctrl+Alt+Q** または **Escを3回** で戻ります。
7. 「入力設定」で感度・ジェスチャー・キーを変更します。キーは「ショートカットを適用」で保存し、その他は自動保存。確認後に画面端切り替えを有効にしてください。

ジェスチャー取得中は小さな受け取り画面が前面に出ます。別のWindows画面へフォーカスが移るとWindowsへ戻ります。**Windows 11 24H2以降の対応する実験的Precision Touchpad API**を実行時に確認します。未対応なら取得をオフにするとキー・マウスのみ使えます。この場合、Windowsのシステムジェスチャーは抑制しません。

0.1.xからの更新は、横スクロール追加でマウスレポートが6→7バイトになります。動かなければ再接続し、必要ならiPad側で人がPC登録を解除・再ペアリングしてください。

v0.3.2では「入力設定 → 2本指スクロール感度」でマウスホイールとは独立して調整できます。推奨値3.00は旧版の既定値の3倍です。数値入力・0.25刻みの増減・推奨値へ戻す操作に対応し、動き始めと終了直前の移動量も累積値から拾います。標準HIDの整数スクロール単位であり、ピクセル単位の連続スクロールや追加の慣性は生成しません。

## プライバシーと復帰

iPadのスリープ復帰後はロックを解除し、両購読が揃ってからiPadへの切り替えショートカットを押し直してください。v0.3.2では新しいGATTセッションを追跡し、キーボード・マウス両方の解放送信が成功した場合に送信処理を復帰します。自動で操作先をiPadへ戻さず、タイムアウトした送信が未完了なら完了まで復帰を拒否します。切断のままならiPad「設定 → Bluetooth」でPCへ接続してください。

Windowsが日本語キーボードとして認識している場合、**半角／全角**（Alt併用も対応）は記号を送らず、**Control+Spaceを1回**送ってiPadの入力言語を切り替えます。長押しで連続切り替えせず、US配列のバッククォートは維持します。iPad「設定 → 一般 → キーボード → キーボード」に日本語と英語を追加してください。[iPadの外付けキーボード用ショートカット](https://support.apple.com/en-euro/guide/ipad/ipaddd28d7ed/ipados)を使うため、配列やIMEの最終的な動作はiPadOSの設定に従います。

入力文字列やHIDペイロードは記録しません。設定・接続診断は `%LOCALAPPDATA%\UniversalBridge` に置き、ログは1MiBでローテーションします。テレメトリーや実行時クラウドサービスはありません。

接続・両購読・フック・キー解放の確認後だけ切り替えます。切断、取得中のフォーカス喪失、キュー異常、スリープ、セッション変更でWindowsへ戻り、再接続時に自動捕捉しません。強制終了時はiPadへ解放レポートを送れず、iPad側の復帰はBluetooth切断処理に依存します。

削除時は自動起動をオフにしてアプリを終了し、展開フォルダを削除します。設定フォルダ・iPadのペアリングは必要に応じて個別に削除してください。

## ビルドとテスト

Windows 11 x64、PowerShell 7、**.NET SDK 10.0.402**。依存取得時のみインターネットが必要です。最低OSはbuild22000、ジェスチャーにはbuild26100以降の対応APIが必要です。

```powershell
# 任意：Microsoft公式SDKを作業フォルダ内だけに準備
pwsh -File scripts/Setup-Sdk.ps1
pwsh -File scripts/Build.ps1 -Test -Publish
pwsh -File scripts/Package.ps1
```

SDKとパッケージは `.tools/` に保存。Windows SDK projectionは `10.0.26100.87` 固定で、実験的APIは分離・対応確認します。ドライバー、制限回避レジストリ、VM、OSのセキュリティ変更は導入しません。

**自動テスト135件**でHID・スリープ／再接続・JIS言語切り替え・精度・ショートカット・翻訳・言語保存を検証します。GUIテストは入力を捕捉せず3言語を確認し、iPad実機確認とは区別します。

[開発・翻訳参加](CONTRIBUTING.md)、[構成](ARCHITECTURE.md)、[制約](KNOWN_LIMITATIONS.md)、[検証結果](TEST_REPORT.md)、[実機チェック](docs/MANUAL_TESTS.md)を参照してください。GitHub Actionsでpush・PRをビルド／テストし、バージョンタグからプレビューReleaseを生成します。

## ライセンスと謝辞

[MIT](LICENSE)。HID descriptor/keymapの一部を [windows-ble-hid](https://github.com/abhishek-raj/windows-ble-hid) から改変し、OpenSpanを調査資料として参照しています。出典・同梱通知は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) と [licenses](licenses) に収録しています。
