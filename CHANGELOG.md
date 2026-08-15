# CHANGELOG

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
