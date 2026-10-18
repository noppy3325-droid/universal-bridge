# Universal Bridge 0.3.3 — Preview

## English

- Fixes a reproducible reason for “connected, but cannot switch”: a Japanese half/full key can arrive as an IME mode virtual key. After a missed release, the mode flag could keep activation blocked even after returning to Windows. Reconciliation now checks the physical OEM key instead.
- Cleans released quarantine entries. Actual held keys, modifiers, mouse buttons, and unmapped hardware keys still prevent activation; reconciliation does not clear remote input state.
- Adds privacy-preserving rejection diagnostics: tracked/pending key counts, mouse flags, and OS physical-idle status. No key identities, typed text, or HID payloads are recorded. Diagnostic export includes the same counts.
- **141 unit tests passed**, including two regressions that failed before the fix, actual-held-key refusal, and 100 cycles of mixed key events, emergency returns, and missed releases. Retains the sleep recovery and language switching from v0.3.2, scroll controls, icon, and three UI languages.

Exit the old app, download and extract `UniversalBridge-0.3.3-win-x64.zip`, and run `UniversalControl.exe`. Settings are retained. This addresses a tested stale-input case; it does not establish that all intermittent failures are resolved. Real iPad verification after updating is still needed. Windows 11 x64 and BLE Peripheral support are required. Unsigned, self-contained preview.

## 日本語

- **「接続済みなのに切り替えできない」原因の一つを修正しました。** 半角／全角キーがIMEモード用の仮想キーとして届き、解放を観測できないままWindowsへ戻ると、そのモード値で切り替えを拒否し続けるケースです。物理OEMキーの状態を確認するよう変更しました。
- 解放済みの待機状態を整理します。本当に押している文字キー・修飾キー・マウス・未対応の実キーは引き続き拒否し、iPad操作中の入力状態は消しません。
- 再発時の診断に、追跡キー件数・解放待ち件数・マウス状態・OS物理入力なしの真偽を追加しました。**キー名・入力文字・HIDペイロードは記録しません。** 診断書き出しにも件数を含めます。
- **自動テスト141件成功**。修正前に失敗する2ケース、実押下中の拒否、混合キー・緊急復帰・キー解放欠落を100回繰り返す検証を含みます。0.3.2のスリープ復旧・日英切り替え、スクロール設定、アイコン、3言語対応も維持します。

旧版を終了し、`UniversalBridge-0.3.3-win-x64.zip` を展開してexeを起動してください。設定は引き継ぎます。再現した押下状態残留への修正であり、すべての断続的な障害が解消したと確認したものではありません。iPad実機での更新後の確認が必要です。Windows 11 x64／BLE Peripheral対応が必要な、ランタイム同梱・署名なしのプレビュー版です。

## 简体中文

- 修复“已连接却无法切换”的一个可复现原因：日语半角/全角键可能以输入法模式虚拟键上报，遗漏释放后返回 Windows，其模式值可能持续阻止切换。现在改为检查物理 OEM 键状态。
- 清理已释放的等待状态。实际按住的普通键、修饰键、鼠标按钮和未映射硬件键仍会阻止切换；不会清除远程操作中的输入状态。
- 新增保护隐私的拒绝诊断：跟踪及待释放按键数量、鼠标状态、系统物理输入空闲状态。**不记录键名、输入文字或 HID 数据内容。** 导出的诊断也包含这些数量。
- **141 项单元测试通过**，包含修复前失败的两个场景、实际按键未释放的拒绝及 100 次混合按键／紧急返回／遗漏释放循环。保留 0.3.2 的睡眠恢复和语言切换、滚动设置、图标与三语界面。

退出旧版，下载并解压 `UniversalBridge-0.3.3-win-x64.zip`，运行 exe，原设置会保留。此修复针对已复现的残留输入状态，尚未确认所有偶发问题均已解决；仍需更新后的 iPad 实机验证。需要 Windows 11 x64 和 BLE 外围设备支持，此为包含运行时的未签名预览版。
