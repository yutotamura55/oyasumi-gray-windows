# おやすみグレー for Windows

指定時刻から Windows のデスクトップ全体を徐々にグレースケールへ変え、朝の指定時刻にカラーへ戻す通知領域アプリです。同じコンセプトの [Chrome 拡張 oyasumi-gray](https://github.com/yutotamura55/oyasumi-gray) は Web ページだけを対象にします。両方を有効にするとブラウザ上で効果が重なります。

## 対応範囲

Windows 11 x64 の通常の SDR デスクトップを初版対象とします。色の変更には Windows の Magnification API を使用します。HDR、独占全画面ゲーム、保護された動画、リモートデスクトップ、ロック画面では動作を保証しません。

## 使い方

1. `artifacts/OyasumiGray.Windows-win-x64.zip` を Windows 側のローカルフォルダーへ展開します。WSL 上の実行ファイルを直接使わないでください。
2. `OyasumiGray.Windows.exe` を実行します。初回は設定画面が開きます。
3. 開始時刻、移行時間、復帰時刻を確認し、「自動切替を有効にする」を選んで保存します。初期値は 22:00、60 分、07:00 です。
4. 以後は通知領域のアイコンから設定、一時解除、停止、終了を操作できます。

ログオン起動は初期状態でオフです。必要な場合に設定画面で有効にしてください。アプリを終了すると、保存された自動切替設定は次回起動時に再開されます。自動切替そのものを止めるときは「今すぐカラーに戻して停止」を使います。

競合が見つかった場合は自動適用を止めます。強制終了後に色変換が残った場合、次回起動時の設定画面から、確認のうえで現在の色変換を解除できます。

## 開発

.NET 10 SDK が必要です。Windows でビルドする場合:

```powershell
dotnet build OyasumiGray.sln -c Release
dotnet run --project tests/OyasumiGray.Core.Tests/OyasumiGray.Core.Tests.csproj
dotnet publish src/OyasumiGray.Windows/OyasumiGray.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

時刻計算と状態管理のテストは OS 非依存です。実際の画面への適用は Windows 実機で確認してください。設計上の動作と受け入れ条件は [設計書](docs/design.md)、実施済みの確認は [検証状況](docs/verification.md) にあります。
