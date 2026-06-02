# ArchiveHashVerifier v1.0.5

ArchiveHashVerifier は、Windows 11 x64 向けのハッシュ検証GUIツールです。  
対象ファイル、ハッシュファイル、またはフォルダをドラッグ＆ドロップすることで、SHA-512 / SHA-256 / SHA3-512 / SHA3-256 のハッシュ一致確認を行えます。

プログラムの透明性を確保するため、実行ファイルだけでなく、C# / .NET 8 / Windows Forms によるソースコード一式も公開しています。  
利用者はソースコードを確認し、必要に応じて自身の環境でビルドして検証できます。

## ダウンロード

最新版は GitHub Releases からダウンロードしてください。

- `ArchiveHashVerifier_v1.0.5.exe`
- `ArchiveHashVerifier_v1.0.5.exe.sha256`

## 初回実行時の注意

この実行ファイルは、個人公開の未署名EXEです。  
初回実行時に Windows SmartScreen の警告が表示される場合があります。

警告が表示された場合は、配布元、SHA-256、ソースコード内容を確認したうえで、利用者自身の判断で実行してください。  
この表示は、コード署名証明書による署名が無い個人公開ソフトで一般的に発生し得るものであり、直ちにマルウェアであることを意味するものではありません。

## SHA-256

`ArchiveHashVerifier_v1.0.5.exe` の SHA-256 は以下です。

```text
061c09127109dd0305cd9d597be79136764d1159ee2d56dd6a8b3f516f80e578
```

## 主な機能

- SHA-512 検証
- SHA-256 検証
- SHA3-512 検証
- SHA3-256 検証
- `.sha512` / `.sha256` / `.sha3-512` / `.sha3-256` 対応
- `.sha3_512` / `.sha3_256` の表記ゆれ対応
- 対象ファイル本体のドラッグ＆ドロップ対応
- ハッシュファイルのドラッグ＆ドロップ対応
- フォルダの再帰検証対応
- 複数ファイル検証対応
- 検証ログ表示
- ログ保存対応
- Windows 11 x64 向け単一EXE

## 動作環境

- Windows 11 x64
- .NET 8 self-contained build
- 外部インストール不要
- 管理者権限不要
- 外部通信不要
- 外部 NuGet パッケージ不要

## 使い方

1. GitHub Releases から `ArchiveHashVerifier_v1.0.5.exe` をダウンロードします。
2. 必要に応じて `ArchiveHashVerifier_v1.0.5.exe.sha256` でEXEのハッシュを確認します。
3. アプリを起動します。
4. 検証したいファイル、ハッシュファイル、またはフォルダを画面へドラッグ＆ドロップします。
5. 検証結果の OK / NG / ERROR とログを確認します。

## 対応例

- `sample.zip` + `sample.zip.sha512`
- `sample.zip` + `sample.zip.sha256`
- `sample.zip` + `sample.zip.sha3-512`
- `sample.zip` + `sample.zip.sha3-256`
- `sample.mkv` + `sample.mkv.sha512`
- `sample.flac` + `sample.flac.sha3-256`

## OK / NG / ERROR の意味

- OK: 期待ハッシュと実測ハッシュが一致
- NG: 期待ハッシュと実測ハッシュが不一致
- ERROR: 対応ファイルが見つからない、ハッシュ値を読めない、読み取りエラーが発生した等

## ソースコード

主要なソースコードは以下にあります。

```text
ArchiveHashVerifier_GUI/
```

主な構成は以下です。

```text
ArchiveHashVerifier_GUI.sln
ArchiveHashVerifier_GUI/ArchiveHashVerifier_GUI.csproj
ArchiveHashVerifier_GUI/MainForm.cs
ArchiveHashVerifier_GUI/Program.cs
ArchiveHashVerifier_GUI/app.ico
ArchiveHashVerifier_GUI/app_icon.svg
ArchiveHashVerifier_GUI/CHANGELOG.md
ArchiveHashVerifier_GUI/README.md
```

## ビルド方法

Windows 11 x64 と .NET 8 SDK が入っている環境で、以下を実行します。

```powershell
cd "ArchiveHashVerifier_GUI"
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

生成されるEXE名はプロジェクト設定により `ArchiveHashVerifier_v1.0.5.exe` です。

## 安全性について

このツールは検証対象ファイルとハッシュファイルを読み取るだけで、削除・移動・変更は行いません。  
また、外部通信は行わず、ハッシュ計算は .NET 8 標準の暗号 API を使用します。

プログラムの透明性を確保するため、実行ファイルだけでなくソースコードも公開しています。  
そのため、実行ファイルをそのまま使用するだけでなく、ソースコードを確認し、自身の環境でビルドして動作を検証することもできます。

## 注意

このツールはファイルのハッシュ値一致を確認するためのものです。  
一致した場合、その検証範囲ではファイル破損・ダウンロード不完全・改ざんの形跡が認められないことを示します。

ただし、配布元そのものの安全性、ファイル内容の合法性、実行時の完全な安全性を保証するものではありません。  
入手元が信頼できるかどうかは、利用者自身でも確認してください。
