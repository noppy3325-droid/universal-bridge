# 技術調査と検証範囲

調査日: 2026-10-17（Asia/Tokyo）。参照コードの実行結果と、本プロジェクトで測定した結果を区別します。

## Windows標準API

[Microsoft GATT Server](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/gatt-server) では、公開禁止サービスとしてDIS、GATT、GAP、Scan Parametersを列挙しています。HID 0x1812はこのリストにありません。本PCで `GattServiceProvider.CreateAsync(0x1812)` はSuccessとなり、実際の広告公開・停止・再公開にも成功しました。対象PCすべてへの一般化はしません。

標準の [NotifyValueAsync(value, subscribedClient)](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattlocalcharacteristic.notifyvalueasync) を使って対象を1台に限定します。通知結果の [Status](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattclientnotificationresult) を確認します。OSが生成するCCCDは手動で追加しません。予約サービスの上書き、ドライバー入替、レジストリによるBluetooth制限回避は行いません。

## windows-ble-hid

[リポジトリ](https://github.com/abhishek-raj/windows-ble-hid) のコミット `b33a0043b3865ea8251a242eae27aa2f72816512` を読みました。WinRT GATTによる複合HOGP、暗号化属性、report reference、リンク切断検出、入力キュー、キーの所有権を確認しました。MITのdescriptor/keymapだけを出典表示付きで再利用し、JISの範囲を拡張しました。

同プロジェクトはiPadOSを未検証としています。[開発記録](https://github.com/abhishek-raj/windows-ble-hid/blob/b33a0043b3865ea8251a242eae27aa2f72816512/DEVELOPMENT.md) にある購読リストの残存とprovider再起動後の再接続問題を考慮し、物理リンク情報も判定し、providerをアプリ存続中に維持します。このOSS自体を本環境で動作確認したとは主張しません。

## OpenSpan

[リポジトリ](https://github.com/Divisionless/OpenSpan) のコミット `f60a7124be4d3d3c1ee3726154eaf6fd48d73514` のREADME、BLE daemon、Windows portal、MITライセンスを確認しました。BlueZでの暗号化読取、入力レポート、画面端・相対カーソルと復帰方法を参照しました。コードは取り込んでいません。

このOSSの方式は、内蔵無線をLinux VMへUSBパススルーしWindowsから通信する構成です。VM導入と無線所有権の移動を要するため採用しません。READMEの「WindowsがHIDを予約」という記述は上記公式資料および本PCでの測定と一致しません。OpenSpanのiPad動作の記述も、今回のアプリの実機検証を代替しません。

## 採用方式と残る確認

0.2.0では [Precision Touchpad APIの公式ポータル](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/precision-touchpad-portal)、[ウィンドウ登録](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/registertouchpadcapable)、[TouchpadGesturesController](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/touchpadgesturescontroller)、[PhysicalGestureRecognizer](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/physicalgesturerecognizer) を参照しました。通常の低レベルマウス／キーボードフックだけでは、OSが処理するPrecision Touchpadの複数指ジェスチャーを取得できません。ウィンドウをタッチパッド対応として登録し、2本指のWM_POINTERと3～5本指のグローバルジェスチャーを別経路で受け取ります。

Microsoftのグローバルジェスチャー配送は前面プロセスに限定されます。そのためiPad操作中だけ小さな受け取り画面を前面に置き、取得できない場合やフォーカスを失った場合はWindows操作へ戻します。OSのジェスチャー設定・ドライバーを変更する方式は採用しません。

これらは公式に実験的とされるAPIです。Windows SDK .NET projection 10.0.26100.87を使用し、最低OSバージョンは22000のまま、26100以降かつAPIの存在・IsSupported・初期化成功を確認して有効にします。本PCの読み取り専用プローブでWindows build26300、ウィンドウAPIあり、コントローラー対応を確認しました（`artifacts/touchpad-capabilities/touchpad-probe.json`）。実Windowsの起動・解除も成功しています。すべての24H2端末への対応を保証するものではありません。

iPad側へは従来の標準キーボード／マウスHIDを使い、横スクロールにConsumer AC Panを追加しました。複数指の接点やMagic Trackpad固有プロトコルは実装していません。アプリ切り替え・検索等のキー変換は [Appleのキーボードショートカット](https://support.apple.com/en-us/102393) を参照しています。ホーム・拡大縮小等の個別キー対応はiPadOSとアプリに依存し、実機の受け入れ項目として残しています。

0.1.2のマウス精度改善では、Microsoftの [Raw Input手順](https://learn.microsoft.com/en-us/windows/win32/inputdev/using-raw-input) と [RAWMOUSE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse) を参照しました。相対Raw InputはWindowsのマウス速度設定の影響を受けないため、Windowsカーソル座標による二段階の加速を避けます。RIDEV_INPUTSINKとmessage-only windowを使い、DefWindowProcによるWM_INPUTの後処理を行います。

標準WinRTを第一選択とし、C#/.NETのネイティブGUIで実装しました。追加SDKは許可を受け、公式.NET 10.0.402をハッシュ照合して作業フォルダへ展開しました。Bluetoothに関するシステム設定やペアリング登録を自動で変えていません。

0.1.1でユーザーからiPadのマウス操作・キーボード入力成功の報告を受けました。0.1.2の相対入力による位置合わせの改善、細かい操作・全キー・長時間動作は、引き続きiPadでの比較・目視確認が必要です。[実機チェック](MANUAL_TESTS.md) と [TEST_REPORT.md](../TEST_REPORT.md) に残しています。
