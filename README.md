# ArchiveHashVerifier v1.1.2

ArchiveHashVerifier は、Windows 11 x64 向けの **ハッシュ生成・検証 / OpenPGP Detached Signature 生成・検証 GUIツール**です。

SHA-512 / SHA-256 / SHA3-512 / SHA3-256 を扱い、GnuPG / Gpg4win が導入されている環境では OpenPGP の `.asc` / `.sig` も生成・検証できます。

- C# / Windows Forms
- .NET 10
- Windows 11 x64
- Self-contained / Single-file
- 管理者権限不要
- 外部 NuGet パッケージ不要

## 主な機能

### 検証モード（起動時の既定）

- SHA-512 / SHA-256 / SHA3-512 / SHA3-256
- OpenPGP ASCII Detached Signature (`.asc`)
- OpenPGP Binary Detached Signature (`.sig`)
- 複数ファイル、フォルダー再帰、ドラッグ＆ドロップ
- UNCパス / NAS上の大容量ファイル
- OK / NG / ERROR / SKIP / CANCEL の結果表示

検証時は以下の表記を認識します。

```text
.sha512 / .sha-512
.sha256 / .sha-256
.sha3-512 / .sha3_512
.sha3-256 / .sha3_256
.asc / .sig
```

### 生成モード

以下を任意に同時生成できます。

```text
SHA-512   -> .sha512
SHA-256   -> .sha256
SHA3-512  -> .sha3-512
SHA3-256  -> .sha3-256
OpenPGP ASCII Detached Signature  -> .asc
OpenPGP Binary Detached Signature -> .sig
```

ハッシュファイルは次の形式です。

```text
<ハッシュ値><半角スペース2個><元ファイル名>
```

複数のハッシュ方式を選択しても、元ファイルは可能な限り1回のストリーム読み込みで同時計算します。

## OpenPGP / GPG

`.asc` / `.sig` の生成・検証には **GnuPG / Gpg4win** が必要です。GPGが無い場合でもSHA-2 / SHA-3機能は利用できます。

- 登録済みの署名可能な秘密鍵を専用ダイアログから選択
- Fingerprintで署名鍵を明示
- 失効・期限切れ・無効・署名不能な鍵は選択対象から除外
- `REVKEYSIG` / `EXPKEYSIG` / `EXPSIG` / `BADSIG` 等を安全側に判定
- 署名生成後の自動再検証をON/OFF可能（既定ON）
- パスフレーズは保存せず、通常の gpg-agent / Pinentry を利用

Gpg4win: https://gpg4win.org/download.html

## 安全設計

- 元ファイルを変更・移動・削除しない
- 生成物は元ファイルと同じフォルダーへ作成
- 既存生成物がある場合は上書きを確認
- 一時ファイルへ生成し、成功後に正式成果物へ確定
- キャンセル時は未完成一時ファイルを削除
- 処理中に元ファイルが変更された場合は失敗扱い
- フォルダー再帰時は既存ハッシュ / `.asc` / `.sig` / 一時ファイルを生成対象から除外
- Junction / Symbolic Link 等のReparse Pointを再帰的に追跡しない
- GPGの秘密鍵・パスフレーズを設定ファイルへ保存しない

## UI

- 検証 / 生成モード切替
- Ctrl + マウスホイールでフォントサイズを1ptずつ変更（9～20pt）
- DPI対応
- D&D対応
- 可動式Splitter
- 処理速度・進捗・GPG処理フェーズ表示
- 詳細な完了ダイアログ
- 完了結果をクリップボードへコピー可能
- ログファイルは自動生成しません

## 設定ファイル

初回実行後、EXEと同じフォルダーへ保存します。

```text
ArchiveHashVerifier.settings.json
```

フォントサイズ、ウィンドウ状態、生成方式、最終使用フォルダー、選択GPG Fingerprint等を保存します。秘密鍵やパスフレーズは保存しません。

## 動作環境

- Windows 11 x64
- GPGを使わない場合: 外部ランタイム不要（Self-contained）
- OpenPGP機能を使う場合: GnuPG / Gpg4win

## v1.1.2 Release EXE / SHA-256

`ArchiveHashVerifier_v1.1.2.exe` のSHA-256:

```text
B9467C72FD694AA1D67282DBFE918B2A2F4F85853E8786253A30B51E0A2002E1
```

ReleaseからEXEを取得した場合は、上記SHA-256との一致を確認してください。

このEXEは個人公開のコード署名未署名EXEです。Windows SmartScreenが警告を表示する場合があります。配布元、SHA-256、公開ソースコードを確認したうえで利用してください。

## 最新ソース

v1.1.2のソースは次にあります。

```text
ArchiveHashVerifier_v1.1.2/
  ArchiveHashVerifier.sln
  ArchiveHashVerifier/
    ArchiveHashVerifier.csproj
    Dialogs.cs
    FileDiscovery.cs
    GpgService.cs
    HashService.cs
    MainForm.cs
    Models.cs
    ProcessingCoordinator.cs
    Program.cs
    SettingsService.cs
    app.ico
  ArchiveHashVerifier.Tests.zip
```

`ArchiveHashVerifier.Tests.zip` には、v1.1.2開発時に使用した62件の自動テストソース（`ArchiveHashVerifier.Tests.csproj` / `Program.cs`）を収録しています。

`bin/`、`obj/`、`publish/` 等のビルド成果物はソースディレクトリへ含めていません。旧v1.0.5の公開ソース・EXEもリポジトリ上に保持しています。

## ビルド

.NET 10 SDKを導入したWindows 11 x64環境で実行します。

```powershell
cd .\ArchiveHashVerifier_v1.1.2
dotnet build .\ArchiveHashVerifier.sln -c Release
```

単一EXE発行:

```powershell
dotnet publish .\ArchiveHashVerifier\ArchiveHashVerifier.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## テスト

`ArchiveHashVerifier.Tests.zip` を `ArchiveHashVerifier_v1.1.2` 直下へ展開し、`ArchiveHashVerifier.Tests` フォルダーを作成した状態で実行します。

```powershell
dotnet run --project .\ArchiveHashVerifier.Tests\ArchiveHashVerifier.Tests.csproj -c Release
```

v1.1.2開発時は62件すべて成功しています。GPG統合試験は隔離した `GNUPGHOME` と使い捨て鍵を使用します。

## 注意

ハッシュ一致は、比較対象となる期待ハッシュと実ファイル内容が一致することを示します。OpenPGP署名検証は、署名鍵と署名データに基づく真正性・完全性確認を補助します。いずれも配布元やファイル内容そのものの安全性・合法性を保証するものではありません。
