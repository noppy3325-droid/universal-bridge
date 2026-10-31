# Verification report — v2.0.1 / 2.0.1

検証日: **2026-10-31 / Asia/Tokyo**。

- **335成功、0失敗、0スキップ**、ビルド警告0／エラー0。追加30件で、感度0.02～4、四方向・斜め、最後の小反転、境界保持、150ms間隔と長い休止、小往復の非累積、キー／ボタン／スクロール混在を検証。更新は実ZIPの少量取得と同一exe生成、切断／停滞後の4096バイト位置からの再開、Range無視時の再取得、不正範囲／破損／余分なentry／パス付きentry／不正SHA拒否、旧形式、既存出力保護、キャンセルを確認しました。`artifacts/tests/tests.trx`。
- 配布exeのGUIは**3言語・42画像・Success=True**。タイトルバーテーマ、アイコン、設定往復、右下カードの配置とforeground不変を確認。共有・更新通信・入力抑制なし。`artifacts/201-final-smoke-test/gui-smoke.json`。
- 配布exeの`--verify-edge-return`は四方向で成功。進入猶予、単発フリック、静止、ドラッグ、復帰位置の内側配置を確認。`--verify-pointer-precision`も成功し、通常のマウス転送精度を維持。入力フック・実カーソル移動・Bluetooth公開は行っていません。`artifacts/201-final-verify-edge-return/edge-return.json`、`artifacts/201-final-verify-pointer-precision/pointer-precision.json`。
- 圧縮版で`--verify-clipboard`が成功。専用CAのDPAPI保持、独自CA検証つき実TLS、Unicode往復、失効、公開証明書のみの取得、非表示のジェスチャー受け取りwindow登録を確認しました。実クリップボード・信頼ストア・入力は変更しません。`artifacts/201-final-verify-clipboard/clipboard-verification.json`。
- 圧縮版の専用コピーで実プロセス更新の5ケースが成功。正常な差し替え・起動確認、起動失敗時の旧版復旧、破損拒否、exe占有時の復旧、700ms占有した起動確認ファイルの競合回復を確認しました。通常のアプリ・設定は変更しません。`artifacts/201-final-verify-self-update/self-update.json`。
- 単体exeはv2.0.0の203,303,812バイトから約84.6MBへ縮小（約58％減）。非捕捉の起動プローブ各3回の中央値は旧1054ms／圧縮1794msで、約740ms増加しました。これはこのPCでのAPIプローブを含むプロセス時間で、一般的なGUI起動時間の保証ではありません。`artifacts/201-startup-size.json`。
- iPadの実座標は取得できず、iPadOSの加速やタッチ操作による推定ずれは残ります。**iPad実機での斜め復帰・境界位置・操作感と、実無線経由の同時ダウンロードの最終受け入れは未実施です。** 取得速度はネットワークにも依存します。更新再開は同じ取得処理内で最大2回です。以下は過去版の検証履歴です。

## v2.0.0 / 2.0.0

検証日: **2026-10-31 / Asia/Tokyo**。

- **305成功、0失敗、0スキップ**、警告0／エラー0。29件の共有テストで、実TLSの独自CA検証、POST／Origin／Host、Cookie保護、一度限りの接続、5回制限・有効期限・失効、未認証拒否、UTF-8の両方向上限、空文字／日本語／中国語／絵文字、HTTPでclipboardが読めないこと、不正ヘッダー・本文・転送形式、public/wildcard bind拒否、停止を確認しました。
- 自己完結exeのGUIは**3言語・42画像・Success=True**。共有はGUI検証中オフ。状態カードは実際に表示して、work area内の配置、taskbar回避、foreground不変、NOACTIVATE／LAYERED／TRANSPARENT／TOOLWINDOWを確認し、ライト／ダークを目視しました。`artifacts/106-final-gui/gui-smoke.json`。
- `--verify-clipboard`は専用データとloopbackだけを使用。DPAPIで保護したCAの再読込、独自CAを正しく検証したTLS、Unicode双方向、未認証拒否、session失効、公開証明書を確認。対応する実WinRT APIで、枠なしalpha=1の受け取りwindowを非表示・無効状態で登録／解除し、foreground不変を確認しました。`artifacts/106-final-clipboard/clipboard-verification.json`。実クリップボード・入力フック・HID公開は使用していません。
- Safari向けと同じHTML／CSS／JSをheadless Edgeで検証。3言語・12画像、接続、取得、コピー操作、送信、失効時の接続画面復帰、HTMLを文字として扱うこと、390pxでの横はみ出しなしを確認。通信APIとnavigator.clipboardはテスト用の模型で、実システムのclipboardと外部ネットワークへはアクセスしません。`artifacts/106-browser/browser-verification.json`。
- iPad実機のSafariでの証明書導入と受け渡し、実際のジェスチャー受信、複数DPI・全taskbar位置での最終受け入れは未実施です。手順はdocs/MANUAL_TESTS.mdへ記載しています。以下は過去版の検証履歴です。

## v1.05 / 1.0.5

検証日: **2026-10-31 / Asia/Tokyo**。

- **276成功、0失敗、0スキップ**、ビルド警告0／エラー0。更新のHTTP・metadata・checksum・破損拒否テストを新しいUniversalBridge.exe名で実行しました。
- 配布exeの製品名Universal Bridge、内部ファイル名UniversalBridge.dll、版番号1.0.5を確認しました。旧名exeを混入させた出力先でBuildとPackageがともに拒否することも確認しました。`artifacts/105-branding-verification.json`。
- AssemblyName変更後のGUIは3言語・33画像・Success=True。アイコン・タイトルバーテーマ・言語・設定の保存を確認しました。入力抑制・更新通信なし。`artifacts/105-local-gui/gui-smoke.json`。
- 専用コピーの実プロセス更新で、成功、起動失敗時復旧、破損拒否、exe占有時の元アプリ再起動が成功しました。起動確認ファイルの読み書き競合を検証中に再現し、完了後の公開と一時的な読取競合の再試行へ修正しました。追加した「起動確認ファイルを700ms排他的に占有する」ケースも成功し、誤ったロールバックはありませんでした。`artifacts/105-final-self-update/self-update.json`。
- 更新の実プロセス検証は同一バージョンの専用コピーを対象とします。普段の作業用アプリ、通常設定、入力、Bluetoothへは触れていません。v1.04以前は旧名を固定しているため、この移行は手動です。旧版からの自動移行成功を主張するものではありません。
- 日本語README.md／README.ja.mdの一致と、3言語の現行ダウンロード案内に旧exe名が残っていないことを確認しました。以下は過去版の検証履歴です。

## v1.04 / 1.0.4

検証日: **2026-10-31 / Asia/Tokyo**。

- 276件の自動テスト成功、3言語・33画像のGUI成功を維持しました。`artifacts/104-final-gui/gui-smoke.json`。
- 専用コピーのtarget exeを実際にFileShare.Readでロックし、差し替えに失敗しても元のSHAを再検証して旧アプリを再起動することを確認しました。親が終了していない場合やexeが外部変更された場合は再起動しません。SharingViolationRecovery=True、NormalAppTouched=Falseです。従来の成功・起動失敗時復旧・破損拒否も成功しました。`artifacts/104-local-verify-self-update/self-update.json`。
- v1.03の公開exeでは公開manifest・サイズ・SHA・asset digestの照合と、実際の自動更新経路によるダウンロードが成功しました。`artifacts/103-public-verify-update-feed/update-feed.json`。以下はv1.03の検証と過去の履歴です。

## v1.03 / 1.0.3

検証日: **2026-10-31 / Asia/Tokyo**。

- **276成功、0失敗、0スキップ**、ビルド警告0／エラー0。更新メタデータ、通常版／preview判定、版番号、固定URL・HTTPS、サイズ上限、asset digestとchecksumの整合、破損・短縮・過大なpayload拒否、既存ファイル保護、設定移行の32ケースを追加しました。`artifacts/103-final-build.log`。
- 自己完結exeのGUIは**3言語・33画像・Success=True**。ライト／ダークのDWM設定が成功し、タイトルバーを含むnative windowをPrintWindowで取得して目視しました。更新設定のオン／オフ保存、既存の感度・ショートカット・言語・アイコンも成功。GUI検証中は更新ネットワーク・HID公開・入力捕捉を行いません。`artifacts/103-final-gui/gui-smoke.json`。
- `--verify-self-update`では、専用コピーを実プロセスとして起動し、helperによる親終了待ち→File.Replace→再起動→起動確認、起動失敗→旧exe復元→旧版再起動、破損SHA拒否を成功確認しました。空白・日本語パス、名前を変えたexe、設定sentinel保持も確認しました。**同一バージョンのコピーを使うトランザクション検証**であり、通常の作業用アプリを更新したものではありません。`artifacts/103-local-verify-self-update/self-update.json`。
- 四方向復帰モデル・Win32入力ループ・斜め精度・高精度タイマーもSuccess=True。実カーソル移動・Bluetooth公開・入力抑制なし。`artifacts/103-local-runtime.json`。
- README.md／README.ja.mdの日本語内容が一致すること、README.en.mdへの英語分離を確認しました。将来の異なるバージョン間の実iPad接続中更新と、高コントラスト・各Windows環境での最終受け入れは`docs/MANUAL_TESTS.md`へ記載しています。以下は過去版の履歴です。

## v1.02 / 1.0.2

検証日: **2026-10-27 / Asia/Tokyo**。

- **244成功、0失敗、0スキップ**、ビルド警告0／エラー0。v1.01の「30＋20カウントの逆移動ですぐ復帰」を再現し、復帰しないことを確認しました。四方向の小さな往復・切り替え直後の大きな移動・単発フリック・静止後の小移動・到達パケットの距離持ち越しを検証し、19ケースを追加しました。停止／反転／沿辺移動／スクロール／キー／ボタンでのリセット、300／450／1500msの確認時間も検証しました。`artifacts/102-final-build.log`。
- 実際の送信タイマーと同様に空のflushを移動サンプル間へ挟み、タイマーだけで復帰しないこと、継続する意図的な押し出しでは復帰することを確認しました。これらは仮想時計とFakeTransportによるロジック検証であり、iPadの実位置を測定したものではありません。
- 自己完結exeの`--verify-edge-return`は四方向でEntryGraceProtected／SingleFlickProtected／SmallMovementProtected／SustainedPushRequired／IdleProtectedがすべてTrue。入力フック・BLE・実カーソル移動なしのモデル検証です。既存の画面端入力ループ・斜め精度・入力タイマー・単体タイマーの4モードもSuccess=True。`artifacts/102-local-runtime.json`。
- GUIは**3言語・27画像・Success=True**。復帰の説明と通常／オフ設定、推定範囲、言語、既存ショートカット・感度・アイコンの保存を確認。入力捕捉・HID公開なし。3言語の配置画面を目視しました。`artifacts/102-local-gui/gui-smoke.json`。
- **今回のiPad実機での操作感は未検証です。** 復帰は相対移動の推定であり、実座標の検出ではありません。従来より意図的な押し続けが必要です。Windows→iPadの処理と通常のHID移動量は変更せず、手動復帰・障害時の復帰も維持します。以下は過去版の検証履歴です。

## v1.01 / 1.0.1

検証日: **2026-10-25 / Asia/Tokyo**。

- **225成功、0失敗、0スキップ**、ビルド警告0／エラー0。接点機能の8テストを廃止し、四方向の自動復帰、旧設定移行、復帰オフ、キー／ドラッグ保持、解放／静止／スクロールによる誤復帰防止、部分portalと負座標、復帰位置・再進入防止を17ケース追加しました。
- 自己完結exeのGUIは**3言語・27画像・Success=True**。自動復帰オン／オフ、推定幅・高さ、ショートカット・感度・アイコン・言語の保存を確認。接点転送の設定と専用画像を削除しました。`artifacts/101-local-gui/gui-smoke.json`。
- `--verify-edge-return`は入力フック・BLEなしのモデル検証です。旧JSONの推定復帰オフを読み込んだ状態で四方向の復帰、ドラッグ保護、新しい移動の必要性、Windows側32px内側、report kindがkeyboard/mouseだけでmap144バイトであることを確認しました。`artifacts/101-local-verify-edge-return/edge-return.json`。
- 既存の`--verify-edge-input`（実Win32ループ・仮想カーソル／準備状態）、`--verify-pointer-precision`、`--verify-motion-input`、`--verify-motion-timer`もSuccess=True。入力抑制・実カーソル移動・Bluetooth公開なし。タイマー64サンプルの中央値8.3023ms、P95 8.5235msでした。ローカルの計測値で、無線遅延ではありません。`artifacts/101-local-runtime.json`。
- **iPad実機での逆方向の境界位置・操作感は未検証です。** 相対量からの推定であり、iPadOSの加速・タッチ操作によるずれは残ります。GUIの推定範囲で調整でき、従来の復帰キー／Esc3回も利用できます。以下は過去版の検証履歴です。

## v1.00 / 1.0.0

検証日: **2026-10-24 / Asia/Tokyo**。通常リリースですが、純正との同等性やiPad実機での最終受け入れを確認したものではありません。

- **216成功、0失敗、0スキップ**、ビルド警告0／エラー0。端の到着帯、接続確認中の揺れ、四方向、キャンセルの不可逆性、隣接画面、斜めの累積誤差、停止、線形転送、追加7操作のHIDと解放、設定移行を検証しました。`artifacts/v1-final-tests.log`。
- 人工入力1000サンプルの浅い高速斜め移動では、短い軸は旧方式0、新方式249、数学的基準248.6907369792391カウント。累積誤差最大0.499930763カウント、停止後の100回は出力なし。これはiPad画面や無線遅延の測定ではありません。`artifacts/v1-verify-pointer-precision/pointer-precision.json`。
- 実Win32入力ループで仮想配置・カーソル・入力準備状態を使い、到着帯での待機と接続準備の一時喪失後の取り消しを2回の要求で検証し、成功しました（CursorScripted=True、InputReadinessScripted=True）。入力捕捉・Bluetooth公開・実カーソル移動は行いません。実iPadのハンドオフ成功を表すテストではありません。
- GUIは**3言語・30画像・Success=True**、追加ショートカットの編集／保存、従来キー・重複拒否・画面端設定・アイコンを検証しました。`artifacts/v1-final-gui2/gui-smoke.json`。
- iPad上の操作感、実際の無線遅延、純正との比較、追加キーの全対応アプリでの結果は未確認です。既存のキーボード・マウス成功報告とは区別します。以下は過去版の結果です。

## 0.5.1の検証履歴

検証日: **2026-10-24 / Asia/Tokyo**。

## 0.5.0のiPad操作キーと画面端切り替え

- **自動テスト192成功、0失敗、0スキップ**。ビルド警告0／エラー0。新しい操作キーの標準HID送信、修飾キーの分離と復元、長押し1回、Windowsモード／無効時の通常キー、他キー／ドラッグ中の拒否、旧設定の競合移行、送信異常・世代破棄を検証します。`artifacts/actions-edge-0.5.1-build.log`。
- 画面端の到着後に移動通知なしでも待機を完了すること、四方向・負座標・再試行抑制・待機取り消し・部分隣接モニター・ドラッグ配置を検証します。静止中にタイマーだけで待機を開始しないことも確認します。
- 入力抑制・HID公開なしのGUIテストで**3言語・27画像・Success=True**。操作キーの編集／保存／重複拒否と有効化、画面端オン／オフ・待ち時間・HID待ち表示を確認します。設定画面の改行・保存欄は目視します。`artifacts/actions-edge-final-smoke-test/gui-smoke.json`。
- 実Windowsの非捕捉プローブで入力ループ応答・Raw Input登録・フック解除が成功し、200ms中25回のタイマー通知を確認しました。8msタイマーは中央値8.2301ms、P95 8.795ms、停止・再開が成功しました。入力抑制・HID公開なし。実Bluetoothによるキー操作や、物理マウスでiPadへ切り替わる結果は未確認です。ホームは⌘H互換であり、現行Globe+H対応を偽装しません。iPad上の実座標へのワープも提供しません。以下は過去版の結果です。

## 0.4.0の検証履歴

検証日: **2026-10-24 / Asia/Tokyo**。実Windowsでの結果とiPad実機の未確認部分を区別します。

## 0.4.0の検証履歴（接点転送はv1.01で削除）

- **173成功、0失敗、0スキップ**、ビルド警告0／エラー0。速度制限の方向・細かい移動・停止後の尾なし・線形モード、スクロール初動、127カウントまでの合流、contact descriptor／エンコード／固定スロット／指離れ、送信待ちで座標更新を置換し接点遷移・キー順序を保つこと、3種類のneutralと準備状態消失時の復帰を検証しました。`artifacts/native-gesture-publish.log`。
- 自己完結exeのGUIは**3言語・24画像・Success=True**。急な移動の抑制と接点モードの保存、再起動要求、アイコン・既存感度の保存も成功。接点説明画面を3言語で目視しました。入力抑制なし、HID公開なし。`artifacts/native-0.4.0/smoke-test/gui-smoke.json`。
- 接点APIプローブはAvailable=True、POINTER_INFO 96バイト／POINTER_TOUCH_INFO 144バイトを確認しました。これはAPIの存在とABIの検証です。物理的な接点取得やiPad認識の実証ではありません。`artifacts/native-0.4.0/probe-touchpad/touchpad-probe.json`。
- 8msタイマー・64サンプルの中央値**8.1933ms**／P95 **8.7206ms**。停止・再開成功。実入力ループも24通知、Raw Input登録・応答・フック解除成功。入力抑制・Bluetooth公開なし。無線入力遅延を測った値ではありません。
- 接点Report Mapは503バイト（上限512）、入力28バイト、feature2バイト。既定の互換モードは従来144バイトのままです。接点モードは初期値オフです。
- **iPad実機の新しいカーソル操作感・スクロール・接点モード認識／ピンチ／スワイプ／慣性は未確認です。** Windowsの接点取得はタップ／長押しを網羅せず、Magic Trackpad完全互換ではありません。実機受け入れは`docs/MANUAL_TESTS.md`へ記載しています。以下は過去版の検証履歴です。

## 0.3.4の検証履歴

検証日: **2026-10-24 / Asia/Tokyo**。このレポートは実行した検証と未検証部分を分けて記載します。

## 0.3.4の斜め移動・送信周期最適化

- **155成功、0失敗、0スキップ**。各象限・浅い角度・低感度で非飽和時の位置誤差が各軸±0.5カウント以内に収まること、停止後の振動がないこと、大移動の方向と総移動量、反転・クリック・キー・スクロールの順序、Windows復帰時の破棄を検証しました。`artifacts/diagonal-build.log`、`artifacts/diagonal-publish.log`。
- 旧実装と同じ誤差補正付き切り捨てを参照として、1000個の人工的な(0.13, 0.037)カウント移動を比較しました。小さい方の軸の最初の反応は28→14サンプルへ短縮し、RMS位置誤差は約0.81→0.41カウントへ減少しました。これは整数化処理の比較で、iPad画面や実際の無線遅延を測った値ではありません。`artifacts/diagonal-motion-comparison.json`。
- Windowsの高精度タイマーは8ms設定・64サンプルで中央値**8.2064ms**、95パーセンタイル**8.5736ms**、最大**8.8027ms**。停止・再開も成功しました。入力フック・Bluetooth公開なし。`artifacts/diagonal-verify-motion-timer/motion-timer.json`。
- 新しいメッセージ待機ループを実Windowsで検証し、200msの試験中にタイマー通知24回、ループ応答・Raw Input登録・フック解放が成功しました。通常アプリと異なる一時ショートカットを使用し、入力抑制・Bluetooth公開は行いません。`artifacts/diagonal-verify-motion-input/motion-input.json`。
- 警告0／エラー0で自己完結exeを生成し、GUIは**3言語・21画像・Success=True**。アイコンと感度保存も成功しています。`artifacts/diagonal-smoke-test/gui-smoke.json`。
- 8msはローカルタイマーの最短設定です。実際のBLE接続間隔・iPadのカーソル加速・表示更新は別の制約です。物理マウスを使った斜め移動・ドラッグ・操作感と無線遅延は更新後の実機確認が必要です。以下は過去版の検証履歴です。

## 0.3.3の接続済みなのに切り替えできない問題

- 稼働中0.3.2の診断で、ペア・接続・両購読が揃っている状態の切り替え拒否が`InputHeld`であることを確認しました。今回の症状をBluetooth接続断とは断定しません。
- 半角／全角キーがVK_DBE_SBCSCHAR／DBCSCHARとして届き、解放を観測できないままWindowsへ戻り、そのIMEモード値が立ち続けるケースを追加しました。修正前は2ケースとも失敗し、物理OEMキーへのポーリング対象の正規化後に成功しました。実際にOEMキーを押している場合、未対応の実キー、リモート操作中は押下判定を維持します。
- **141成功、0失敗、0スキップ**。新規6件に上記の回帰・実押下の拒否・100回の混合キー／緊急復帰とキー解放欠落・未対応キーとリモート状態の維持を含みます。`artifacts/input-return-build.log`。
- 最終ビルド・自己完結publishは警告0／エラー0。`artifacts/input-return-publish.log`。入力を抑制しないGUIスモークテストは**3言語・21画像・Success=True**、アイコン・感度保存も成功しました。`artifacts/input-return-gui/gui-smoke.json`。
- 拒否時と診断書き出しに追跡キー件数・解放待ち件数・マウス状態を追加しました。文字列・キー名・HIDペイロードは含めません。iPad実機で長時間使用した際の再発防止は、ユーザーによる更新後の確認が必要です。以下は過去版の検証履歴です。

## 0.3.2のスリープ復帰と日本語キー

- **135成功、0失敗、0スキップ**。新規15件でSuspend／Resumeと接続終了、端末別の状態、送信失敗後の明示的な復帰、片方だけの解放送信時の拒否、未完了のタイムアウト送信、復帰中の取消、半角／全角のControl+Space化・長押し・Alt復元・USキーとWindows操作の維持を検証しました。`artifacts/sleep-jis-build.log`、`artifacts/tests/tests.trx`。
- ビルドは警告0／エラー0、Windows x64の自己完結exeを生成しました。入力抑制なしのGUIスモークテストは**3言語・21画像・Success=True**。アイコン、感度保存、ショートカット欄を含みます。`artifacts/sleep-jis-gui/gui-smoke.json`。
- 実Windowsの読み取り専用プローブ10回すべてで**KeyboardType=7（日本語キーボード）、PhysicalIdle=True**。IMEモード状態2個を押下と誤認しないことも継続確認しました。Bluetooth公開・フック・入力抑制なし。`artifacts/sleep-jis-readiness/input-readiness.json`。
- iPad画面での実入力、物理スリープ復帰、日本語／英語IME切り替え自体は未検証です。`docs/MANUAL_TESTS.md`に実機での追加確認項目を記載しました。以下は過去版の検証履歴です。

## 0.3.1の2本指スクロールとアイコン

- 自動テストは**120成功、0失敗、0スキップ**。従来107件に、累積移動の初回／最終更新、重複通知、合流した入力と分割入力の合計一致、逆方向、不正値、感度の範囲と旧設定の移行、ホイールとの独立、小数蓄積・復帰時の破棄を追加しました。`artifacts/scroll-icon-build.log`。
- 警告0／エラー0でビルドし、Windows x64の自己完結exeを生成しました。`artifacts/scroll-icon-final-publish.log`。
- GUIスモークテストは**3言語、全4画面、スクロール設定、ショートカット欄、ライト／ダーク、21画像、Success=True**。数値4.50の保存とスライダー反映、推奨3.00への復元、ウィンドウ／トレイの新アイコン読み込みを確認しました。`artifacts/scroll-icon-gui/gui-smoke.json`。
- 2本指の変換は旧版の固定係数0.4に独立した感度を追加し、初期値3.00としました。送信レポート長は変更していません。実際の指の動き・iPad側のスクロール量はこの自動テストでは測定していません。
- 以下の0.3.0以前の結果は過去版の検証履歴です。

**0.3.0は107件の自動テスト、警告0／エラー0のビルド、自己完結exeの生成に成功しました。日本語・英語・中国語（簡体字）の247項目についてキー網羅・書式引数を確認し、言語設定の保存と旧設定の移行を検証しています。GUIでは全4画面・ショートカット欄・ライト／ダークの18画像を生成し、実際の選択変更ハンドラーによる即時反映と保存を確認しました。入力抑制・Bluetooth公開なしの確認であり、新たなiPad実機入力の検証ではありません。**

## 0.3.0の多言語化と配布

- xUnit: **107成功、0失敗、0スキップ**。`artifacts/localization-publish.log`、`artifacts/tests/tests.trx`。
- GUI: **ja／en／zh-Hans、全4画面・入力設定下部・ダッシュボードのダークテーマ、18画像、Success=True**。`artifacts/localization-gui/gui-smoke.json`。画像を目視し、英語・中国語の改行、キー編集欄、ライト／ダークを確認しました。
- 動的リソースのナビゲーション・適用ボタン、ジェスチャー選択肢、トレイメニュー、保存言語が選択言語と一致することをGUIスモークテスト内でも検証しています。
- 公開READMEは英語・日本語・簡体字で機能概要、ダウンロード、利用手順、制約、ビルド・検証、MIT／出典を記載しています。
- 以下の0.2.0以前の実アダプター／入力経路／iPad報告は過去版の検証履歴です。物理トラックパッドの実際のジェスチャー、全iPadOS／機器、長時間動作は未完了です。

## 0.2.0のジェスチャー取得・GUIショートカット

通常の低レベルフックを経由しないWindows Precision Touchpad入力を受け取るため、対応ウィンドウへのWM_POINTERとTouchpadGesturesControllerを追加しました。iPad操作中だけ小さな受け取り画面を前面に置き、前面を取得できない場合・別画面へフォーカスが移った場合はWindowsへ戻ります。2本指の縦横移動はスクロール、ピンチ・3～5本指操作は設定したHIDキー操作へ変換します。ネイティブのiPadマルチタッチ接点転送は実装していません。

このPCの読み取り専用プローブで対応APIの存在とIsSupported=Trueを確認しました。実入力を抑制しないプラットフォーム検証では、コントローラーの起動・解除、ショートカットの即時再登録と元のキーへの復元、他スレッドで予約したキーとの競合時のロールバックが成功しています。Windowsのドライバー・OSジェスチャー設定・ペアリング登録は変更していません。APIはMicrosoftが実験的とする機能で、全Windows 11環境への対応を意味しません。

通常起動のGUIでも「入力設定 → ショートカット」でCtrl+Alt+IをCtrl+Alt+Jへ変更し、「保存・適用しました。」と設定ファイルへの保存を確認しました。Ctrl+Alt+Jで実際に入力モードが変わり、ジェスチャー受け取り画面が前面になりました。Windows画面へフォーカスを戻すとWindows操作へ戻りました。キーは確認後に初期値へ復元済みです。ユーザーが調整した感度0.84と配置・自動切り替え設定は引き継いでいます。物理的な3本指入力やiPad画面上の結果を自動検証したとは扱いません。

## 0.1.2のマウス精度・切り替え操作の改善

マウス移動の既定を、Windowsカーソル座標からRaw Inputのデバイス相対量へ変更しました。Windowsの加速やカーソル再センタリングの影響を移動量に含めません。旧方式は互換入力として残し、登録失敗やrawパケット未受信が続く場合に切り替えます。絶対位置型の機器はデバイス別に基準を保持して変換します。

低感度0.1で10個の1カウント移動を蓄積した際に、浮動小数点誤差で1カウントの送信が次まで遅れるケースを修正しました。正負方向・0.1/0.05/0.02の感度・異なる送信間隔で合計移動量を検証しています。GUIは感度0.02～4.00、数値入力、0.01の増減に対応しました。

Ctrl+Alt+IはiPadへの切り替え専用として、登録結果を確認し画面・ログに表示します。iPadモード中の再押下でIを転送せず、Windowsへの復帰はCtrl+Alt+Qです。

実Windowsの検証でRaw Input登録・解除とiPad切り替えショートカット登録は成功しました。自動の入力フック検証中のRawPacketsObservedは0であり、物理マウスの生移動量やiPad画面上の精度向上を自動テストで実測したとは扱いません。

通常起動した改善版でも、相対入力が有効であること、感度を0.10→0.09へ下げ、数値入力で0.10へ戻して保存できることをGUIで確認しました。ペア・両購読済みのiPadを選択し、Ctrl+Alt+IでiPadモードへ、Ctrl+Alt+QでWindowsへ戻ることを確認しました。入力内容は送信していません。記録は `artifacts/fix-0.1.2-live-check.json`。この確認時の感度0.10と配置・自動切り替え設定を引き継ぎました。その後のユーザー調整は0.2.0で保持しています。

## 0.1.1の入力切り替え修正

ユーザーログと稼働中の0.1.0を確認し、ペア済み・両レポート購読済み、送信済み0件で切り替えが失敗していることを確認しました。このPCでは文字キーやマウスボタンを押していなくても仮想キー0xF0・0xF4の上位ビットが継続して立っていました。全仮想キーを調べる旧判定はIMEモード状態も押下とみなし、切り替え前に拒否していました。

修正後は物理キー・マウスに限定したポーリングとフック側の押下追跡を併用します。未対応の実キーを含む押下中の拒否、切り替え後の再確認、復帰要求による取消は維持しています。拒否理由の画面・ログ表示、iPadのHID一時停止表示も追加しました。

`artifacts/fix-0.1.1-readiness/input-readiness.json` の10サンプル全件で `AllVirtualKeysIdle=false`、`ImeModeStatesActive=2`、`PhysicalIdle=true`、`OnDefaultDesktop=true` を確認しました。フック・広告・入力抑制なしの実Windows測定であり、iPad入力成功の測定ではありません。

通常起動した0.1.1でもiPadのペア・キーボード／マウス購読が揃い、16:29:29にアプリの入力先が「iPad / 選択端末」「iPadへ入力を送信中」へ変わったことをWindows GUIと診断ログで確認しました。その後「Windowsへ戻す」でWindowsモードへの復帰を確認しました。この時点ではiPad画面自体を観測していません。後続のユーザー報告でマウス操作・キーボード入力の成功が確認されました。記録は `artifacts/fix-0.1.1-live-check.json`。

## 環境と成果物

- Windows x64、OS build **10.0.26300.0**。
- Microsoft .NET SDK **10.0.402**、ランタイム **10.0.13**。
- コンパイル対象: Windows SDK .NET projection **10.0.26100.87**。最低対応OSはbuild22000、ジェスチャーは26100以降でAPI対応判定。
- アダプター: Windows APIが返した名前はBluetooth、無線On、LE=True、Peripheral=True。
- 実行プロセスは管理者トークンではありません。OSのセキュリティ設定、Bluetoothドライバー、既存ペアリングを変更していません。
- 配布版: `dist/UniversalBridge-0.3.0-win-x64/UniversalControl.exe`、ランタイム同梱、Windows x64。旧版は別フォルダに保持。
- 本実行環境の結果であり、ユーザー指定の全構成・iPad実機の検証を代替しません。

## 実行結果

| 検証 | 結果 | 証拠 |
|---|---|---|
| Release solution build | 成功、警告0／エラー0 | `artifacts/localization-publish.log` |
| xUnit | **107成功、0失敗、0スキップ** | `artifacts/tests/tests.trx`、`artifacts/localization-publish.log` |
| win-x64自己完結publish | 成功 | `artifacts/localization-publish.log` |
| ジェスチャーAPI対応判定 | IsSupported=True、Windows build26300 | `artifacts/touchpad-capabilities/touchpad-probe.json` |
| ジェスチャーコントローラー起動／解除・ホットキー即時変更・競合復元 | すべて成功、入力抑制なし | `artifacts/platform-latest/verify-local-input/input-verification.json` |
| GUIでのキー変更・保存・新キーでの切り替え、フォーカス喪失時の復帰 | 成功、Windows側の状態を観測 | `artifacts/fix-0.2.0-live-check.json` |
| IME状態による未押下の誤判定修正 | 実Windows上で10回とも旧判定と修正後の差を確認 | `artifacts/fix-0.1.1-readiness/input-readiness.json` |
| 通常版でのiPad入力モード・Windows復帰 | 成功。ペア済みの実iPadを対象に、Windows側の状態遷移を観測 | `artifacts/fix-0.1.1-live-check.json` |
| 0.1.2の感度調整・Ctrl+Alt+I/Qによる切り替え | 成功、設定保存・キーによるモード変更をWindows側で確認 | `artifacts/fix-0.1.2-live-check.json` |
| 実アダプター／HID service作成 | LE / Peripheral=True、0x1812=Success | `artifacts/platform-latest/probe-gatt/adapter-probe.json` |
| 実HID公開・停止・再公開・解放 | 成功、6 characteristics、Restarted=True、CleanupCompleted=True | 同フォルダ `verify-server/server-verification.json` |
| Win32フック・Raw Input・切り替えショートカット登録／解除 | 成功、InputSuppressed=False | 同フォルダ `verify-local-input/input-verification.json` |
| GUI起動・全4画面・ライト／ダーク・設定再読込 | 成功、5画像生成、入力抑制なし | 同フォルダ `smoke-test/gui-smoke.json`、`screenshots` |
| テスト専用プロセス強制終了後のフック／ホットキー再取得 | 成功、入力転送なしで実行 | `artifacts/crash-latest.json` |
| 実GUI操作 | ナビゲーション、配置図ドラッグ、感度変更、テーマ切り替え、正常終了を確認 | Windows Computer Useによる目視・操作、`artifacts/ui-review/settings.json` |
| iPadのペアリング・アプリでの認識 | **ユーザー報告で成功、稼働中GUIでもペア・両購読を確認** | 今回のユーザー報告／ログ |
| 0.1.1のiPadでのキー・マウス入力 | **ユーザー報告で成功** | マウス精度改善の要望あり |
| 0.1.2の相対マウス入力の操作感 | **実機比較が必要** | `docs/MANUAL_TESTS.md` に受け入れ手順 |

公開検証はアプリに暗号化必須属性を作成した確認です。暗号化リンクの成立・iPadによるレポート解釈までを検証したものではありません。

## 自動テストの範囲

- キーボード8バイト／マウス7バイト（横スクロールAC Panを追加）、符号付きlittle endian、範囲制限、不正レポート拒否、6キー超過ErrorRollOver。
- 変更可能なショートカットの検証・左右修飾キー一致・旧設定の移行、ジェスチャー方向判定・ピンチ量蓄積、キー変換後の解放、キー／ボタン保持時のジェスチャー抑止、縦横スクロールの小数保持と復帰時のクリア。
- descriptorを解析したreport長、JISのusage範囲、左右修飾キー、JIS／テンキー／OEM物理キーの変換。
- 画面端の押し続け、負の原点、複数ディスプレイの共有境界、4方向、切り替え範囲、再進入抑止。
- 仮想カーソルの復帰ヒステリシス、小数感度、高分解能ホイール、大きい相対量の分割保持。
- 未接続・未起動フック・ローカル押下中に入力を抑制しないこと。
- IMEモードが継続していても未押下なら切り替え可能。文字・修飾・JIS変換・マウス・メディア等の実押下は引き続き拒否。接続と入力監視の拒否理由、切り替え処理の二重起動・途中取消・再確認。
- キーの押下／解放、リピート、クリック押下／解放の順序保持、正常復帰時のneutral reports。
- キー保持中の切断、キー解放で発見した切断、送信停止中の緊急復帰、Escの3回押下、キュー上限、通知タイムアウト。
- 復帰中のactivation取消、古いgenerationの待機入力失効、購読が一部残る状態での解放、別デスクトップで解放されたキーの整合。
- 再接続時に入力を自動で再捕捉しないこと、再試行の指数バックオフと上限。
- 設定の保存・再読込、破損ファイルを保持した復旧、不正値の正規化。
- 20万件の移動イベント蓄積と、分割した相対量の合計一致。

## 性能測定

| 測定対象 | 測定結果 | 範囲 |
|---|---|---|
| CoreのMove蓄積処理 | 200,000 events / 27.0447ms、平均約0.135µs/event | FakeTransport、フック・キュー送出・OSスケジューリング・Bluetoothを除く |
| 同ループの割当 | 40 bytes | `GC.GetAllocatedBytesForCurrentThread` の差分 |
| 開発版GUIプレビューの待機時 | 約1.24%（1コア換算）、Working Set約169.8MiB、Private約76.8MiB | 5.02秒のサンプル。フック・無線入力転送なし |

根拠は `artifacts/local-performance.json` と `artifacts/gui-idle-performance.json`。短時間のローカル測定であり、実際のiPad入力遅延、転送中のCPU、電池消費、長時間メモリ安定性の評価ではありません。

## 機能の最終分類

| 機能 | 分類 | 補足 |
|---|---|---|
| 内蔵Bluetoothの検出・Peripheral判定 | **実機動作確認済み** | Windows側 |
| GATT HIDの作成・公開・停止・再公開 | **実機動作確認済み** | Windows側、iPad接続なし |
| 入力フック開始・正常解除・強制終了後の再取得 | **実機動作確認済み** | 入力転送しない状態での確認 |
| GUI全4画面、テーマ、配置ドラッグ、感度、設定保存 | **実機動作確認済み** | Windows GUIで操作 |
| HIDエンコード、JISマッピング、画面端・仮想カーソル・入力所有権 | **自動テスト確認済み** | Coreの決定ロジック |
| 緊急復帰、切断時復帰、解放レポート、キュー異常、再試行方針 | **自動テスト確認済み** | モック通信で確認。iPad転送中は未検証 |
| iPadのペアリング・アプリでの認識 | **実機動作確認済み（ユーザー報告）** | 0.1.0。入力切り替えは失敗と報告 |
| iPadのキーボード入力・マウス操作 | **実機動作確認済み（ユーザー報告）** | 0.1.1。精度改善の要望を受け0.1.2を実装 |
| 0.1.2の相対マウス入力、全キー、日本語IME、個別のクリック・スクロールの受け入れ | **実装済み・実機未検証** | 新方式の操作感と詳細な受け入れ一覧は人による確認が必要 |
| 実際の画面端転送、混合DPI、推定自動復帰、実際の無線再接続 | **実装済み・実機未検証** | 判定ロジックのテストとは区別 |
| トレイ常駐・サインイン時自動起動 | **実装済み・実機未検証** | 自動起動設定を検証目的で変更していない |
| GUIショートカット編集・即時適用・保存・競合復元 | **Windowsで動作確認済み** | 許可した修飾キーと英数字・F1～F24 |
| タッチパッド取得ウィンドウ、コントローラーの起動・解除 | **Windowsで動作確認済み** | 実際の複数指入力は未検証 |
| 複数指の操作変換、ピンチ変換、横スクロール | **実装済み・iPad実機未検証** | Coreロジックは自動テスト済み。iPadOS・アプリのキー対応に依存 |
| メディア／Fn、追加ボタン、iPadネイティブマルチタッチ、インストーラー・署名 | **未実装** | 対応範囲はKNOWN_LIMITATIONS参照 |
| 標準HID経由でiPadのカーソル絶対位置を直接取得 | **技術的制約により実現不可** | 相対量の推定を実装 |

## 検証で見つかった問題と修正

最初のWindows公開テストで、停止直後にOSの状態表示が更新されず、終了処理がStopAdvertisingを重複して呼ぶとInvalidOperationExceptionになる問題を検出しました。広告開始要求の追跡、重複停止の抑止、再公開前の停止待機を実装し、同じproviderでの公開→停止→再公開→終了を再検証して成功しました。初回の失敗記録は `artifacts/server-verification/verification-error.json` に保持しています。

ソース確認では、接続切断をキー解放イベントで発見した際の押下追跡、キュー異常後の解放、クリックの順序、遅いAPIを入力フックから除外する経路も修正し、回帰テストを追加しました。GUIではテーマに追従するコンボボックス、ページ移動時のスクロール位置、正規化後の設定表示を確認しました。

## 未完了の受け入れ

修正後のiPad画面上の入力は人による実機操作が必要です。`docs/MANUAL_TESTS.md` を実行し、結果を追記してください。ビルド・自動テスト・Windows側公開の成功だけを理由に、iPadOSで利用可能と判断してはいけません。
