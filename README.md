# おやすみグレー for Windows

指定時刻から Windows の画面を徐々にグレースケールにし、朝の指定時刻にカラーへ戻す通知領域アプリです。
夜の PC 利用を終えるきっかけとして作っています。

現在は開発中です。
Windows 11 x64 の通常の SDR デスクトップを対象に設計しています。
通常ユーザーの Windows 11 で、デスクトップ・タスクバー・ブラウザへの中間色の適用と、
ロック・スリープからの復帰、停止・終了時のカラー復帰を確認しました。
3 台のモニターそれぞれへの適用と、モニターの切断・再接続、解像度と拡大縮小の変更の後も、色の変更が続くことを確認しました。
確認済みの範囲は [検証状況](docs/verification.md) に記録しています。

## 入手と起動

### Release から入手する

ビルド済みの ZIP は [GitHub Releases][releases] の添付ファイルとして配布します。
現在は Release がまだありません。
Release のページの「Assets」から `OyasumiGray.Windows-win-x64.zip` をダウンロードしてください。
同じ欄の「Source code」はソースコードの圧縮ファイルで、そのままでは起動できません。
Release の ZIP は .NET ランタイムを含むため、.NET を別にインストールする必要はありません。

このアプリには電子署名がありません。
ダウンロードした ZIP から展開した `OyasumiGray.Windows.exe` を開くと、
「Windows によって PC が保護されました」という Microsoft Defender SmartScreen の警告が表示されることがあります。
その場合は「詳細情報」を選び、表示される「実行」を押すと起動できます。
Windows 11 のスマート アプリ コントロールがオンの PC では、署名のないアプリとして起動がブロックされることがあります。

### ソースからパッケージを作る

Windows 11 x64 と [.NET 10 SDK][dotnet-sdk] を用意してください。
リポジトリのルートで PowerShell から次を実行します。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-package.ps1
```

`-ExecutionPolicy Bypass` は、このコマンドで起動する PowerShell にだけ適用されます。
PC 全体の設定は変更しません。

`artifacts/OyasumiGray.Windows-win-x64.zip` が作成されます。
GitHub Releases で ZIP を公開する手順は [Release の作り方](docs/release.md) にあります。

### 起動と初期設定

Release から入手した ZIP、またはソースから作った ZIP を任意のフォルダーに展開し、
`OyasumiGray.Windows.exe` を実行してください。

初回起動時に設定画面が開きます。
開始時刻、移行時間、復帰時刻を確認し、「自動切替を有効にする」を選んで保存してください。
初期値は 22:00、60 分、07:00 で、自動切替は初期状態でオフです。
設定画面を閉じてもアプリは終了せず、タスクバーの通知領域にアイコンが残ります。

アイコンを右クリックすると、設定画面を開く、一時解除、停止、終了を選べます。
「終了」を選ぶとアプリが終了します。
保存済みの自動切替設定は次回起動時にも使われます。
自動切替を止めるには「今すぐカラーに戻して停止」を選んでください。

Windows にサインインしたとき自動で起動したい場合は、
設定画面の「Windows へのログオン時に起動」にチェックを入れ、
「設定を保存」を押してください。
ログオン起動には OyasumiGray.Windows.exe の保存場所を登録します。
設定後にこのファイルを移動すると、次回のサインイン時に自動起動できません。

ほかのアプリによる画面の色変更を検出すると、自動適用を停止します。
強制終了後に色変換が残ると、次回起動時の設定画面に解除ボタンが表示されます。
「現在の画面の色変更を解除する」を選び、確認画面で解除するか決めてください。
この操作は、ほかのアプリの色変換も解除する可能性があります。

[Chrome 拡張 oyasumi-gray][chrome] は Web ページを対象にします。
両方を有効にすると、ブラウザ上では効果が重なります。
HDR、独占全画面ゲーム、保護された動画、リモートデスクトップ、ロック画面での動作は保証していません。

## ソースコードの構成

`src/OyasumiGray.Core` は、時刻計算・設定保存・色変換の制御を扱います。
`src/OyasumiGray.Windows` は、設定画面・通知領域・Windows API を扱います。
[設計書](docs/design.md) は実装前の判断と受け入れ条件を記録した文書です。
[実装計画](docs/implementation-plan.md) には残る実機確認項目があります。

## ビルドとテスト

Windows でビルドとテストを実行するコマンドは次のとおりです。

```powershell
dotnet build OyasumiGray.sln -c Release
dotnet run --project tests/OyasumiGray.Core.Tests -c Release
dotnet run --project tests/OyasumiGray.Windows.Tests -c Release
```

Core のテストは OS 非依存です。
設定画面のテストと実際の画面への適用確認は Windows で行ってください。
ZIP の作成には Windows PowerShell を使用します。

## コミット前の検査

コミットの前に、リポジトリのルートで PowerShell から次を実行してください。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check.ps1
```

PowerShell 7 がある場合は `pwsh -NoProfile -File .\scripts\check.ps1` でも実行できます。CI はこちらを使います。

このスクリプトは次の順に実行し、1つでも失敗すると止まります。

1. 書式の検査：`dotnet format whitespace --verify-no-changes`。書式のずれを見つけたら、`dotnet format OyasumiGray.sln` で直せます。
2. ビルドとコード分析：`dotnet build -c Release`。.NET のコード分析とコードスタイルの規則に違反するとビルドが失敗します。
3. Core のテスト。
4. 設定画面のテスト（Windows だけ。ほかの OS では警告を表示して省略します）。

規則の設定は `.editorconfig` と `Directory.Build.props` にあります。
`Directory.Build.props` の `TreatWarningsAsErrors` により、警告はすべてビルドエラーになります。
コード分析（CA で始まる規則）の水準は、`AnalysisLevel` で .NET 10 の既定に固定しています。
コードスタイル（IDE で始まる規則）と書式の判定は、インストールされている SDK の版によって変わることがあります。
`.editorconfig` では、導入時点のコードが守っていたコードスタイルの規則を選んで有効にしています。

ソースファイルの改行コードは LF に統一しています（`.gitattributes` と `.editorconfig`）。
この設定より前に Windows で clone し、改行コードが CRLF のままのファイルがあると、書式の検査が失敗します。
その場合は `dotnet format OyasumiGray.sln` を一度実行すると、改行コードが LF に変わります。
Git 上の内容は変わらないため、この操作でコミットすべき差分は出ません。

GitHub Actions は PR と `main` への push のたびに、Windows の runner で同じスクリプトを実行します。
`v` で始まるタグを push したときも同じスクリプトを実行し、ZIP を添付した Release の下書きを作ります（[Release の作り方](docs/release.md)）。

## ライセンス

[MIT License](LICENSE) です。

[releases]: https://github.com/yutotamura55/oyasumi-gray-windows/releases
[dotnet-sdk]: https://dotnet.microsoft.com/download/dotnet/10.0
[chrome]: https://github.com/yutotamura55/oyasumi-gray
