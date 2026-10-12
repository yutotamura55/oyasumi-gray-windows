# おやすみグレー for Windows

指定時刻から Windows の画面を徐々にグレースケールにし、朝の指定時刻にカラーへ戻す通知領域アプリです。夜の PC 利用を終えるきっかけとして作っています。

現在は開発中で、ダウンロードできる正式な ZIP は公開していません。Windows 11 x64 の通常の SDR デスクトップを対象に設計していますが、実際の画面への適用や複数モニターでの動作はまだ確認できていません。確認済みの範囲は [検証状況](docs/verification.md) に記録しています。

## 入手と起動

検証を終えた ZIP は [GitHub Releases](https://github.com/yutotamura55/oyasumi-gray-windows/releases) の添付ファイルとして配布する予定です。現在は Release がないため、試す場合は Windows 11 x64 と [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) を用意し、リポジトリのルートから PowerShell で次を実行してください。

```powershell
./scripts/build-package.ps1
```

`artifacts/OyasumiGray.Windows-win-x64.zip` が作成されます。ZIP を Windows 側の任意のフォルダーへ展開し、`OyasumiGray.Windows.exe` を実行してください。ログオン起動を使う場合は、先に実行ファイルを置く場所を決めてください。ZIP と発行したファイルは Git の管理対象ではありません。

初回起動時に設定画面が開きます。開始時刻、移行時間、復帰時刻を確認し、「自動切替を有効にする」を選んで保存してください。初期値は 22:00、60 分、07:00 で、自動切替は初期状態でオフです。設定画面を閉じても通知領域で動作します。

通知領域のアイコンから設定、一時解除、停止、終了を操作できます。「終了」は今回の常駐を終える操作で、保存済みの自動切替設定は次回起動時にも使われます。自動切替を止めるには「今すぐカラーに戻して停止」を選んでください。ログオン起動は設定画面で明示的に有効にした場合だけ登録します。

ほかのアプリによる画面の色変更を検出すると、自動適用を停止します。強制終了後に色変換が残った場合は、次回起動時の設定画面で確認したうえで「現在の画面の色変更を解除する」を選べます。この操作はほかのアプリの色変換も解除する可能性があります。

[Chrome 拡張 oyasumi-gray](https://github.com/yutotamura55/oyasumi-gray) は Web ページを対象にします。両方を有効にすると、ブラウザ上では効果が重なります。HDR、独占全画面ゲーム、保護された動画、リモートデスクトップ、ロック画面での動作は保証していません。

## 開発する

プロジェクトは、時刻計算・設定保存・色変換の制御を扱う `src/OyasumiGray.Core` と、設定画面・通知領域・Windows API を扱う `src/OyasumiGray.Windows` に分かれています。[設計書](docs/design.md) は実装前の判断と受け入れ条件を記録した文書です。[実装計画](docs/implementation-plan.md) には残る実機確認項目があります。

Windows でビルドとテストを実行するコマンドは次のとおりです。

```powershell
dotnet build OyasumiGray.sln -c Release
dotnet run --project tests/OyasumiGray.Core.Tests/OyasumiGray.Core.Tests.csproj -c Release
dotnet run --project tests/OyasumiGray.Windows.Tests/OyasumiGray.Windows.Tests.csproj -c Release
```

Core のテストは OS 非依存です。設定画面のテストと実際の画面への適用確認は Windows で行ってください。
ZIP 作成も Windows PowerShell で行います。WSL 上のビルド成果物を直接起動せず、Windows 側のフォルダーへ展開してください。

## ライセンス

[MIT License](LICENSE) です。
