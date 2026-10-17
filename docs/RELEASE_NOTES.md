# Universal Bridge 0.3.2 — Preview

## English

- Recover input delivery after iPad sleep/wake without restarting the app: replacement GATT sessions are monitored, closed-session Suspend state expires, and notifications select an active subscription. Activation refreshes connection information first.
- After a send failure, the switch button and shortcut can recover the sender only after both keyboard and mouse release reports succeed. Old queued input is discarded; an unfinished timed-out send blocks recovery. Reconnection never captures input automatically.
- Japanese **半角/全角** (including Alt+半角/全角) sends one **Control+Space** tap to cycle iPad input languages instead of typing punctuation. Holding the key does not cycle repeatedly; US backtick remains unchanged. Add Japanese and English keyboards on iPad.
- **135 unit tests passed**, including sleep/reconnect, partial-release refusal, cancellation, unfinished sends, and JIS key regressions. Physical iPad sleep/wake and IME behavior still require user verification.
- Retains adjustable two-finger scrolling, custom app icon, editable shortcuts, and Japanese/English/Simplified Chinese UI.

Exit the old app, download and extract `UniversalBridge-0.3.2-win-x64.zip`, then run `UniversalControl.exe`. After waking iPad, unlock it, wait for both subscriptions, and activate again. If disconnected, connect to the PC in iPad Settings → Bluetooth. Windows 11 x64 and BLE Peripheral support are required. Unsigned, self-contained preview; no installer.

## 日本語

- iPadのスリープ復帰後にアプリ再起動が必要になる送信停止を修正しました。新しいGATTセッションを監視し、終了した接続の一時停止状態を解除します。通知先には有効な購読を選び、切り替え前に接続情報を更新します。
- 送信失敗後もボタン／ショートカットから再試行できます。キーボードとマウス両方の解放送信が成功した場合だけ復帰し、古い入力は再送しません。未完了のタイムアウト送信が残る場合は拒否し、再接続だけでは自動で操作先を戻しません。
- 日本語キーボードの**半角／全角**（Alt併用も対応）は記号ではなく**Control+Spaceを1回**送って入力言語を切り替えます。長押しの連続切り替えを防止し、US配列のバッククォートは維持します。iPadに日本語・英語キーボードを追加してください。
- **自動テスト135件成功**。スリープ／再接続、片方だけの解放送信時の拒否、取り消し、未完了送信、JISキーを検証しました。iPad実機のスリープ復帰とIMEの再確認は残ります。
- 2本指スクロール感度、アイコン、GUIショートカット編集、3言語対応は継続します。

旧版を終了し、`UniversalBridge-0.3.2-win-x64.zip` を展開してexeを起動してください。iPad復帰後はロックを解除し、両購読が揃ってから再度切り替えます。切断中ならiPad「設定 → Bluetooth」からPCへ接続してください。Windows 11 x64／BLE Peripheral対応が必要な、ランタイム同梱・署名なしのプレビュー版です。

## 简体中文

- 修复 iPad 睡眠唤醒后发送程序无法恢复的问题：跟踪新的 GATT 会话，清除已关闭会话的暂停状态，选择有效订阅发送通知，并在切换前刷新连接信息。
- 发送失败后可通过按钮或快捷键重试；只有键盘和鼠标释放报告均成功才恢复。不会重发旧输入，未完成的超时发送会阻止恢复，重连不会自动捕获输入。
- 日语键盘的**半角/全角**（支持 Alt 组合）发送一次 **Control+Space** 切换 iPad 输入语言，不再输入标点；长按不反复切换，美式反引号保持原有行为。请在 iPad 添加日语和英语键盘。
- **135 项单元测试通过**，覆盖重连、部分释放、取消、未完成发送及日语按键。iPad 实机睡眠唤醒和输入法行为仍需使用者验证。
- 保留双指滚动灵敏度、应用图标、可编辑快捷键及三语界面。

退出旧版，下载并解压 `UniversalBridge-0.3.2-win-x64.zip`，运行 exe。唤醒后解锁 iPad，等待双报告订阅后重新切换；若未连接，请从 iPad“设置 → 蓝牙”连接电脑。需要 Windows 11 x64 和 BLE 外围设备支持；此为包含运行时的未签名预览版。
