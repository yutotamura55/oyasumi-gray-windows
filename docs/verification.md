# 検証状況

2026-09-26 時点。

- .NET 10 SDK で Release ビルドと win-x64 の自己完結型発行に成功。
- OS 非依存のテスト 11 件が通過。開始・移行・復帰、日付越え、一時解除、夏時間、行列、競合、保存の破損を確認。
- WSL から起動した Windows PowerShell で、通常ユーザーの対話セッションから `MagInitialize` と `MagGetFullscreenColorEffect` に成功。現在の行列は単位行列だった。
- 同じセッションで、単位行列の `MagSetFullscreenColorEffect` に成功。画面の色は変更していない。

Windows 上でアプリを起動し、中間色の見え方、朝の復帰、通知領域 UI、スリープ復帰、複数モニター、HDR との組合せはまだ確認していない。ここまでの確認だけで、それらの動作を保証しない。
