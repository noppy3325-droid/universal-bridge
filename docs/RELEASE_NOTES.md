# Universal Bridge 0.4.0 — Preview

## English

- Adds **Stabilize fast pointer movement**, on by default. A radial soft limit reduces sudden fast travel without replaying compressed motion later. Disable it in Input settings for linear transfer. Queued XY merging is capped at 127 counts per axis.
- Improves compatibility-mode two-finger scroll onset with nearest-tick rounding and fractional carry. Physical wheel behavior is unchanged.
- Adds **Contact relay for native iPad gestures (experimental)**, off by default. Relays real two-to-five-finger contacts through standard HID Touch Pad reports, with stable contact IDs, release frames and separate readiness checks. iPadOS performs gesture recognition; app scroll gain and gesture macros are bypassed.
- **iPad recognition and behavior are not hardware-verified.** Windows does not expose all tap/hold contacts; full Magic Trackpad parity is not supported. Enable the mode, fully exit/restart, and manually re-pair on iPad if its HID cache prevents recognition. Disabling also requires restart. Default keyboard/mouse mode keeps its existing descriptor.
- 173 tests passed; three-language GUI, API/ABI, native input-loop and local timer checks passed. These do not measure iPad cursor feel or wireless latency. No coordinates or input payloads are logged.

Exit the old app, extract UniversalBridge-0.4.0-win-x64.zip and run UniversalControl.exe. Settings are retained. Windows 11 x64 / BLE Peripheral required. Unsigned, self-contained preview. See README for experimental-mode requirements.

## 日本語

- **急な移動の抑制**を追加し初期値オンにしました。高速時の移動を方向を保って短くし、抑えた距離を停止後に流しません。入力設定でオフにすると線形転送へ戻せます。送信待ちのXY合流は各軸127カウントまでに制限します。
- 互換モードの2本指スクロールは小数持ち越し付き最近傍丸めにし、動き始めの反応を早めました。物理ホイールは従来方式を維持します。
- **iPad純正ジェスチャー向けの接点転送（試験版）**を追加しました。初期値オフです。実際の2～5接点を標準HID Touch Padとして送り、接点ID・指離れ・独立した準備判定を扱います。認識はiPadOSに任せ、アプリのスクロール感度・ジェスチャーキー変換は使いません。
- **iPad実機の認識・動作は未確認です。** Windowsではタップ／長押しの接点を網羅できず、Magic Trackpad完全互換ではありません。有効化後は完全終了・再起動し、認識しなければiPadで手動再ペアリングしてください。無効化時も再起動が必要です。既定のキーボード／マウスモードは従来descriptorを維持します。
- 自動テスト173件、3言語GUI、API／ABI、入力ループ、ローカルタイマー検証が成功。iPadの操作感や無線遅延の実測ではありません。座標・入力内容は記録しません。

旧版を終了しUniversalBridge-0.4.0-win-x64.zipを展開、exeを起動してください。設定は引き継ぎます。Windows 11 x64／BLE Peripheral対応が必要な、ランタイム同梱・署名なしのプレビューです。試験版の条件はREADMEを参照してください。

## 简体中文

- 新增**抑制快速指针移动**，默认开启。对高速位移施加保持方向的柔和限制，停止后不继续补发压缩的距离。可在输入设置关闭以恢复线性转发，待发送XY合并上限为每轴127计数。
- 兼容模式的双指滚动改用保留小数余量的最近整数舍入，改善初始响应。物理滚轮维持原有行为。
- 新增**iPad原生手势触点转发（实验性）**，默认关闭。通过标准HID Touch Pad发送真实双指至五指触点，保持触点ID、抬起帧及独立就绪检查。由iPadOS识别手势，不使用应用的滚动增益或手势按键映射。
- **尚未在iPad实机确认识别及行为。** Windows无法提供全部轻点／长按触点，不支持Magic Trackpad完全兼容。启用后完全退出并重启，必要时在iPad手动重新配对；关闭也需重启。默认键鼠模式维持现有descriptor。
- 173项测试、三语GUI、API／ABI、输入循环及本地定时器验证通过，并非iPad操作感或无线延迟测量。不记录坐标或输入内容。

退出旧版，解压UniversalBridge-0.4.0-win-x64.zip并运行exe。保留原设置，需要Windows 11 x64及BLE外围支持。此为包含运行时的未签名预览版，实验模式要求见README。
