# Universal Bridge v1.00

## English

- Regular release. Display/tag: v1.00; executable/package version: 1.0.0.
- Fixes overly strict edge handoff validation. Physical cursor position is sampled after mouse processing; arrival and handoff use separate tolerance bands. Small inward jitter no longer cancels connection checks. Deliberate departure, input, settings/readiness changes and expiry still cancel permanently. Diagnostics distinguish cancellation reasons without coordinates.
- Preserves fractional XY errors after fast-motion stabilization, fixing the minor axis disappearing during shallow diagonals. A synthetic 1000-sample trace improved from 0 to 249 minor-axis counts against a 248.69-count reference, with cumulative error below 0.5 count and no motion after stopping. Direct mode remains available.
- Adds seven configurable actions: Previous app (Ctrl+Alt+P), Screenshot (C), Undo (Z), Redo (Y), Zoom in (U), Zoom out (D), and Close window (W, disabled by default), all with Ctrl+Alt. They join Home, Search and Next app. Apply to save bindings and per-action enable flags.
- 216 automated tests; three-language GUI checks. Standard HID format and existing settings are retained. Actual iPad cursor feel, wireless latency and parity with Apple's controls remain unmeasured. Home uses compatibility ⌘H; action support depends on iPadOS/apps.

Exit the old app, extract UniversalBridge-1.0.0-win-x64.zip and run UniversalControl.exe. Enable edge switching in Display layout after starting HID and selecting iPad. The iPad cursor resumes at its previous position. Windows 11 x64 / BLE Peripheral required; unsigned, self-contained release.

## 日本語

- 通常リリースです。表示・タグはv1.00、実行ファイル・配布物の内部バージョンは1.0.0です。
- 画面端の厳しすぎる位置確認を修正。マウス処理後の物理位置を読み、到着帯と接続確認中の許容範囲を分けました。小さな内側の揺れで取り消さず、明確な離脱・入力・設定／準備変更・期限切れは取り消します。診断は座標なしで理由を区別します。
- 高速移動の抑制後もXYの小数誤差を保持し、浅い斜め移動で短い軸が消える問題を修正。人工入力1000回で旧0→249カウント、基準248.69、累積誤差0.5カウント未満、停止後の移動なしを確認。線形モードも使えます。
- ホーム・検索・次のアプリに、前のアプリ（Ctrl+Alt+P）、スクリーンショット（C）、取り消す（Z）、やり直す（Y）、拡大（U）、縮小（D）、閉じる（W、初期値オフ）を追加。すべてCtrl+Altとの組み合わせで、GUIからキーと有効化を変更・適用できます。
- 自動テスト216件、3言語GUI確認。既存設定と標準HID形式を維持します。iPadの実操作感・無線遅延・純正との同等性は未測定です。ホームは⌘H互換、各操作はiPadOS／アプリに依存します。

旧版を完全終了し、UniversalBridge-1.0.0-win-x64.zipを展開してexeを起動してください。HID公開とiPad選択後、「ディスプレイ配置」で画面端切り替えを有効にします。iPadカーソルは前回位置から操作します。Windows 11 x64／BLE Peripheral対応、ランタイム同梱・署名なしの通常リリースです。

## 简体中文

- 正式发布。显示名及标签为v1.00，程序和压缩包内部版本为1.0.0。
- 修复边缘切换位置检查过于严格的问题。鼠标更新后读取物理位置，分别设置到达及交接容差，小幅向内晃动不再取消连接确认。明确离开、输入、设置／就绪变化及超时仍会永久取消。诊断区分原因，不记录坐标。
- 快速移动抑制后保留XY小数误差，修复浅斜向移动短轴消失。1000次人工输入由0改善为249计数，数学基准248.69，累计误差小于0.5计数，停止后无额外移动。仍可使用线性模式。
- 新增七项可配置操作：上一个应用（Ctrl+Alt+P）、截屏（C）、撤销（Z）、重做（Y）、放大（U）、缩小（D）及关闭窗口（W，默认禁用），均与Ctrl+Alt组合。与主页、搜索、下一个应用一起在GUI修改并应用保存。
- 216项自动测试及三语GUI验证。保留原设置及标准HID格式，尚未测量iPad实际操作感、无线延迟或与Apple原生设备的同等性。主页使用⌘H兼容方式，各操作依赖iPadOS／应用。

完全退出旧版，解压UniversalBridge-1.0.0-win-x64.zip并运行exe。启动HID并选择iPad后，在显示器布局启用边缘切换。iPad指针从原位置继续操作。需要Windows 11 x64及BLE外围支持，包含运行时的未签名正式版本。
