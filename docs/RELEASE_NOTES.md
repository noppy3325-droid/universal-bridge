# Universal Bridge v2.0.1

## 日本語

- **iPadからWindowsへの画面端復帰を改善。** 感度調整前の移動量を使い、低感度や斜め移動でも押し戻しを判定します。最後の小さな反転で継続判定が消えないようにし、境界付近の微小な揺れや入力間隔のばらつきにも対応しました。
- 切り替え直後1秒の保護、境界到達後の最短300msの継続移動、停止・キー・ドラッグ・スクロールによる誤復帰防止は維持します。iPadの実座標は取得できないため、相対移動の推定による限界は残ります。復帰キー／Esc3回も利用できます。
- **更新ダウンロードを軽量化。** 自己完結の単体exeを約203MBから約85MBへ縮小し、v2.0.0から取得する量も約58％減りました。新版の更新機能は専用ZIP、通信停止時の範囲再開、速度・取得量表示に対応し、ダウンロード処理を画面操作から分離します。ZIPと展開後exeのサイズ・SHA-256を両方確認します。
- 旧版の更新形式に対応し、実行ファイル名は**UniversalBridge.exe**のままです。圧縮によりこのPCの起動試験では約0.7秒増加しました。実際の取得速度は回線やGitHub側の状況にも依存します。
- 335件の自動テストで低感度・四方向・斜め・微小反転・誤復帰防止と、中断／停滞後の再開・破損ZIP拒否を確認しました。**iPad実機での境界位置・操作感の最終確認は未実施です。**
- v1.05以降はアプリの「更新を確認」→「更新して再起動」から更新できます。手動の場合は**UniversalBridge-2.0.1-win-x64.zip**を展開し、UniversalBridge.exeを実行してください。クリップボード共有と右下の状態カードも引き続き利用できます。

## English

- **More reliable edge returns from iPad to Windows.** Return pressure uses movement before sensitivity scaling, including low sensitivity and diagonal motion. Tiny final reversals no longer erase a net outward batch; small boundary jitter and uneven input intervals are tolerated.
- Keeps the one-second entry grace, at least 300ms of fresh outward movement after boundary arrival, and protection during idle, typing, dragging and scrolling. This remains a relative-motion estimate, not iPad cursor tracking. The return shortcut and triple-Esc remain available.
- **Smaller, resumable updates.** The self-contained executable shrinks from about 203MB to 85MB, roughly 58% less even for the v2.0.0 updater. The new updater uses a dedicated ZIP, resumes interrupted or stalled transfers, displays speed and byte progress, and runs download work off the UI thread. Both the ZIP and extracted executable are checked by size and SHA-256.
- Compatible with the older update format; the filename remains **UniversalBridge.exe**. Compression added about 0.7 seconds in startup probes on this PC. Network and GitHub conditions still affect download speed.
- Passed 335 automated tests covering return precision and false-return protection, interrupted/stalled transfers, and invalid archive rejection. **Final physical-iPad boundary and usability testing is still pending.**
- From v1.05 onward, use Check for updates → Update and restart. For manual installation, extract **UniversalBridge-2.0.1-win-x64.zip** and run UniversalBridge.exe. Clipboard sharing and the bottom-right status card remain available.

## 简体中文

- **改善从iPad边缘返回Windows的操作。** 返回判断使用灵敏度调整前的移动量，支持低灵敏度和斜向移动。最后的微小反向输入不会抹去整批向外移动，也能容忍边界附近的小幅抖动与输入间隔变化。
- 保留切换后一秒保护、到达边界后至少300ms的持续移动，以及静止、键盘、拖动和滚动时的误返回保护。这仍是相对移动估算，无法读取iPad光标的实际坐标。返回快捷键和连按三次Esc仍可使用。
- **缩小更新文件并支持断点续传。** 自包含exe从约203MB缩至85MB，即使从v2.0.0更新，下载量也减少约58%。新版更新器使用专用ZIP，支持中断或停滞后继续下载，显示速度与进度，并将下载处理移出界面线程。ZIP和解压后的exe均核对大小与SHA-256。
- 兼容旧更新格式，文件名仍为**UniversalBridge.exe**。此电脑的启动测试因压缩增加约0.7秒。实际下载速度仍受网络和GitHub影响。
- 已通过335项自动测试，涵盖返回精度、误返回保护、中断／停滞恢复和无效压缩包拒绝。**尚未完成实体iPad边界位置与操作体验的最终验收。**
- v1.05及以后版本可通过“检查更新”→“更新并重新启动”升级。手动安装请解压**UniversalBridge-2.0.1-win-x64.zip**并运行UniversalBridge.exe。剪贴板共享和右下角状态卡片继续可用。
