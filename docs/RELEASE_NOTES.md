# Universal Bridge v1.04

## 日本語

- 差し替えがアクセス競合で失敗した場合も、検証済みの元exeを再起動して復帰します。

- 276件の自動テスト、3言語・33画像のGUI確認、専用コピーによる実プロセス更新・ロールバック検証に成功しました。
- GitHubの標準READMEを日本語へ変更。英語版はREADME.en.md、中国語版も継続します。
- ダーク／ライト設定をWindowsのタイトルバー・文字・枠へ反映しました。
- アプリ内更新を追加。起動時の自動確認はオン／オフ可能で、適用は「詳細・診断 → アプリの更新 → 更新して再起動」から行えます。SHA-256・サイズ・版番号を検証し、Windows操作へ戻してexeを差し替えます。設定を保持し、起動失敗時は旧exeへ復旧します。
- 初回のみv1.04を手動でダウンロードしてください。v1.02以前には更新機能がありません。自動更新はexeのみを対象とし、説明書はZIPで更新します。
- 更新確認・ダウンロード時にGitHubへ接続します。入力共有はオフラインで利用できます。Windows 11 x64、ランタイム同梱・署名なしの通常リリースです。

## English

- A sharing violation during replacement now restarts the verified original executable too.

- Passed 276 automated tests, three-language/33-image GUI checks, and real process replacement/rollback against private copies.
- Japanese is the default GitHub README; English moves to README.en.md and Chinese remains available.
- Dark/light mode now includes the native Windows title bar, text and border.
- Adds in-app updates with optional automatic checks and Update and restart. Stable-release metadata, size and SHA-256 are verified. Input returns to Windows before replacing the exe; settings remain and startup failure restores the previous executable.
- Download v1.04 manually once when coming from v1.02 or earlier. The updater replaces only the exe; refresh bundled documents through the ZIP.
- Update checks/downloads contact GitHub; input sharing works offline. Unsigned, self-contained regular release for Windows 11 x64.

## 简体中文

- 文件占用导致替换失败时，也会重新启动已验证的原exe。

- 已通过276项自动测试、三语33张GUI截图验证，以及专用副本的实际进程更新与回滚测试。
- GitHub默认README改为日语，英语移至README.en.md，保留中文版本。
- 深色／浅色主题也应用到Windows原生标题栏、文字和边框。
- 新增应用内更新，可开关自动检查并点击“更新并重启”。验证正式版元数据、大小和SHA-256，替换exe前返回Windows并保留设置；启动失败恢复原来的exe。
- 从v1.02及更早版本升级时需手动下载v1.04一次。自动更新仅替换exe，同捆文档通过ZIP更新。
- 检查和下载更新时连接GitHub，输入共享仍可离线使用。Windows 11 x64，包含运行时，未签名正式版。
