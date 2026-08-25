# CHANGELOG

## v1.1.6

- checksumファイルを `StrictChecksumList` / `MalformedStrictChecksumList` / `LegacySidecar` 相当の3状態で分類するよう安全化
- strict形式を意図した破損・混在checksum-listをlegacy parserへfallbackさせずERRORとして拒否
- strict/legacy判定を先頭行だけでなく全非空行へ拡張し、I/O異常もlegacy扱いへ降格しない
- `BatchCancellationBeforeCommit` 回帰試験の誤った対象名を `a.bin.sha512` へ修正し、temp/backup不存在と元ファイル不変も確認
- GPG署名生成後の自動検証OFF時でも、秘密鍵metadataから実際のUser IDを取得して完了画面へ表示
- User ID取得のために `gpg --verify` は実行せず、`CreatedUnverified` / 「自動検証未実施」の意味を維持
- 隔離したGNUPGHOMEと使い捨て鍵を用いた自動検証OFFのUID保持回帰試験を追加
- 回帰試験を102件へ拡充。正式Release時 102 passed / 0 failed、Release build 0 warning / 0 error

## v1.1.5

- Batch ONで単体ファイルを処理する場合、元ファイル名を拡張子込みの基底名として成果物を生成するよう変更（例: `ClariS.zip.sha512`）
- 同名で拡張子だけ異なる `ClariS.zip` / `ClariS.7z` 等の成果物衝突を回避
- Batch ONで複数ファイルを処理する場合、Windows標準SaveFileDialogを1回表示し、任意の共通basenameを指定可能に変更
- SaveFileDialogの初期フォルダーを対象ファイルの共通parentへ設定し、成果物は対象ファイルと同一フォルダーへ限定
- SaveFileDialog Cancel時はHash/GPG/上書き/temp/backup等を開始せず無変更で終了
- 任意basenameの `.sha512` / `.sha256` / `.sha3-*` / `.blake3` checksum-listを直接D&Dして検証可能に拡張
- 元ファイルD&D時も同一フォルダー内の任意basename checksum-listからmatching entryを検出可能に拡張
- 任意basenameの `.manifest` / `.manifest.asc` / `.manifest.sig` を正式対応
- v1.1.4の `ArchiveHashVerifier.*` 固定名を後方互換として維持
- 完了ダイアログ表示直後の結果本文全選択を解消し、OKボタンへ初期focusするようUIを改善
- 回帰試験100件、Release build 0 warning / 0 error

## v1.1.4

- 複数ファイルのHashを方式ごとの固定ファイルへまとめる一括出力を追加（既定ON）
- 一括Hash: `ArchiveHashVerifier.sha512` / `.sha256` / `.sha3-512` / `.sha3-256` / `.blake3`
- 一括OpenPGP用の決定的JSON `ArchiveHashVerifier.manifest` と `.manifest.asc` / `.manifest.sig` に対応
- 一括manifestはSHA-512を必須収録し、選択Hashを同一1パスで計算
- 固定一括Hashの直接D&D、元ファイルD&Dからの該当entry自動検証、複数固定Hash同時D&Dに対応
- manifest / signatureを固定byte列で検証し、処理途中の差替えを検知するTOCTOU対策を追加
- NO_PUBKEY時は全署名結果を集約し、hard failureが無い場合のみ1回確認してHash-only継続を選択可能
- 一括生成のbackup → commit → cleanup transactionを実装。commit完了後のcleanup失敗では新版をrollbackしない
- `My  Video.mkv` 等、filename内に連続半角スペースを含む正当な一括Hash行を検証可能に修正
- manifest serializationを`Utf8JsonWriter`で明示順序化し、UTF-8 BOMなし / CRLF / 決定的出力を保証
- 「方式ごとに1ファイルへまとめて出力する」チェックをモード選択の右側へ移動し、生成モード時のみ有効化
- 回帰試験を98件へ拡充。正式Release時 98 passed / 0 failed、Release build 0 warning / 0 error

## v1.1.3

- BLAKE3（標準256bit）の生成・検証に対応
- BLAKE3をSHA-512 / SHA-256 / SHA3-512 / SHA3-256と同列のハッシュ方式として追加
- `.blake3` sidecar生成・検証、フォルダー再帰、D&D、自動検出、生成対象除外に対応
- BLAKE3は初期設定ON。SHA-512と同時に1パスで生成可能
- `Blake3` NuGet 3.0.2 完全マネージド版を採用
- CPU機能自動判定、SIMD最適化、`Hasher.UpdateWithJoin`によるライブラリ正式並列処理を利用
- 複数ファイル同時並列化や独自ファイル分割は導入せず、既存の1MiBストリーミングI/Oを維持
- BLAKE3公式test vectors（0 / 1 / 1023 / 1024 / 1025 bytes）を含む実行型回帰テスト87件を実施
- Blake3 3.0.2のBSD-2-Clause第三者ライセンス通知を追加

## v1.1.2

- v1.1.1で発生したGUI配色の回帰を修正
- v1.1.0と同等の明るい標準配色へ復帰
- DPI追従の太いSplitter、GPG安全化、詳細完了ダイアログ等のv1.1.1機能は維持

## v1.1.1

- OpenPGP署名検証で失効・期限切れ・不正署名を `VALIDSIG` より優先して安全側に判定
- 署名サブキーと鍵Capability判定を改善
- gpg-agent / Pinentry利用を改善
- GPGキャンセル時のプロセス終了、一時ファイルcleanup、処理中終了操作を安全化
- RadioButton表示、DPI/フォント対応、Splitter、GPGフェーズ表示を改善
- 完了ダイアログを詳細化し「結果をコピー」を追加
- 自動テストを62件へ拡充

## v1.1.0

- 検証 / 生成モードを追加
- SHA-512 / SHA-256 / SHA3-512 / SHA3-256 の生成に対応
- 複数ハッシュ方式の1パス計算に対応
- OpenPGP ASCII Detached Signature (`.asc`) の生成・検証に対応
- OpenPGP Binary Detached Signature (`.sig`) の生成・検証に対応
- GPG署名生成後の自動検証ON/OFFを追加
- GPG署名鍵選択、Fingerprint表示、安全な上書き・変更検知・キャンセル処理を追加
- 設定JSON、最終使用フォルダー、Ctrl+マウスホイールによる9～20ptフォント変更を追加
- .NET 10へ更新

## v1.0.5

- SHA-512 / SHA-256 / SHA3-512 / SHA3-256 の検証に対応した旧検証専用版
