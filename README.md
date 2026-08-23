# 🧪 ArchiveHashVerifier v1.1.4

ArchiveHashVerifier は、Windows 11 x64 向けの **ハッシュ生成・検証 / OpenPGP Detached Signature 生成・検証 GUIツール**です。

SHA-512 / SHA-256 / SHA3-512 / SHA3-256 / **BLAKE3** を扱い、GnuPG / Gpg4win が導入されている環境では OpenPGP の `.asc` / `.sig` も生成・検証できます。

- 💻 C# / Windows Forms
- 🧩 .NET 10
- 🪟 Windows 11 x64
- 📦 Self-contained / Single-file
- 👤 管理者権限不要
- ✅ 実行時の外部DLL不要
- ⚡ BLAKE3: `Blake3` NuGet 3.0.2（完全マネージド版）

## 🔐 プライバシーと透明性

ArchiveHashVerifier は、利用者のプライバシーと動作の透明性を重視して設計しています。

- 📡 テレメトリ、アクセス解析、広告、ユーザー追跡機能は実装していません
- 📁 対象ファイルの内容、ファイル名、ハッシュ値等を開発者へ送信する処理は実装していません
- 🔑 GPGの秘密鍵やパスフレーズをArchiveHashVerifier自身が保存・収集することはありません
- ⚙️ 設定情報は `ArchiveHashVerifier.settings.json` として利用者のローカル環境へ保存します
- 🛡️ OpenPGP処理には、利用者のPCへインストールされているGnuPG / Gpg4winを使用します
- 🌐 ArchiveHashVerifier自身には、動作に不要な外部通信や情報収集を行う機能を実装していません

ソースコードはGitHub上で公開しています。配布EXEを利用する場合は、公開SHA-256との一致も確認してください。

## ✨ 主な機能

### 🔎 検証モード（起動時の既定）

- ✅ SHA-512 / SHA-256 / SHA3-512 / SHA3-256 / BLAKE3
- 📝 OpenPGP ASCII Detached Signature (`.asc`)
- 💾 OpenPGP Binary Detached Signature (`.sig`)
- 📂 複数ファイル、フォルダー再帰、ドラッグ＆ドロップ
- 🌐 UNCパス / NAS上の大容量ファイル
- 📋 OK / NG / ERROR / SKIP / CANCEL の結果表示

認識する主な拡張子:

```text
.sha512 / .sha-512
.sha256 / .sha-256
.sha3-512 / .sha3_512
.sha3-256 / .sha3_256
.blake3
.asc / .sig
```

### 🛠️ 生成モード

以下を任意に同時生成できます。

```text
SHA-512   -> .sha512
SHA-256   -> .sha256
SHA3-512  -> .sha3-512
SHA3-256  -> .sha3-256
BLAKE3    -> .blake3
OpenPGP ASCII Detached Signature  -> .asc
OpenPGP Binary Detached Signature -> .sig
```

初期設定では **SHA-512 / BLAKE3 / OpenPGP ASCII (.asc)** がONです。

ハッシュ行は次の形式です。

```text
<ハッシュ値><半角スペース2個><元ファイル名>
```

複数Hash方式を選択しても、同じ元ファイルは1回のストリーム読み込みで同時計算します。

## 📚 v1.1.4 一括出力

生成モードの **「方式ごとに1ファイルへまとめて出力する」** は既定ONです。同一フォルダーの対象ファイルを方式ごとの固定ファイルへ集約できます。

```text
ArchiveHashVerifier.sha512
ArchiveHashVerifier.sha256
ArchiveHashVerifier.sha3-512
ArchiveHashVerifier.sha3-256
ArchiveHashVerifier.blake3
```

一括OpenPGP署名を選択した場合は、決定的JSON manifestを生成し、そのmanifestへDetached Signatureを作成します。

```text
ArchiveHashVerifier.manifest
ArchiveHashVerifier.manifest.asc
ArchiveHashVerifier.manifest.sig
```

- 一括生成対象は同一フォルダー内の実ファイルに限定
- manifestにはSHA-512を必須収録し、選択された他Hashも収録
- manifestはUTF-8 BOMなし / CRLF / 固定property順 / timestamp等の個人・端末情報なし
- `.asc` / `.sig` の両方が存在する場合は両方を検証
- 公開鍵不足時は、利用者が選択した場合のみ「OpenPGP署名未検証 / ハッシュのみ検証」として継続
- 不正署名・失効・期限切れ等のhard failureは安全側に判定

チェックをOFFにすると、v1.1.3互換のファイルごとのsidecar / Detached Signature生成を利用できます。

## ⚡ BLAKE3

BLAKE3実装には `Blake3` NuGet 3.0.2 の完全マネージド版を使用しています。

- ✅ 標準256bit（32 bytes / 64桁hex）
- 📄 `.blake3` 生成・検証
- 🧠 CPU機能を実行時に自動判定
- 🚀 SIMD最適化をライブラリ側で利用
- 🧵 `Hasher.UpdateWithJoin` を利用
- 🚫 アプリ独自のファイル分割・独自ツリー結合・複数ファイル同時並列化は行わない
- 📦 SHA系と同じ1MiB・1パスのストリーミングI/Oを維持
- 🔌 外部ネイティブDLL不要
- 🚫 `Blake3.Native` 不使用

第三者ライセンスは `ArchiveHashVerifier_v1.1.4/ArchiveHashVerifier/THIRD-PARTY-NOTICES.txt` を参照してください。

## 🔏 OpenPGP / GPG

`.asc` / `.sig` の生成・検証には **GnuPG / Gpg4win** が必要です。GPGが無い場合でもHash機能は利用できます。

- 🔑 署名可能な秘密鍵を専用ダイアログから選択
- 🪪 Fingerprintで署名鍵を明示
- 🚫 失効・期限切れ・無効・署名不能な鍵は選択対象から除外
- 🛡️ `REVKEYSIG` / `EXPKEYSIG` / `EXPSIG` / `BADSIG` 等を安全側に判定
- 🔍 署名生成後の自動再検証をON/OFF可能（既定ON）
- 🔐 パスフレーズは保存せず、gpg-agent / Pinentryを利用
- 🧊 manifest / signature検証では固定byte列を使用し、処理途中の差替えを検知

Gpg4win: https://gpg4win.org/download.html

## 🛡️ 安全設計

- 🔒 元ファイルを変更・移動・削除しない
- 📁 生成物は元ファイルと同じフォルダーへ作成
- ⚠️ 既存生成物がある場合は上書きを事前確認
- 🧩 一時ファイルへ生成後、安全に正式成果物へ確定
- ♻️ 一括確定はbackup → commit → cleanupのtransaction設計
- 🧹 キャンセル時は未完成一時ファイルをcleanup
- 👀 処理中に元ファイルが変更された場合は失敗扱い
- 🚫 既存Hash / signature / manifest / `.ahvtmp-*` / `.ahvbak-*` を生成対象から除外
- 🔗 Junction / Symbolic Link等のReparse Pointを再帰的に追跡しない
- 🔑 GPGの秘密鍵・パスフレーズを設定ファイルへ保存しない

## 🖥️ UI

- 🔄 検証 / 生成モード切替
- ☑️ 一括出力チェックをモード選択の右側へ配置（生成時のみ有効）
- 🔠 Ctrl + マウスホイールで9～20ptのフォント変更
- 🖥️ DPI対応
- 🖱️ D&D対応
- ↕️ 可動式Splitter
- 📊 処理速度・進捗・GPG処理フェーズ表示
- 📋 詳細な完了ダイアログ / 結果コピー
- 📝 ログファイルは自動生成しません

## ⚙️ 設定ファイル

初回実行後、EXEと同じフォルダーへ保存します。

```text
ArchiveHashVerifier.settings.json
```

フォントサイズ、ウィンドウ状態、生成方式、一括出力設定、最終使用フォルダー、選択GPG Fingerprint等を保存します。秘密鍵やパスフレーズは保存しません。

## 💻 動作環境

- 🪟 Windows 11 x64
- 📦 GPGを使わない場合: 外部ランタイム不要（Self-contained）
- 🔏 OpenPGP機能を使う場合: GnuPG / Gpg4win

## 📦 v1.1.4 Release EXE / SHA-256

`ArchiveHashVerifier_v1.1.4.exe`

```text
SHA-256: 766A117F1654E5153D31F51F16672D2EAC6ABF5B19C853A6A96DCFB86F3CA0ED
Size:    52,090,701 bytes
```

SHA-256確認用ファイル:

```text
ArchiveHashVerifier_v1.1.4.exe.sha256
```

本EXEは個人公開のコード署名未署名EXEです。Windows SmartScreenが警告を表示する場合があります。配布元・公開ソース・SHA-256を確認して利用してください。

## 📜 第三者ライセンス

`Blake3` 3.0.2（BSD-2-Clause）の通知:

```text
ArchiveHashVerifier_v1.1.4/ArchiveHashVerifier/THIRD-PARTY-NOTICES.txt
```

## 🧑‍💻 最新ソース

v1.1.4の完全なソースコードと実行型回帰テストは、このリポジトリ内に公開しています。

```text
ArchiveHashVerifier_v1.1.4/
├─ ArchiveHashVerifier/
└─ ArchiveHashVerifier.Tests/
```

`bin/`、`obj/`、`publish/` 等のビルド成果物は含めていません。旧版ソースも保持しています。リポジトリのCodeメニューからソース一式をZIPで取得できます。

## 🔨 ビルド

.NET 10 SDKを導入したWindows 11 x64環境で実行します。

```powershell
cd .\ArchiveHashVerifier_v1.1.4
dotnet build .\ArchiveHashVerifier\ArchiveHashVerifier.csproj -c Release
```

単一EXE発行:

```powershell
dotnet publish .\ArchiveHashVerifier\ArchiveHashVerifier.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 🧪 テスト

```powershell
dotnet run --project .\ArchiveHashVerifier.Tests\ArchiveHashVerifier.Tests.csproj -c Release
```

v1.1.4正式Release時は **98 passed / 0 failed**、Release buildは **0 warning / 0 error**。

BLAKE3公式test vectors（0 / 1 / 1023 / 1024 / 1025 bytes）、一括Hash、transaction rollback/cleanup、manifest/OpenPGP、NO_PUBKEY、BADSIG等を含む回帰試験を実施しています。GPG統合試験は隔離した `GNUPGHOME` と使い捨て鍵を使用します。

DPIについては自動構造試験を実施済みで、100/125/150/200%の実モニター目視確認はRelease時点では未実施です。

## ⚠️ 注意

ハッシュ一致は、比較対象となる期待ハッシュと実ファイル内容が一致することを示します。OpenPGP署名検証は、署名鍵と署名データに基づく真正性・完全性確認を補助します。いずれも配布元やファイル内容そのものの安全性・合法性を保証するものではありません。
