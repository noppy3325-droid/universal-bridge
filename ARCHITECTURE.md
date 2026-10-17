# Architecture

## 構成

0.3.0ではCoreにオフライン翻訳カタログを埋め込みました。`L.T`／`L.F`とWPFのDynamicResourceで247項目を日本語・英語・簡体字に切り替えます。`AppSettings.Language`はauto／ja／en／zh-Hansを保存し、autoは日本語・中国語以外で英語にフォールバックします。変更時はWindowsへ戻し、画面・トレイ・選択肢を再構築します。旧設定ファイルも既定autoで読み込めます。OS例外文・過去ログは元の言語を保持します。

配布手順は`Build.ps1`→`Package.ps1`です。3言語README、MIT／第三者通知、ソースZIP、SHA256SUMSを収録します。GitHub Actionsはpush／PRのビルド・テストと、バージョン一致タグからのプレビューRelease作成を行います。CIはBluetooth・入力フックを開始しません。

```text
WPF GUI / タスクトレイ (STA)
  BridgeController ─ SettingsStore / DiagnosticLog
       │ 型付きC#呼び出し・イベント（IPCなし）
       ▼
InputRouter (platform-independent, 同期的な入力所有権判定)
       ▲                         │
Win32 hooks (専用MTA thread)      ▼
  keyboard / mouse / timer   ReportDispatcher (bounded channel, worker)
  emergency hotkeys              │ 選択した1台 / timeout / generation
       │                         ▼
  independent watchdog      BleHidTransport / WinRT GATT server
                                 │ encrypted HOGP
                                 ▼
                           iPadOS built-in HID host
```

- `UniversalControl.Core`: HID report map / encoder、JISマッピング、入力所有権、送信キュー、相対移動蓄積、画面端判定、仮想カーソル、設定、再試行方針。OSに依存せずxUnitで検証できます。
- `UniversalControl.Windows`: GATT公開、Bluetoothリンク／購読／無線状態監視、Win32 low-level hooks、モニター列挙、任意の自動起動登録。
- `UniversalControl.App`: 4画面のWPF GUI、トレイ、診断、ライフサイクル、GUIスモーク／プラットフォーム検証モード。
- `UniversalControl.Tests`: プロトコル・状態遷移・異常系・入力洪水の自動テスト。FakeTransportはここだけに存在します。

軽量なネイティブGUIとBluetooth処理を同一ランタイムで保守するため、推奨構成からTauri / Reactを変更しました。WebView、Node、Rust、ローカルHTTPサーバー、ネットワークIPCは実行時に必要ありません。.NET 10を自己完結形式で同梱します。

## HOGP

| 属性 | UUID | 値／役割 |
|---|---|---|
| HID service | 0x1812 | GattServiceProvider |
| HID Information | 0x2A4A | HID 1.11、NormallyConnectable、remote wakeなし |
| Report Map | 0x2A4B | 複合keyboard/mouse、JIS usage含む |
| Control Point | 0x2A4C | Suspend/Exit Suspend、入力値検証 |
| Keyboard input | 0x2A4D + 0x2908 | report ID 1、Input、8 bytes |
| Keyboard output | 0x2A4D + 0x2908 | report ID 1、Output、LED 1 byte |
| Mouse input | 0x2A4D + 0x2908 | report ID 2、Input、7 bytes、AC Panによる横スクロール |

CCCDはWindowsが生成します。手動で予約済みCCCDを追加しません。全属性に暗号化要求を設定しています。Report MapはStaticValueで提供し、長い読取／offset処理はWindowsに任せます。動的な小さい読取と出力書込はdeferralと例外処理を持ち、書込長・offset・値を検証します。ペイロードにはReport IDを前置しません。

Boot Protocolは実装せず、その対応を示す属性も公開しません。Device Information / GAP / GATT等のOS予約サービス、GAP Appearance、BluetoothのOS識別名を上書きしません。

## 入力の所有権

Windows操作 → 対象端末選択 → 両入力レポート購読・ペアリング・接続・フックを確認 → キーとボタンが未押下 → neutral reportsを送信 → 再確認 → iPadモード、の順です。baseline送信中に復帰要求が入った場合はgenerationが変わり、後から抑制が有効になることを防ぎます。

未押下判定は `PhysicalInputState` で物理キーボード・マウスの仮想キーだけをポーリングします。IMEのDBEモード（0xF0等）や予約値・文字パケットを押下として扱いません。実際のフックから観測したキーは未対応のものもInputRouterで追跡し、押下中は引き続き切り替えを拒否します。切り替え開始時に解放済みの追跡を整合し、最大2秒キー解放を待ち、待機中の復帰要求もgenerationで取り消します。拒否理由はActivationStatusで画面・診断ログへ伝えます。

低レベルフックの中ではBluetooth APIやファイルI/Oを呼びません。接続監視は別の経路でスナップショットを更新し、入力経路はロックなしの読み取りだけを行います。2秒を超えて古い状態では抑制しません。マウス移動は相対移動として蓄積し、15〜100msの間隔で送出します。Windows 11が返す接続間隔を参考にします。小数感度・高分解能ホイールの余りは次のレポートへ持ち越します。

クリックの押下・解放とキーボードの全状態遷移は、移動を先にflushした上で順序付きチャネルに格納します。クリックを単なる移動へまとめて消さない設計です。キーボードのOSリピートイベントは追加送信せず、保持状態からiPadOSがリピートします。6キー超過はErrorRollOverを生成します。

送信待ちは256レポートまでです。溢れたら直ちにWindowsへ戻し、送信を停止します。通知は1秒で打ち切り、失敗後はアプリ再起動まで転送を禁止します。generationの古い待機レポートは捨てます。復帰時のneutral reportsは既に無効化された接続判定と独立して、残っている各購読先へ個別に試みます。

## 復帰と終了

Ctrl+Alt+Q、Escの3回押下、GUI／トレイの復帰、Bluetooth無効化、リンク断、購読・セッション変更、送信エラー、フック監視の停滞、デスクトップ／画面構成変更、スリープで復帰します。入力許可を同期的に失効させてから、非同期でキーとボタンの解放を試みます。戻る際に押されていたキー・ボタンの解放はWindowsへ漏らさず、次の通常操作から渡します。別デスクトップで解放されたキーはGetAsyncKeyStateで整合させます。

フックは専用メッセージループで動作し、独立した250ms watchdogがあります。通常操作時は入力をそのまま通します。マウスは同じ入力スレッドのmessage-only windowへRIDEV_INPUTSINKで登録し、WM_INPUTのデバイス相対量を使います。移動を二重送信しないよう、Raw Input使用中はフック側で移動を送信しません。remoteへの移行時だけカーソルを中央へ置き、以後は移動の抑制だけを行います。絶対位置型のraw機器はデバイス別の基準位置から相対量へ変換し、切り替えや画面サイズ変更時は基準をリセットします。低感度で整数直前に生じる浮動小数点誤差は1e-9未満に限り整数へ補正し、小数の残りは保持します。

Raw Inputの登録失敗や、フックでは移動を観測しているのにrawパケットが500ms以上更新されない場合は互換入力へ戻します。互換入力だけがカーソル座標と再センタリングを使用します。RIDEV_NOLEGACYやグローバルのマウス設定変更は使いません。終了時はRaw Input登録・受信window・window class・バッファを入力スレッドで解放します。

Ctrl+Alt+IはRegisterHotKeyで登録し、失敗はGUIとログへ伝えます。専用のActivateRemoteAsyncを呼び、すでにiPadモードなら再切り替えしません。iPadモード中の同じ組み合わせはIの押下・解放を転送から除きます。Ctrl+Alt+Qの緊急復帰は従来どおりフックから同期的に実行します。

上記は初期値です。ShortcutBindingはRegisterHotKey用修飾キーと仮想キー、HID側の照合を共通化します。GUI適用時はWindowsへ戻して入力スレッドに再登録を依頼し、競合時は旧登録へロールバックします。設定保存に失敗した場合も旧登録へ戻します。キューに残った旧WM_HOTKEYはキーと修飾キーを照合して無視します。Escの3回押下は設定に関わらず維持します。

## トラックパッドの取得

TouchpadCaptureはWindows 11 build 26100以降、APIの存在とTouchpadGesturesController.IsSupportedを確認した上で起動します。Microsoftが実験的とするAPIへの参照はこのファイルに限定し、Windows SDK .NET projection 10.0.26100.87で型を参照します。未対応OSでは生成しません。

remoteへの移行時に専用の小さなWin32画面をカーソルの位置へ表示して前面を取得し、TouchpadGesturesControllerを有効にします。RegisterTouchpadCapableWindowで2本指のWM_POINTERを受け、PhysicalGestureRecognizerへ渡します。DefWindowProcによるWindowsホイールへの再変換を避けます。3～5本指は前面プロセス向けのcontrollerのイベントを別のrecognizerで認識します。スクロールは縦横の小数を保持し、ピンチ・スワイプ・タップはキーボードマクロへ変換します。キーやボタンを保持している間はマクロを実行せず、入力所有権を壊さないようにします。

controllerはremote中だけ有効です。Windows側へフォーカスが移るとwatchdogがlocalへ戻します。取得画面を前面にできない場合もlocalへ戻します。終了・正常復帰でcontrollerを無効化し、ジェスチャー状態を終了させ、元の前面画面を復元して受け取り画面を隠します。OS設定やドライバーの変更はありません。iPadネイティブの接点転送は実装していません。

カーソルのクリップやシステム全体の入力禁止は使いません。プロセス異常終了時はWindowsがフックを除去します。iPad側の強制終了時のキー解放は保証できません。

GATT providerはアプリの存続中に保持します。「公開停止」でproviderを作り直さず、再公開で同じ属性を利用します。停止APIの反映が非同期であるため、広告の開始要求を記録し、重複したStopAdvertising呼び出しを防止しています。

## 設定・プライバシー

JSON設定は正規化・バージョン確認し、一時ファイルから原子的に置換します。不正な設定は上書きせず警告と初期値で復帰します。診断は容量を制限したキューとローテーションファイルを使い、入力内容を記録しません。GUIが診断を保存する際のみ端末名・接続識別子を含めます。

自動起動はユーザーがチェックを変えた場合だけHKCUのRunに登録します。ドライバー、サービス、管理者常駐、セキュリティ設定変更はありません。

参考調査と方式選定の根拠は [docs/RESEARCH.md](docs/RESEARCH.md) を参照してください。
