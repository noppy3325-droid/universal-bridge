# Architecture

## v1.01

画面端の復帰は`AutoSwitch && ReturnAtEdge`で有効です。新しい`ReturnAtEdge`は初期値trueで、旧`EstimatedReturn`は読み込み・保存から除外します。`NativeTouchpad`も廃止し、不明な旧プロパティとして無視します。既存の感度・キー・配置・2本指スクロール設定を保持します。

`VirtualCursor`は送信XYの積算であり、iPad実座標ではありません。進入位置をWindows側portalの比率から初期化し、Windows側へ向かう新しい移動で推定境界を越えた場合に復帰します。キー・ボタン・解放待ちがある間は復帰せず、解放やスクロールだけでも復帰しません。入力世代を変更して旧移動を破棄し、両neutralを送ります。Windowsカーソルは接続辺の32px内側、移動後の沿辺比率に対応する位置へ戻し、edge detectorをリセットします。GUIで推定幅・高さを調整できます。実機の加速・タッチ操作によるずれは残ります。

## v1.00

低レベルフック内の位置による到着判定をやめ、実移動を記録して入力スレッドの10msタイマーでGetPhysicalCursorPosを読みます。到着帯3px＋端の1pxと、接続確認中の12pxのヒステリシスを分けます。`EdgeActivationLease`は設定世代・接続準備・入力状態・5秒の期限を持ち、取り消しは不可逆です。入力先が切り替わった後の再センタリングで要求自身を取り消しません。診断は試行件数・成功件数・取消種別だけです。

`PointerMotion`は感度を掛けたXYを小数で保持し、必要なら連続量へradial soft kneeを掛け、その後に一度だけ最近傍丸めします。変換後の両軸の残差を保存し、停止中は±0.5以内の残差が勝手に出力されません。圧縮した距離は残差へ入れません。抑制なしは既存の方向を保つ32767カウント分割を使います。

`ActionShortcuts`は前のアプリ・スクリーンショット・Undo／Redo・Zoom・閉じるの7件を定義します。旧プロフィールには重複を避けて追加し、GUIは各行の有効化と割り当てを保存します。ウィンドウを閉じるは初期値オフです。変更は通常キーボードHID内に収まり、Report Mapは変更しません。

`--verify-edge-input`は仮想の配置・カーソル・入力準備状態を実Win32メッセージループへ渡す非捕捉検証です。実デスクトップのカーソルを動かさず、Bluetoothを公開せず、iPadへの接続成功を表すテストではありません。

## 0.5.0の操作ショートカットと画面端

専用のHome／Search／NextAppをInputRouterの物理キー照合で扱い、グローバルホットキーとして登録しません。iPad操作中だけ有効で、トリガー・リピート・解放を消費します。ほかの文字キーやドラッグがないとき、移動flush → 全解放 → ⌘H／⌘Space／⌘Tab → 解放 → 物理修飾キーの復元、の順に送ります。各コマンドは同じ端末／世代に固定し、途中のキュー異常で次世代へ修飾キーを再送しません。既存の切り替え・復帰キーを優先し、旧設定と新しい既定値が衝突する場合は既存キーを維持して空いているFキーへ移行します。GUI適用時は5組の重複を拒否し、保存とOS再登録の失敗時は元へ戻します。

`EdgeDetector.Observe`は実マウス到着で待機を開始し、`Poll`はタイマーから到着後の待機だけを進めます。静止中の有効化・再接続では開始しません。NativeInputは10ms監視タイマーでも座標を確認し、キー／ボタン押下・復帰・設定変更・取得不可で解除します。公開・選択・両購読を満たした場合にだけ要求を1件ずつ送ります。

画面端由来の要求はモニター・配置・設定世代を持ち、Bluetooth更新とneutral送信の後も現在の端・設定・入力状態を確認します。端を離れた操作や復帰要求で取り消し、遅い接続処理が後から入力を捕捉しません。GUIは同じ隣接モニター除外計算で有効な線を描き、ドラッグで方向・範囲を保存します。iPadカーソル座標は測らず、相対HIDによる操作先の移行です。

## 0.4.0の入力転送

`PointerTransfer`は2軸の長さへsoft kneeを適用し、方向を保持します。上限は8カウント/ms、経過時間を4～32msに制限し、長い停止後の追い付きバーストを防ぎます。半分までは線形です。抑制時は未送信XYを捨て、遅い尾を作りません。GUIでオン／オフでき、初期値オンです。推定カーソルも実際の送出量で更新します。互換モードの接点スクロールは独立した最近傍丸めと残差を使い、物理ホイールの切り捨て方式は維持します。

v1.01では接点転送を削除し、Report Mapは144バイト、keyboard/mouseの2種類のみとします。解放レポートも2種類です。

## 構成

0.3.4では`MotionAccumulator`のXYを小数残差付きの最近傍丸めへ変更します。±0.5の残差は無入力時にゼロへ丸まるため、停止後に往復しません。非飽和時の累積誤差を各軸±0.5カウントへ抑え、大移動の分割ではXYへ共通の縮尺を適用して方向を保ちます。縦横スクロールは従来の整数単位・切り捨てと小数持ち越しを維持します。

`MouseReportTimer`はプロセス内の高精度waitable timerで、iPad操作中だけ実接続間隔に合わせた8～100ms周期で動作します。入力スレッドは`MsgWaitForMultipleObjectsEx`でメッセージとタイマーを待ち、Raw Inputやポインター通知が続いてもWM_TIMERの低優先度に依存せず移動を送信します。各タイマー通知の後にもキューの入力を処理し、緊急復帰操作を飢餓状態にしません。ローカル復帰で停止、終了時に同じ入力スレッドで解放します。作成できない場合は従来のWM_TIMERを期限方式で使い、毎回の遅れに全周期を足し直す処理を避けます。Windows全体のタイマー分解能は変更しません。

`ReportDispatcher.EnqueueMotion`は、まだ送信を開始していない末尾のXY移動だけを合流します。キーボード・ボタン・ホイール／panは合流境界です。端末・世代・ボタン状態が一致し、各軸が同方向で、合計が各軸127カウント以内の場合だけまとめます（0.4.0）。範囲外は切り詰めず次のレポートへ分けます。送信開始時のフレーム取得と末尾更新を同じ短いロックで保護し、非同期送信中のフレームを書き換えません。旧世代の合流移動もWindows復帰後に破棄します。

0.3.1では`TouchpadScroll`でPhysicalGestureRecognizerの累積移動を差分化し、最初の更新に含まれる移動と終了直前の移動を保持します。既定3.00倍・範囲0.25～12.00倍の`TouchpadScrollSensitivity`を追加し、マウスホイールの`ScrollSensitivity`とは別経路で適用します。小数は従来のMotionAccumulatorで保持し、Windows復帰・再切り替え時には累積座標と小数を破棄します。HID descriptorは変更せず、標準HIDの整数スクロール単位を使います。

アプリアイコンは`Assets/app-icon.png`から9サイズのICOへ変換し、exeのApplicationIcon・WPFウィンドウ・トレイ・サイドバーへ組み込みます。トレイ用Iconは所有権を持ち、終了時にDisposeします。生成元とプロンプトはAssets/README.md、変換はscripts/Build-Icon.ps1です。

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

低レベルフックの中ではBluetooth APIやファイルI/Oを呼びません。接続監視は別の経路でスナップショットを更新し、入力経路はロックなしの読み取りだけを行います。2秒を超えて古い状態では抑制しません。マウス移動は相対移動として蓄積し、8〜100msの間隔で送出します。Windows 11が返す接続間隔を参考にします。小数感度・高分解能ホイールの余りは次のレポートへ持ち越します。

クリックの押下・解放とキーボードの全状態遷移は、移動を先にflushした上で順序付きチャネルに格納します。クリックを単なる移動へまとめて消さない設計です。キーボードのOSリピートイベントは追加送信せず、保持状態からiPadOSがリピートします。6キー超過はErrorRollOverを生成します。

送信待ちは256レポートまでです。溢れたら直ちにWindowsへ戻し、送信を停止します。通知の待機は1秒で打ち切ります。失敗後は明示的な切り替え時に、旧送信がすべて完了済み・接続準備済みで、キーボードとマウス両方の解放送信が成功し、generationと障害番号が変わっていない場合だけ送信を復帰します。generationの古い待機レポートは捨てます。復帰時のneutral reportsは既に無効化された接続判定と独立して、残っている各購読先へ個別に試みます。

0.3.2では全購読のGATTセッションを追跡し、置き換わったセッションも監視します。HID Suspendは端末IDに永続保存せず、その書き込み元セッションが終了するか全入力購読がなくなれば解除します。同一接続でのSuspendはResumeまで保持します。通知対象はActiveな購読から選び、接続情報変更前に開始した通知の結果で現在のキー状態キャッシュを上書きしません。切り替え前には接続情報更新の完了を待ちます。

半角／全角はスキャンコード0x29とWindowsの日本語キーボード種別（7）、またはIME専用仮想キーで識別します。iPadモード中だけControl+Spaceの押下・解放へ変換し、その前後で他の押下状態を復元します。物理押下を追跡して長押しを1回に制限し、US配列の通常の0x35は維持します。入力文字列やOSのIME状態の変換・記録は行いません。

0.3.3では、スキャンコード0x29・非拡張のVK_DBE_SBCSCHAR/DBCSCHARに限り、ローカル押下再確認の対象をVK_OEM_3へ正規化します。IMEモード用GetAsyncKeyStateのビットを押下状態と誤認して復帰後の切り替えを拒否し続けるケースを防ぎます。解放待ち集合は残る物理押下の集合へ絞り込みます。未対応の実キーは引き続きフックで追跡し、リモート操作中は再確認で消しません。拒否時・診断書き出しには状態の件数と真偽のみを追加し、キー識別子を記録しません。

## 復帰と終了

Ctrl+Alt+Q、Escの3回押下、GUI／トレイの復帰、Bluetooth無効化、リンク断、購読・セッション変更、送信エラー、フック監視の停滞、デスクトップ／画面構成変更、スリープで復帰します。入力許可を同期的に失効させてから、非同期でキーとボタンの解放を試みます。戻る際に押されていたキー・ボタンの解放はWindowsへ漏らさず、次の通常操作から渡します。別デスクトップで解放されたキーはGetAsyncKeyStateで整合させます。

フックは専用メッセージループで動作し、独立した250ms watchdogがあります。通常操作時は入力をそのまま通します。マウスは同じ入力スレッドのmessage-only windowへRIDEV_INPUTSINKで登録し、WM_INPUTのデバイス相対量を使います。移動を二重送信しないよう、Raw Input使用中はフック側で移動を送信しません。remoteへの移行時だけカーソルを中央へ置き、以後は移動の抑制だけを行います。絶対位置型のraw機器はデバイス別の基準位置から相対量へ変換し、切り替えや画面サイズ変更時は基準をリセットします。低感度で整数直前に生じる浮動小数点誤差は1e-9未満に限り整数へ補正し、小数の残りは保持します。

Raw Inputの登録失敗や、フックでは移動を観測しているのにrawパケットが500ms以上更新されない場合は互換入力へ戻します。互換入力だけがカーソル座標と再センタリングを使用します。RIDEV_NOLEGACYやグローバルのマウス設定変更は使いません。終了時はRaw Input登録・受信window・window class・バッファを入力スレッドで解放します。

Ctrl+Alt+IはRegisterHotKeyで登録し、失敗はGUIとログへ伝えます。専用のActivateRemoteAsyncを呼び、すでにiPadモードなら再切り替えしません。iPadモード中の同じ組み合わせはIの押下・解放を転送から除きます。Ctrl+Alt+Qの緊急復帰は従来どおりフックから同期的に実行します。

上記は初期値です。ShortcutBindingはRegisterHotKey用修飾キーと仮想キー、HID側の照合を共通化します。GUI適用時はWindowsへ戻して入力スレッドに再登録を依頼し、競合時は旧登録へロールバックします。設定保存に失敗した場合も旧登録へ戻します。キューに残った旧WM_HOTKEYはキーと修飾キーを照合して無視します。Escの3回押下は設定に関わらず維持します。

## トラックパッドの取得

TouchpadCaptureはWindows 11 build 26100以降、APIの存在とTouchpadGesturesController.IsSupportedを確認した上で起動します。Microsoftが実験的とするAPIへの参照はこのファイルに限定し、Windows SDK .NET projection 10.0.26100.87で型を参照します。未対応OSでは生成しません。

remoteへの移行時に専用の小さなWin32画面をカーソルの位置へ表示して前面を取得し、TouchpadGesturesControllerを有効にします。RegisterTouchpadCapableWindowで2本指のWM_POINTERを受け、PhysicalGestureRecognizerへ渡します。DefWindowProcによるWindowsホイールへの再変換を避けます。3～5本指は前面プロセス向けのcontrollerのイベントを別のrecognizerで認識します。スクロールは縦横の小数を保持し、ピンチ・スワイプ・タップはキーボードマクロへ変換します。キーやボタンを保持している間はマクロを実行せず、入力所有権を壊さないようにします。

controllerはremote中だけ有効です。Windows側へフォーカスが移るとwatchdogがlocalへ戻します。取得画面を前面にできない場合もlocalへ戻します。終了・正常復帰でcontrollerを無効化し、ジェスチャー状態を終了させ、元の前面画面を復元して受け取り画面を隠します。OS設定やドライバーの変更はありません。接点転送はありません。

カーソルのクリップやシステム全体の入力禁止は使いません。プロセス異常終了時はWindowsがフックを除去します。iPad側の強制終了時のキー解放は保証できません。

GATT providerはアプリの存続中に保持します。「公開停止」でproviderを作り直さず、再公開で同じ属性を利用します。停止APIの反映が非同期であるため、広告の開始要求を記録し、重複したStopAdvertising呼び出しを防止しています。

## 設定・プライバシー

JSON設定は正規化・バージョン確認し、一時ファイルから原子的に置換します。不正な設定は上書きせず警告と初期値で復帰します。診断は容量を制限したキューとローテーションファイルを使い、入力内容を記録しません。GUIが診断を保存する際のみ端末名・接続識別子を含めます。

自動起動はユーザーがチェックを変えた場合だけHKCUのRunに登録します。ドライバー、サービス、管理者常駐、セキュリティ設定変更はありません。

参考調査と方式選定の根拠は [docs/RESEARCH.md](docs/RESEARCH.md) を参照してください。
