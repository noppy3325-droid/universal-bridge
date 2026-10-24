# Universal Bridge v1.01

## English

- Regular release: v1.01 / executable and packages 1.0.1. Validation: 225 automated tests, three-language GUI checks and non-capturing executable probes.
- Removes optional iPad contact relay, related settings, HID reports and diagnostics. Standard mouse/keyboard, two-finger scrolling and gesture-to-key conversion remain.
- Fixes one-way edge switching: automatic edge return now defaults to on with edge switching, including old profiles with estimated return disabled. Configure it in Display layout.
- Preserves entry/return position along the portal and places the Windows pointer 32 px inside the connected edge. Held keys/buttons block return; release them and move toward Windows again. Idle, scrolling or release alone cannot trigger a delayed return.
- Return uses estimated relative motion, not measured iPad coordinates. Adjust Estimated iPad range in Display layout if needed. iPad acceleration/touch can cause drift; physical iPad acceptance remains pending. Ctrl+Alt+Q / Esc three times remain available.
- Exit the old app before updating. If you used the removed contact mode and iPad caches its old HID format, manually forget/re-pair only if reconnection fails. Unsigned, self-contained Windows 11 x64 build.

## 日本語

- 通常リリースv1.01、実行ファイル・ZIPは1.0.1です。225件の自動テスト、3言語GUI、入力捕捉なしのexe検証を確認しました。
- iPad向け接点転送の設定・HIDレポート・診断を削除。通常のキー・マウス・2本指スクロール・ジェスチャーのキー変換は利用できます。
- 画面端切り替えが片方向だった問題を修正。自動切り替え有効時はiPad→Windowsの推定復帰も初期値オンです。旧プロフィールの推定復帰オフも移行します。「ディスプレイ配置」で変更できます。
- 接続辺に沿う進入／復帰位置を対応させ、Windows側は32px内側へ戻します。キー・ボタン保持中は復帰せず、離した後に新しくWindows側へ動かすと戻ります。静止・スクロール・解放だけの遅延復帰は防ぎます。
- 復帰は相対移動からの推定です。iPad実座標は読めず、加速・タッチ操作でずれます。配置画面の推定幅・高さで調整できます。iPad実機での最終確認は未実施です。Ctrl+Alt+Q／Esc3回も利用できます。
- 旧版を完全終了して更新してください。削除した接点モードを使用し、旧HID形式のキャッシュで接続できない場合だけ手動で登録解除・再ペアリングしてください。Windows 11 x64、ランタイム同梱・署名なしです。

## 简体中文

- 正式版v1.01，程序及压缩包版本1.0.1。已通过225项自动测试、三语GUI及不捕获输入的程序验证。
- 移除iPad触点转发的设置、HID报告及诊断。保留键盘、鼠标、双指滚动及手势到按键的转换。
- 修复边缘切换只能单向工作的问题。启用边缘切换后默认开启iPad→Windows估算返回，旧版关闭估算返回的配置也会迁移。可在显示器布局修改。
- 沿连接边缘映射进入／返回位置，Windows指针回到边缘内侧32像素处。按住按键或按钮时不会返回；松开后需重新向Windows移动。静止、滚动或松开本身不会触发延迟返回。
- 返回根据相对移动估算，无法读取iPad实际坐标。加速及触屏操作会导致偏差，可在布局调整估算宽高。iPad实机最终确认尚未完成。也可用Ctrl+Alt+Q或连按Esc三次返回。
- 更新前完全退出旧版。若以前使用触点模式且旧HID缓存导致连接失败，再手动忽略PC并重新配对。Windows 11 x64，包含运行时，未签名。
