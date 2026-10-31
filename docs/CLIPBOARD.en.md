# Share the clipboard with Safari

English · [日本語](CLIPBOARD.md) · [简体中文](CLIPBOARD.zh-CN.md)

Version 1.06 adds bidirectional plain-text sharing between Windows and iPad on the same Wi-Fi. No iPad companion app or internet service is required. Transfers use explicit buttons; automatic sync, images and files are not supported. The limit is 64 KiB of UTF-8 text.

## First connection

1. Connect both devices to the same Wi-Fi. In the Windows app, open Clipboard sharing, select your Wi-Fi IP address, and start sharing.
2. If Windows asks for network access, allow inbound connections only on your trusted private network. You do not need to disable the firewall or forward router ports.
3. On iPad, open the setup URL (`http://…:43182/`) in Safari. This HTTP page serves only the public certificate and instructions. It cannot read or write the clipboard and never asks for the connection code.
4. **Compare the certificate name and SHA-256 with the Windows app**, then download the public certificate. Install it in Settings → General → VPN & Device Management. Trust that certificate in General → About → Certificate Trust Settings. Managed iPads may require administrator assistance. [Apple’s instructions](https://support.apple.com/en-us/102390).
5. Open the sharing URL (`https://…:43183/`) in Safari. If a certificate warning remains, stop and check the certificate, trust settings and URL. Enter the six-digit code shown on Windows.

The private authority key stays encrypted under your Windows account. Only the public certificate goes to iPad. Installing it trusts this PC as a connection provider, so verify its name and fingerprint. Remove the added certificate in iPad Settings when you stop using sharing.

## Transfer text

- **Windows → iPad:** Copy text in any Windows app. In Safari, tap Get from Windows, then Copy to iPad. Paste into Notes or another app.
- **iPad → Windows:** Copy text on iPad. Paste it into the Safari text box using the normal paste menu, then tap Send to Windows. Paste into any Windows app. Sending an empty box clears the Windows clipboard.

If the copy button is unavailable, touch and hold the received text to select and copy it. Clipboard operations follow Safari’s user-gesture requirements. [WebKit’s explanation](https://webkit.org/blog/10855/async-clipboard-api/).

## Manage the connection

- Codes last five minutes and can be used once. After five failures, generate a new code on Windows. This also revokes previously authorized pages.
- Sharing starts off. Stop, exit, Windows session changes and sleep revoke the session. Resume with a new code. You can bookmark the sharing URL.
- Text travels over HTTPS. There is no clipboard polling, automatic sync or content history. Codes, cookies and content are not logged. OS clipboard history and text copied by the user remain subject to the OS/browser’s own behavior.
- If connection fails, check the Wi-Fi, selected IP, Windows inbound access and guest/corporate Wi-Fi device isolation. After an IP change, stop sharing, refresh the address list and start again. The certificate need not be reinstalled while this PC’s public authority stays the same.

Local TLS, authentication, revocation, Unicode transfer and browser UI are tested. Final Safari/device setup and usability acceptance on a physical iPad have not been performed.
