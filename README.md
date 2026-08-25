# 🧪 ArchiveHashVerifier v1.1.6

ArchiveHashVerifier は、Windows 11 x64 向けの **ハッシュ生成・検証 / OpenPGP Detached Signature 生成・検証 GUIツール**です。

SHA-512 / SHA-256 / SHA3-512 / SHA3-256 / **BLAKE3** に対応し、GnuPG / Gpg4win が導入されている環境では OpenPGP の `.asc` / `.sig` も生成・検証できます。

- 💻 C# / Windows Forms
- 🧩 .NET 10
- 🪟 Windows 11 x64
- 📦 Self-contained / Single-file
- 👤 管理者権限不要
- ✅ 通常実行時の外部DLL不要
- ⚡ BLAKE3: `Blake3` NuGet 3.0.2（完全マネージド版）

## 🔐 プライバシーと透明性

- 📡 テレメトリ、アクセス解析、広告、ユーザー追跡機能は実装していません
- 📁 対象ファイルの内容、ファイル名、ハッシュ値等を開発者へ送信する処理は実装していません
- 🔑 GPGの秘密鍵やパスフレーズをArchiveHashVerifier自身が保存・収集することはありません
- ⚙️ 設定情報は `ArchiveHashVerifier.settings.json` としてローカル環境へ保存します
- 🛡️ OpenPGP処理には、利用者のPCへインストールされているGnuPG / Gpg4winを使用します
- 🌐 ArchiveHashVerifier自身には、動作に不要な外部通信や情報収集を行う機能を実装していません

配布EXEを利用する場合は、Releaseに添付されたSHA-256確認用ファイルとの一致も確認してください。

## ✨ 主な機能

### 🔎 検証モード（起動時の既定）

- SHA-512 / SHA-256 / SHA3-512 / SHA3-256 / BLAKE3
- OpenPGP ASCII Detached Signature (`.asc`)
- OpenPGP Binary Detached Signature (`.sig`)
- 複数ファイル、フォルダー再帰、ドラッグ＆ドロップ
- UNCパス / NAS上の大容量ファイル
- OK / NG / ERROR / SKIP / CANCEL の結果表示

認識する主なHash拡張子:

```text
.sha512 / .sha-512
.sha256 / .sha-256
.sha3-512 / .sha3_512
.sha3-256 / .sha3_256
.blake3
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

Hash行の標準形式:

```text
<ハッシュ値><半角スペース2個><元ファイル名>
```

複数Hash方式を選択しても、同じ元ファイルは可能な限り1回のストリーム読み込みで同時計算します。

## 📚 一括出力（v1.1.5以降）

生成モードの **「方式ごとに1ファイルへまとめて出力する」** は既定ONです。

### 単体ファイル

元ファイル名を**拡張子込み**で基底名として使用します。

例: `ClariS.zip`

```text
ClariS.zip.sha512
ClariS.zip.sha256
ClariS.zip.sha3-512
ClariS.zip.sha3-256
ClariS.zip.blake3
ClariS.zip.manifest
ClariS.zip.manifest.asc
ClariS.zip.manifest.sig
```

そのため、同一フォルダーに `ClariS.zip` と `ClariS.7z` が共存しても成果物名は衝突しません。

### 複数ファイル

同一フォルダーの複数ファイルを一括処理する場合、Windows標準の「名前を付けて保存」ダイアログを1回表示し、ユーザーが指定した共通基底名を使用します。

例: `ClariS_Complete`

```text
ClariS_Complete.sha512
ClariS_Complete.blake3
ClariS_Complete.manifest
ClariS_Complete.manifest.asc
ClariS_Complete.manifest.sig
```

成果物は対象ファイルと同じフォルダーへ保存します。Cancel時はHash/GPG/書込みを開始せず、成果物を変更しません。

### 任意basenameの検証

v1.1.5以降は固定名 `ArchiveHashVerifier.*` に依存せず、任意basenameのchecksum list / manifestを直接D&Dして検証できます。旧v1.1.4固定名も後方互換で引き続き検証可能です。

```text
MyArchive.sha512
MyArchive.blake3
MyArchive.manifest
MyArchive.manifest.asc
MyArchive.manifest.sig
```

## 🔏 OpenPGP / manifest

一括OpenPGP署名では、元ファイルへ個別署名する代わりに決定的JSON manifestを生成し、そのmanifestへDetached Signatureを作成します。

- manifestにはSHA-512を必須収録し、選択された他Hashも同じ読み込みで計算
- UTF-8 BOMなし / CRLF / 末尾改行 / property順固定
- timestamp、PC名、ユーザー名、絶対パスを記録しない
- basenameのみ収録
- `.asc` / `.sig` の両方が存在する場合は両方を検証
- 公開鍵不足時はhard failureが無い場合のみ、利用者の確認によりHash-only検証へ移行可能
- `BADSIG` / `ERRSIG` / `REVKEYSIG` / `EXPKEYSIG` / `EXPSIG` 等は安全側に判定
- manifest / signatureは検証開始時のbyte列を固定し、処理途中の差替えを検知

GPG署名生成後の自動検証をOFFにした場合でも、v1.1.6では選択された秘密鍵のmetadataから実際のUser IDを取得して完了画面へ表示します。このために `gpg --verify` を実行することはありません。

## 🛡️ checksum-list安全性（v1.1.6）

checksumファイルは、strict checksum-list / malformed strict / legacy sidecarを区別して処理します。

strict形式を意図した行を含むにもかかわらず全体が正しいchecksum-listとして成立しないファイルはERRORとし、legacy sidecar parserへfallbackして成功扱いしません。

- digest長・hexを厳格検証
- digest直後の半角スペース2個をseparatorとして扱う
- filename内の連続半角スペースを保持
- duplicate basenameをcase-insensitiveで拒否
- rooted / UNC / drive path / directory separator / `.` / `..` 等の危険な名前を拒否

## ⚡ BLAKE3

BLAKE3実装には `Blake3` NuGet 3.0.2 の完全マネージド版を使用しています。

- 標準256bit（32 bytes / 64桁hex）
- `.blake3` 生成・検証
- CPU機能を実行時に自動判定
- SIMD最適化をライブラリ側で利用
- `Hasher.UpdateWithJoin` を利用
- SHA系と同じ1MiB・1パスのストリーミングI/O
- `Blake3.Native` 不使用
- 外部ネイティブDLL不要

## 🔏 OpenPGP / GPG

`.asc` / `.sig` の生成・検証には **GnuPG / Gpg4win** が必要です。GPGが無い場合でもHash機能は利用できます。

- 署名可能な秘密鍵を専用ダイアログから選択
- Fingerprintで署名鍵を明示
- 失効・期限切れ・無効・署名不能な鍵は選択対象から除外
- gpg-agent / Pinentryを利用
- パスフレーズはArchiveHashVerifierへ保存しない

Gpg4win: https://gpg4win.org/download.html

## 🛡️ 安全設計

- 元ファイルを変更・移動・削除しない
- 一括生成対象は同一フォルダー内に限定
- 既存成果物がある場合は上書きを事前確認
- 一括処理では全衝突・上書き判断を完了してから変更を開始
- 一時ファイルへ生成後、安全に正式成果物へ確定
- backup → commit → cleanup のtransaction
- commit前障害時はrollback
- commit完了後のbackup cleanup失敗では新成果物をrollbackしない
- rollback失敗時は復旧用backupを保持
- 処理中に元ファイルが変更された場合は失敗扱い
- Hash / signature / manifest / `.ahvtmp-*` / `.ahvbak-*` を生成対象から除外
- Junction / Symbolic Link等のReparse Pointを再帰的に追跡しない

## 🖥️ UI

- 検証 / 生成モード切替
- 一括出力チェックは生成モード時のみ有効
- Ctrl + マウスホイールで9～20ptのフォント変更
- DPI対応
- D&D対応
- 可動式Splitter
- 処理速度・進捗・GPG処理フェーズ表示
- 詳細な完了ダイアログ / 結果コピー
- v1.1.5以降、完了ダイアログ表示直後に結果本文を全選択せずOKボタンへ初期focus
- ログファイルは自動生成しません

## ⚙️ 設定ファイル

初回実行後、EXEと同じフォルダーへ保存します。

```text
ArchiveHashVerifier.settings.json
```

フォントサイズ、ウィンドウ状態、生成方式、一括出力設定、最終使用フォルダー、選択GPG Fingerprint等を保存します。秘密鍵やパスフレーズは保存しません。

## 💻 動作環境

- Windows 11 x64
- GPGを使わない場合: 外部.NET Runtime不要（Self-contained）
- OpenPGP機能を使う場合: GnuPG / Gpg4win

## 📦 v1.1.6 Release EXE / SHA-256

`ArchiveHashVerifier_v1.1.6.exe`

```text
SHA-256: 7B16B19CBD6AD095B8376A3ED944B853FC867492A507AECDADC863F9570835BE
Size:    52,094,475 bytes
```

SHA-256確認用ファイル:

```text
ArchiveHashVerifier_v1.1.6.exe.sha256
```

本EXEは個人公開のコード署名未署名EXEです。Windows SmartScreen等が警告を表示する場合があります。配布元・公開ソース・SHA-256を確認して利用してください。

## 📜 第三者ライセンス

`Blake3` 3.0.2（BSD-2-Clause）の通知:

```text
ArchiveHashVerifier_v1.1.6/ArchiveHashVerifier/THIRD-PARTY-NOTICES.txt
```

## 🧑‍💻 最新ソース

v1.1.6のソースコードと実行型回帰テストは、正式Source ZIPとして公開しています。

```text
ArchiveHashVerifier_v1.1.6_Source.zip
```

ZIP内:

```text
ArchiveHashVerifier/
ArchiveHashVerifier.Tests/
```

`bin/`、`obj/`、`publish/` 等のビルド成果物は含めていません。旧版ソースも保持しています。

## 🔨 ビルド

.NET 10 SDKを導入したWindows 11 x64環境で実行します。

```powershell
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

v1.1.6正式Release時は **102 passed / 0 failed**、Release buildは **0 warning / 0 error**。

BLAKE3公式test vectors、一括Hash、任意basename、transaction rollback/cleanup、manifest/OpenPGP、NO_PUBKEY、BADSIG、strict/legacy分類、GPG自動検証OFF時のUID保持等を含む回帰試験を実施しています。GPG統合試験には隔離した `GNUPGHOME` と使い捨て鍵を使用します。

DPI全倍率および実利用鍵でのGUI目視については、自動試験とは別に利用環境での確認を推奨します。

## ⚠️ 注意

Hash一致は、比較対象となる期待Hashと実ファイル内容が一致することを示します。OpenPGP署名検証は、署名鍵と署名データに基づく真正性・完全性確認を補助します。いずれも配布元やファイル内容そのものの安全性・合法性を保証するものではありません。
