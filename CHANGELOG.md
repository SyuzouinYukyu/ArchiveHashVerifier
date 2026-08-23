# CHANGELOG

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
