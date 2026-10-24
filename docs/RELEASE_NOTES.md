# Universal Bridge 0.3.4 — Preview

## English

- Improves fine diagonal motion with nearest-count rounding and fractional carry. Accumulated error stays within ±0.5 count per axis when not saturated. Splitting large movement preserves direction with a shared XY scale.
- Adds a private high-resolution mouse timer, with an 8 ms local minimum when the BLE interval permits it. The input loop waits for both messages and timer signals. The timer stops on local return; system-wide timer resolution is unchanged, with a standard-timer fallback.
- Coalesces adjacent unsent XY motion under load. Keys, clicks, wheel/pan, reversals, target/epoch changes, and report limits remain barriers. Travel and ordering are preserved; old-epoch motion is discarded.
- Keeps existing sensitivity settings, three UI languages, shortcuts, scrolling controls, and icon. No smoothing filter, acceleration, or artificial momentum is added.
- **155 tests passed**. A synthetic diagonal trace reduced minor-axis onset from 28 to 14 samples and RMS count error from about 0.81 to 0.41. This PC's 8 ms timer probe measured median 8.2064 ms and P95 8.5736 ms. Native input-loop and three-language GUI checks passed. These are local/synthetic measurements, not iPad cursor-feel or wireless-latency measurements.

Exit the old app, extract UniversalBridge-0.3.4-win-x64.zip and run UniversalControl.exe. Settings are retained. Physical diagonal movement and dragging still need iPad verification. Requires Windows 11 x64 and BLE Peripheral support. Unsigned, self-contained preview.

## 日本語

- **斜めの微小移動を最適化。** 小数を持ち越す最近傍丸めで非飽和時の各軸の累積誤差を±0.5カウントへ抑え、大移動の分割でも方向を保ちます。
- マウス送信に専用の高精度タイマーを追加し、BLE間隔が許す場合のローカル最短周期を8msにしました。入力とタイマーを一緒に待ち、Windows復帰時に停止します。OS全体のタイマー分解能は変更せず、失敗時は標準方式へ戻ります。
- 送信待ちの隣り合うXY移動を合流します。キー・クリック・縦横スクロール・反転・端末／世代・レポート上限を越えてまとめず、移動量と順序を保ちます。古い世代の移動は再送しません。
- 保存済み感度、3言語、ショートカット、スクロール設定、アイコンを引き継ぎます。平滑化フィルター・加速・人工的な慣性は追加しません。
- **自動テスト155件成功**。人工入力の小さい軸の反応は28→14サンプル、RMS誤差は約0.81→0.41カウント。このPCの8ms設定は中央値8.2064ms、95パーセンタイル8.5736ms。入力ループと3言語GUIも成功しました。iPad画面や無線遅延を実測した値ではありません。

旧版を終了し、UniversalBridge-0.3.4-win-x64.zipを展開してexeを起動してください。設定は引き継ぎます。実際の斜め移動とドラッグの体感はiPad実機での確認が必要です。Windows 11 x64／BLE Peripheral対応が必要な、ランタイム同梱・署名なしのプレビュー版です。

## 简体中文

- 优化细微斜向移动：采用保留小数余量的最近整数舍入，未饱和时各轴累计误差控制在±0.5计数内，大位移拆分保持方向。
- 新增专用高精度鼠标定时器；BLE间隔允许时，本地最短周期为8ms。输入循环同时等待消息和定时器，返回Windows后停止，不改变系统全局分辨率，并保留标准定时器回退。
- 负载较高时仅合并相邻未发送的XY移动，不跨越按键、点击、滚动、反转、目标／世代变化或报告范围限制。保留位移与顺序，丢弃旧世代移动。
- 保留已有灵敏度、三语界面、快捷键、滚动设置及图标，不添加平滑滤波、加速或模拟惯性。
- **155项测试通过**。人工轨迹的小轴首次响应由28缩至14个采样，RMS误差约由0.81降至0.41计数。本机8ms定时器中位数为8.2064ms、P95为8.5736ms；输入循环与三语GUI验证通过。这些并非iPad实际操作感或无线延迟测量。

退出旧版，解压UniversalBridge-0.3.4-win-x64.zip并运行exe，原设置会保留。实际斜向移动和拖动仍需iPad实机确认。需要Windows 11 x64和BLE外围设备支持，此为包含运行时的未签名预览版。
