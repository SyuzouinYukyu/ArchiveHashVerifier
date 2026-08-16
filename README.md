# 🧪 ArchiveHashVerifier v1.1.3

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

ソースコードは、透明性の確保と利用者自身による動作内容の確認・検証を可能にするため、GitHub上で公開しています。

利用者はソースコードを確認し、必要に応じて自身のWindows環境でビルド・検証できます。

なお、ソースコードが公開されていること自体が、配布された実行ファイルの安全性を保証するものではありません。  
ダウンロードしたEXEについては、公開しているSHA-256との一致もあわせて確認してください。

## ✨ 主な機能

### 🔎 検証モード（起動時の既定）

- ✅ SHA-512 / SHA-256 / SHA3-512 / SHA3-256 / BLAKE3
- 📝 OpenPGP ASCII Detached Signature (`.asc`)
- 💾 OpenPGP Binary Detached Signature (`.sig`)
- 📂 複数ファイル、フォルダー再帰、ドラッグ＆ドロップ
- 🌐 UNCパス / NAS上の大容量ファイル
- 📋 OK / NG / ERROR / SKIP / CANCEL の結果表示

検証時は以下の表記を認識します。

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

BLAKE3は標準256bit（32 bytes / 64桁hex）の通常Hashモードを使用します。

ハッシュファイルは次の形式です。

```text
<ハッシュ値><半角スペース2個><元ファイル名>
```

複数のハッシュ方式を選択しても、元ファイルは1回のストリーム読み込みで同時計算します。

## ⚡ BLAKE3

BLAKE3実装には `Blake3` NuGet 3.0.2 の完全マネージド版を使用しています。

- ✅ 標準256bit BLAKE3
- 📄 `.blake3` 生成・検証
- 🧠 CPU機能を実行時に自動判定
- 🚀 SIMD最適化をライブラリ側で利用
- 🧵 `Hasher.UpdateWithJoin` による正式な並列処理を利用
- 🚫 アプリ独自のファイル分割・独自ツリー結合・複数ファイル同時並列化は行わない
- 📦 SHA系と同じ1MiB・1パスのストリーミングI/Oを維持
- 🔌 外部ネイティブDLL不要
- 🚫 `Blake3.Native` 不使用

第三者ライセンスは `ArchiveHashVerifier_v1.1.3/THIRD-PARTY-NOTICES.txt` を参照してください。

## 🔏 OpenPGP / GPG

`.asc` / `.sig` の生成・検証には **GnuPG / Gpg4win** が必要です。

GPGが無い場合でもSHA-2 / SHA-3 / BLAKE3機能は利用できます。

- 🔑 登録済みの署名可能な秘密鍵を専用ダイアログから選択
- 🪪 Fingerprintで署名鍵を明示
- 🚫 失効・期限切れ・無効・署名不能な鍵は選択対象から除外
- 🛡️ `REVKEYSIG` / `EXPKEYSIG` / `EXPSIG` / `BADSIG` 等を安全側に判定
- 🔍 署名生成後の自動再検証をON/OFF可能（既定ON）
- 🔐 パスフレーズは保存せず、通常の gpg-agent / Pinentry を利用

Gpg4win: https://gpg4win.org/download.html

## 🛡️ 安全設計

- 🔒 元ファイルを変更・移動・削除しない
- 📁 生成物は元ファイルと同じフォルダーへ作成
- ⚠️ 既存生成物がある場合は上書きを確認
- 🧩 一時ファイルへ生成し、成功後に正式成果物へ確定
- 🧹 キャンセル時は未完成一時ファイルを削除
- 👀 処理中に元ファイルが変更された場合は失敗扱い
- 🚫 フォルダー再帰時は既存ハッシュ / `.blake3` / `.asc` / `.sig` / 一時ファイルを生成対象から除外
- 🔗 Junction / Symbolic Link 等のReparse Pointを再帰的に追跡しない
- 🔑 GPGの秘密鍵・パスフレーズを設定ファイルへ保存しない

## 🖥️ UI

- 🔄 検証 / 生成モード切替
- 🔠 Ctrl + マウスホイールでフォントサイズを1ptずつ変更（9～20pt）
- 🖥️ DPI対応
- 🖱️ D&D対応
- ↕️ 可動式Splitter
- 📊 処理速度・進捗・GPG処理フェーズ表示
- 📋 詳細な完了ダイアログ
- 📎 完了結果をクリップボードへコピー可能
- 📝 ログファイルは自動生成しません

## ⚙️ 設定ファイル

初回実行後、EXEと同じフォルダーへ保存します。

```text
ArchiveHashVerifier.settings.json
```

フォントサイズ、ウィンドウ状態、生成方式、最終使用フォルダー、選択GPG Fingerprint等を保存します。

秘密鍵やパスフレーズは保存しません。

v1.1.2形式の設定JSONにBLAKE3項目が存在しない場合は、BLAKE3を既定ONとして読み込みます。

## 💻 動作環境

- 🪟 Windows 11 x64
- 📦 GPGを使わない場合: 外部ランタイム不要（Self-contained）
- 🔏 OpenPGP機能を使う場合: GnuPG / Gpg4win

## 📦 v1.1.3 Release EXE / SHA-256

`ArchiveHashVerifier_v1.1.3.exe` のSHA-256:

```text
64D897CC16C48371414D2E41FFF09BDC0F8499AEBB0C762CF5EC4435B8976255
```

EXEサイズ:

```text
52,075,335 bytes
```

ReleaseからEXEを取得した場合は、上記SHA-256との一致を確認してください。

SHA-256確認用ファイル:

```text
ArchiveHashVerifier_v1.1.3.exe.sha256
```

本EXEは個人公開のコード署名未署名EXEです。  
Windows SmartScreenが警告を表示する場合があります。

配布元、公開されているソースコード、およびSHA-256を確認したうえで利用してください。

## 📜 第三者ライセンス

BLAKE3実装として使用している `Blake3` 3.0.2 の第三者ライセンス情報は、以下で確認できます。

```text
ArchiveHashVerifier_v1.1.3/THIRD-PARTY-NOTICES.txt
```

Release Assetにも以下のファイルを添付しています。

```text
THIRD-PARTY-NOTICES.txt
```

## 🧑‍💻 最新ソース

v1.1.3の完全なソースコードと87件の実行型回帰テストは、GitHub ReleaseのAssetとして公開しています。

```text
ArchiveHashVerifier_v1.1.3_Source.zip
```

Source ZIPには以下を収録しています。

```text
ArchiveHashVerifier/
ArchiveHashVerifier.Tests/
```

`bin/`、`obj/`、`publish/` 等のビルド成果物は含めません。

リポジトリ内の `ArchiveHashVerifier_v1.1.3/THIRD-PARTY-NOTICES.txt` でも第三者ライセンスを確認できます。

旧版の公開ソースも保持しています。

GitHubが自動生成する以下のソースアーカイブも利用できます。

```text
Source code (zip)
Source code (tar.gz)
```

## 🔨 ビルド

.NET 10 SDKを導入したWindows 11 x64環境でSource ZIPを展開後に実行します。

```powershell
cd .\ArchiveHashVerifier_v1.1.3
dotnet build .\ArchiveHashVerifier\ArchiveHashVerifier.csproj -c Release
```

単一EXE発行:

```powershell
dotnet publish .\ArchiveHashVerifier\ArchiveHashVerifier.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Publish時はEXEと第三者ライセンス通知 `THIRD-PARTY-NOTICES.txt` が出力されます。

EXE本体は単体で動作し、外部ランタイムDLLを必要としません。

## 🧪 テスト

```powershell
dotnet run --project .\ArchiveHashVerifier.Tests\ArchiveHashVerifier.Tests.csproj -c Release
```

v1.1.3開発時は **実行型回帰テスト87/87件成功**。

BLAKE3については公式 `test_vectors.json` に基づき、以下の入力長を検証しています。

```text
0 bytes
1 byte
1023 bytes
1024 bytes
1025 bytes
```

さらに以下を確認しています。

- ✅ 通常の `Update`
- ✅ `UpdateWithJoin`
- ✅ ArchiveHashVerifierのストリーミング経路
- ✅ XOF出力先頭32 bytes
- ✅ 標準256bit出力との一致

GPG統合試験は隔離した `GNUPGHOME` と使い捨て鍵を使用します。

## ⚠️ 注意

ハッシュ一致は、比較対象となる期待ハッシュと実ファイル内容が一致することを示します。

OpenPGP署名検証は、署名鍵と署名データに基づく真正性・完全性確認を補助します。

いずれも配布元やファイル内容そのものの安全性・合法性を保証するものではありません。
