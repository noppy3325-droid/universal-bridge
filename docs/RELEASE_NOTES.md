# Universal Bridge v1.02

## 日本語

- 244件の自動テスト、3言語GUI、入力捕捉なしの配布exe検証に成功しました。
- 小さな逆方向移動でWindowsへ戻るv1.01の誤復帰を修正しました。推定位置を中央から開始し、切り替え後1秒は自動復帰を抑止します。
- 推定境界の到達だけでは戻らず、Windows方向へ最短300ms以上・一定距離を動かし続けた場合だけ戻ります。配置の待ち時間が300msより長い場合はその値を使います。
- 単発フリック・細かな揺れ・停止後の小さな移動では復帰しません。停止・反転・沿辺移動・スクロール・キー／ボタン操作で確認をリセットし、境界超過量を持ち越しません。
- Windows→iPadの切り替え、感度、復帰キーは維持。自動復帰だけをオフにもできます。iPad実座標は取得できず、実機の復帰位置・操作感は未確認です。
- 通常リリースv1.02／内部バージョン1.0.2。旧版を完全終了し、ZIPを展開してexeを起動してください。Windows 11 x64・BLE Peripheralが必要、ランタイム同梱・署名なしです。

## English

- Passed 244 automated tests, three-language GUI checks and non-capturing executable verification.
- Fixes v1.01 accidentally returning to Windows on small reverse movements. The estimate starts in the middle; automatic return is blocked for one second after switching.
- Crossing the estimate only starts confirmation. Keep moving toward Windows for at least 300 ms and a minimum distance to return. A longer configured edge wait applies.
- Single flicks, tiny jitter and small moves after stopping cannot trigger return. Stopping, reversing, parallel motion, scrolling or key/button input resets confirmation. Overshoot is not carried forward.
- Windows-to-iPad switching, sensitivity and manual return shortcuts remain available. Automatic return can be disabled separately. Actual iPad coordinates are unavailable and hardware feel remains unverified.
- Regular release v1.02 / internal version 1.0.2. Fully exit the old app, extract the ZIP and launch the exe. Windows 11 x64 / BLE Peripheral required; unsigned, self-contained build.

## 简体中文

- 已通过244项自动测试、三语GUI及不捕获输入的程序验证。
- 修复v1.01轻微反向移动就返回Windows的问题。从中央开始估算，切换后一秒内不自动返回。
- 到达估算边界仅开始确认，需持续向Windows移动至少300毫秒及一定距离。若配置的边缘等待时间更长，则使用该值。
- 单次快速移动、细小抖动及停止后轻微移动不会返回。停止、反向、沿边移动、滚动或按键／按钮操作会重新开始确认，不累积越界距离。
- 保留Windows→iPad切换、灵敏度及手动返回快捷键，可单独关闭自动返回。无法读取iPad实际坐标，实际操作感仍需实机确认。
- 正式版v1.02／内部版本1.0.2。完全退出旧版，解压ZIP并运行exe。需要Windows 11 x64及BLE外围支持，含运行时、未签名。
