# Contributing / 開発への参加 / 参与贡献

The default README.md is Japanese; keep README.ja.md identical. English lives in README.en.md and Simplified Chinese in README.zh-CN.md.

Issues and pull requests are welcome in English, Japanese, or Simplified Chinese. Include Windows/iPadOS versions, adapter/driver, reproduction steps, expected results, and HID subscription state. **Do not post passwords, typed content, device addresses, or unredacted diagnostics.**

日本語・英語・中国語（簡体字）でIssue／PRを受け付けます。環境・再現手順・期待する動作・購読状態を記載し、入力内容や識別情報を含むログは公開しないでください。

欢迎使用英语、日语或简体中文提交 Issue／PR。请提供环境、复现步骤、预期行为和订阅状态。请勿公开输入内容、设备地址或未经脱敏的诊断文件。

## Development

1. Fork/clone on Windows 11 x64. Use .NET SDK 10.0.402 or `scripts/Setup-Sdk.ps1`.
2. Run `pwsh -File scripts/Build.ps1 -Test`.
3. Keep hook callbacks synchronous and short. Never log HID payloads or automatically capture input after reconnect.
4. Distinguish unit tests, Windows API tests, and actual iPad verification. See [hardware tests](docs/MANUAL_TESTS.md).

`--smoke-test` uses an isolated data directory, starts neither Bluetooth nor hooks, and exercises three languages. Exit the normal app before `scripts/Test-Platform.ps1`, which briefly advertises HID and installs hooks without suppressing input.

## Translation

Catalogs: `src/UniversalControl.Core/Localization/{ja,en,zh-Hans}.json`. Japanese source messages are stable gettext-style keys; `ui.json` maps WPF resources to source messages.

- C#: `L.T(source)` or `L.F(source, arguments)`.
- Add every key to all catalogs. Keep format placeholders such as `{0}` and `{1:P0}` identical.
- XAML: map a resource in `ui.json` and use `DynamicResource` for immediate language changes.
- Keep device names, key letters, protocol values, and diagnostic field names unchanged. OS exception text follows Windows language. Historical logs retain their original language.
- Run unit and GUI smoke tests. Check all pages, the shortcut editor, tray, and both themes for wrapping/clipping.

## Releases

`scripts/Package.ps1` creates binary/source archives and `SHA256SUMS.txt`, excluding SDKs, caches, user settings, and logs. Push a `vX.Y.Z` tag matching the source version; GitHub Actions builds/tests it and publishes preview assets. CI does not prove real-device input works.

Contributions use the [MIT license](LICENSE). Retain third-party notices in adapted files.
