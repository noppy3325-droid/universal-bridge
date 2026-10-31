# Universal Bridge v1.05

## 日本語

- 実行ファイル名を **UniversalBridge.exe** に修正しました。単体ダウンロード、ZIP内のexe、ビルド出力、更新用ファイル、3言語の案内を統一しています。
- 旧名のexeが配布物に混入した場合は、ビルド・パッケージ作成を停止します。
- 更新後の起動確認ファイルを完全に書き終えてから公開し、一時的な読み取り競合で誤って旧版へ戻らないようにしました。
- **v1.04以前からは今回だけ手動ダウンロードが必要です。** 旧版の更新機能は旧ファイル名を固定しているため、この移行に利用できません。旧版を完全終了してから新版を起動してください。保存済みの設定は引き継ぎます。
- Windows起動時の自動起動を利用している場合は、新版の設定で一度オフ→オンにして起動先を登録し直してください。
- 日本語を標準とするREADME、英語・中国語表示、タイトルバーテーマ、アプリ内更新を継続します。Windows 11 x64向け、ランタイム同梱の通常リリースです。

## English

- Corrected the executable name to **UniversalBridge.exe** across the standalone download, ZIP, build output, update files and all three language guides.
- Builds and packaging reject output directories containing the old executable name.
- Startup confirmation markers are published only after writing completes; temporary read locks no longer trigger an unnecessary rollback.
- **From v1.04 or earlier, download this release manually once.** Older updaters hardcode the previous filename and cannot migrate it. Fully exit the old app before launching the new one; saved settings carry over.
- If you use launch at Windows startup, turn it off and on in the new app to register its new path.
- Retains Japanese as the default README, English/Chinese UI, themed title bars and in-app updates. Self-contained regular release for Windows 11 x64.

## 简体中文

- 可执行文件名修正为 **UniversalBridge.exe**，独立下载、ZIP、构建输出、更新文件和三语说明全部统一。
- 构建和打包会拒绝含旧文件名的输出目录。
- 启动确认文件写入完成后才发布，暂时的读取冲突不再导致不必要的回滚。
- **从v1.04及更早版本升级时，此次请手动下载。** 旧更新程序固定使用旧文件名，无法完成此次迁移。完全退出旧版后启动新版，已有设置会保留。
- 如果启用了随Windows启动，请在新版中关闭后重新开启，以注册新路径。
- 保留日语默认README、英语和中文界面、标题栏主题与应用内更新。Windows 11 x64，包含运行时的正式版。
