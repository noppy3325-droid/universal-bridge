# Universal Bridge v2.0.0

## 日本語

- **Safariでテキストのクリップボードを双方向共有。** 同じWi-Fi上で動作し、追加のiPadアプリ・インターネットは不要です。アプリの「クリップボード共有」で開始し、初回の証明書設定と6桁コードで接続します。取得→iPadへコピー、または貼り付け→Windowsへ送信を選びます。
- HTTPS暗号化、5分・1回限りの接続コード、既存接続の失効、64 KiBの上限、共有内容・コードを記録しない設計。共有は毎回オフで起動し、停止・終了・セッション変更・スリープで切断します。初回設定用HTTPは公開証明書と案内のみです。
- **iPad操作中の表示を右下の小さな状態カードへ。** 角丸と控えめな影、ライト／ダーク、戻るショートカットとEsc3回を表示。タスクバーを避け、表示カードはフォーカスを奪わずクリックを透過します。ジェスチャー受け取りの中央の枠を取り除き、マウス移動の基準位置は維持しました。
- サイドバー・起動ログ・診断の版番号を同じビルド情報へ統一しました。実行ファイル名はUniversalBridge.exeです。
- 305件の自動テスト、3言語42画像のGUI、PC内の実TLS・DPAPI・文字往復、ブラウザー操作の検証に成功。**iPad実機でのSafari・証明書設定・ジェスチャーの最終受け入れは未実施です。**
- v1.05からはアプリ内更新が使えます。v1.04以前は手動でダウンロードしてください。テキストのみの明示操作で、自動同期・画像・ファイル共有は対象外です。[設定手順](https://github.com/noppy3325-droid/universal-bridge/blob/main/docs/CLIPBOARD.md)。

## English

- **Bidirectional text clipboard sharing through Safari**, on the same Wi-Fi, with no iPad companion app or internet service. Start Clipboard sharing, set up this PC’s certificate once, then connect with the six-digit code. Fetch and copy to iPad, or paste into the page and send to Windows.
- HTTPS, one-use/five-minute codes, session revocation and a 64 KiB text limit. Content and codes are not logged. Sharing starts off and stops on exit, session changes and sleep. The HTTP setup listener serves only public setup information and the public certificate.
- **A small bottom-right status card during iPad control.** Rounded corners, subtle shadow, light/dark themes, the return shortcut and triple-Esc. It avoids the taskbar, does not activate and passes clicks through. The central gesture receiver’s frame is removed while retaining the motion anchor.
- Sidebar, startup log and diagnostics use the same build version. The executable remains UniversalBridge.exe.
- Passed 305 automated tests, three-language/42-image GUI checks, real local TLS/protected-key/Unicode transfer checks, and browser UI checks. **Final physical-iPad Safari/certificate/gesture acceptance is still pending.**
- In-app update is available from v1.05; versions through v1.04 need a manual download. Explicit plain-text transfers only; no automatic sync, images or files. [Setup guide](https://github.com/noppy3325-droid/universal-bridge/blob/main/docs/CLIPBOARD.en.md).

## 简体中文

- **通过Safari双向共享纯文本剪贴板**，使用同一Wi-Fi，无需iPad专用应用或互联网服务。在“剪贴板共享”中开始，首次设置此电脑的证书，输入六位连接码后获取并复制到iPad，或粘贴到页面后发送到Windows。
- HTTPS加密、一次性且五分钟有效的连接码、撤销已有连接及64 KiB文本上限。不记录内容与连接码。每次启动默认关闭，退出、会话变化或睡眠时停止。HTTP首次设置服务仅提供公用证书与设置说明。
- **操作iPad时改用右下角小型状态卡片**，带圆角、轻微阴影、深浅主题和返回快捷键／Esc三次提示。避开任务栏，不获取焦点，点击可穿透。移除中央手势接收窗口的边框，保留鼠标移动基准位置。
- 侧栏、启动日志和诊断使用同一构建版本，文件名仍为UniversalBridge.exe。
- 已通过305项自动测试、三语42张GUI截图、电脑内实际TLS／密钥保护／Unicode传输及浏览器界面验证。**尚未完成实体iPad上的Safari、证书设置和手势最终验收。**
- v1.05支持应用内升级，v1.04及更早版本需手动下载。仅限明确操作的纯文本传输，不支持自动同步、图片或文件。[设置说明](https://github.com/noppy3325-droid/universal-bridge/blob/main/docs/CLIPBOARD.zh-CN.md)。
