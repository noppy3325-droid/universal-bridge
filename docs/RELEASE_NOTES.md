# Universal Bridge 0.5.0 — Preview

## English

- Adds dedicated iPad-only action shortcuts: **Ctrl+Alt+H Home (⌘H compatibility), Ctrl+Alt+S Search, Ctrl+Alt+N Next app**. Edit all five bindings in Input settings and apply; duplicates are rejected. Holding a trigger runs it once and keeps local Ctrl/Alt out of the command. Actions can be disabled and remain ordinary keys on Windows.
- Finishes edge switching: arriving at the edge starts the wait and a timer completes it even when the mouse stops. Display layout exposes the 150–1500ms delay (default 300ms), enable checkbox and readiness reason. Drag the iPad to any of four sides. The preview marks only segments not shared with another Windows monitor.
- Cancels pending edge activation when the pointer leaves, a key/button is pressed, settings change or readiness is lost. Reconnection or remaining at an edge does not automatically recapture input.
- 192 automated tests and three-language GUI verification. New actions and physical edge switching still need iPad hardware confirmation. Home uses ⌘H rather than Apple's Globe key; compatibility depends on iPadOS/settings. The iPad cursor resumes at its previous position, with no absolute coordinate warp.

Exit the old app, extract UniversalBridge-0.5.0-win-x64.zip and run UniversalControl.exe. Existing settings and the HID report format are retained. Enable edge switching in Display layout after connecting/selecting iPad. Windows 11 x64 / BLE Peripheral; unsigned, self-contained preview.

## 日本語

- iPad操作中だけの専用キーを追加：**Ctrl+Alt+Hでホーム（⌘H互換）、Ctrl+Alt+Sで検索、Ctrl+Alt+Nで次のアプリ**。「入力設定」で切り替え・復帰を含む5組を編集・適用でき、重複を拒否します。長押しは1回、Ctrl／Altを変換先へ混ぜず解放します。専用キーはオフにでき、Windowsでは通常キーとして使えます。
- 画面端に到着後、手を止めてもタイマーで待機を完了して切り替えます。「ディスプレイ配置」でオン／オフ、150～1500msの待ち時間（初期値300ms）、準備状況を表示。iPadの図は上下左右へ配置でき、隣接Windows画面と重ならない有効範囲だけを青く描きます。
- 待機中の端からの離脱、キー／ボタン入力、設定変更、準備状態の消失で取り消します。再接続や同じ端での静止だけで再捕捉しません。
- 自動テスト192件、3言語GUI検証。新しい操作キーと物理的なiPad切り替えは実機確認が残ります。ホームはApple固有Globeではなく⌘Hの互換経路で、iPadOS／設定に依存します。iPadカーソルは前回位置から操作し、実座標へのワープはしません。

旧版を完全終了しUniversalBridge-0.5.0-win-x64.zipを展開してexeを起動してください。既存設定とHID形式は引き継ぎます。接続・iPad選択後、配置画面で自動切り替えを有効にします。Windows 11 x64／BLE Peripheral対応、ランタイム同梱・署名なしのプレビューです。

## 简体中文

- 新增仅控制iPad时生效的操作键：**Ctrl+Alt+H主页（⌘H兼容）、Ctrl+Alt+S搜索、Ctrl+Alt+N下一个应用**。可在输入设置一起编辑五种快捷键，拒绝重复组合。长按仅执行一次，不将本地Ctrl／Alt混入目标命令。可独立关闭，控制Windows时保持普通按键。
- 完成边缘切换：到达边缘后即使停止移动，也会由定时器完成等待。显示器布局提供开关、150～1500ms等待时间（默认300ms）及就绪状态。iPad图可拖到上下左右，仅将不与其他Windows显示器相邻的有效区域画为蓝线。
- 离开边缘、按键／鼠标按钮、更改设置或失去就绪状态会取消待执行切换。重连或静止停留在边缘不会自动重新捕获。
- 192项自动测试及三语GUI验证。新操作及物理iPad切换仍需实机确认。主页使用⌘H兼容通道，不模拟Apple的Globe键，依赖iPadOS及设置。iPad指针从原有位置继续操作，不执行绝对坐标跳转。

退出旧版，解压UniversalBridge-0.5.0-win-x64.zip并运行exe，保留原设置及HID格式。连接并选择iPad后，在布局页面启用自动切换。需要Windows 11 x64及BLE外围支持，包含运行时的未签名预览版。
