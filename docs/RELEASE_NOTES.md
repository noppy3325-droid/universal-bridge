# Universal Bridge 0.3.1 — Preview

## English

- Faster two-finger scrolling: default gain is three times the previous default. Cumulative movement retains the start and final motion without counting duplicate samples twice.
- Dedicated **Two-finger scroll sensitivity**, independent of the mouse wheel: **0.25–12.00 ×**, default/recommended **3.00 ×**, with numeric entry, 0.25 steps, and reset. Existing mouse and wheel preferences are retained.
- New blue PC/tablet bridge icon in the executable, window, tray, and sidebar; nine Windows icon sizes. Artwork provenance and prompt are included in the source.
- Japanese, English, and Simplified Chinese remain supported; new controls are translated.
- **120 unit tests passed**. GUI verification produced **21 images in three languages**, including sensitivity editing/persistence/reset and icon loading. Physical iPad scroll feel still needs user acceptance.

Download the `win-x64.zip`, extract, and run `UniversalControl.exe`. Requires Windows 11 x64 and BLE Peripheral support. Touchpad capture needs compatible experimental APIs on 24H2 or later. Standard HID mappings do not transmit native iPad multitouch. Unsigned preview binary; no installer.

## 日本語

2本指スクロールの既定反応を旧版の3倍にし、動き始めと終了直前の移動量を保持しました。「入力設定 → 2本指スクロール感度」で0.25～12.00倍（初期値・推奨値3.00）をマウスホイールとは独立して調整できます。数値入力・0.25刻み・推奨値復元に対応します。PCとタブレットをつなぐ新アイコンをexe・ウィンドウ・トレイ・サイドバーへ追加しました。自動テスト120件、3言語21画像のGUI検証が成功しています。実際のiPadでの操作感は確認が必要です。

`win-x64.zip` を展開しexeを起動してください。Windows 11 x64／BLE Peripheralが必要です。ジェスチャーは24H2以降の対応する実験的APIが必要で、ネイティブ接点ではなくスクロール・キーへの変換です。署名なしのプレビュー版です。

## 简体中文

双指滚动默认增益提高至旧版的 3 倍，并保留手势开始和结束时的移动量。在“输入设置 → 双指滚动灵敏度”中独立调整0.25～12.00倍（默认／推荐3.00），支持数值输入、0.25步长和重置。新增连接电脑与平板的蓝色图标，应用于exe、窗口、托盘和侧栏。120项单元测试通过，三语GUI验证生成21张图像。iPad实际滚动体验仍需使用者确认。

下载并解压 `win-x64.zip`，运行 exe。需要 Windows 11 x64 和 BLE 外围设备支持。手势捕获需要 24H2 或更新系统的受支持实验性 API；仅转换滚动／按键，不传输原生多点触控。此为未签名预览版。
