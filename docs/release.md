# Release の作り方

ビルド済みの ZIP を GitHub Releases で公開する手順。
リポジトリの管理者が、`main` に取り込んだ版を配布するときに使う。

## 仕組み

`v` で始まるタグを GitHub に push すると、GitHub Actions の Release workflow（`.github/workflows/release.yml`）が Windows の runner で次を行う。

1. タグの形式と、タグの commit が `main` に含まれることを確かめる。満たさなければ、ここで失敗する。
2. `scripts/check.ps1` で書式・ビルド・コード分析・テストを実行する。
3. `scripts/build-package.ps1 -Version <タグの v を除いた値>` で ZIP を作る。実行ファイルのプロパティの「ファイル バージョン」と「製品バージョン」に、この値が入る。
4. ZIP を添付した Release を **下書き（Draft）** で作る。Release ノートには、前回のタグ以降にマージした PR の一覧が自動で入る。

下書きの Release は、リポジトリに書き込める人にしか見えない。
「Publish release」を押すまで、利用者には公開されない。

タグは `v0.1.0` のように `v` とバージョン番号の 3 つの数字で付ける。
`v0.1.0-beta.1` のようにハイフンの後ろに文字を付けると、プレリリース（Pre-release）として作られる。

ZIP の名前は版によらず `OyasumiGray.Windows-win-x64.zip` とする。
公開した最新の Release（プレリリースを除く）の ZIP は、
`https://github.com/yutotamura55/oyasumi-gray-windows/releases/latest/download/OyasumiGray.Windows-win-x64.zip` で常に取得できる。

## 手順

1. 公開する版の確認を [検証状況](verification.md) に記録し、`main` の CI が成功していることを確かめる。
2. 最新の `main` にタグを付けて push する。`v0.1.0` は例なので、公開する版の番号に置き換える。

   ```bash
   git switch main
   git pull --ff-only
   git tag v0.1.0
   git push origin v0.1.0
   ```

3. GitHub のリポジトリで **Actions → Release** を開き、push したタグの run が成功するまで待つ。
4. リポジトリのトップページ右側の **Releases** を開く。「Draft」と表示された Release から ZIP をダウンロードし、Windows で展開して起動できることを確かめる。
5. 下書きの右上の鉛筆アイコンから編集画面を開く。Release ノートに、確認済みの範囲と未確認の範囲（[検証状況](verification.md) の要約）を書き足す。
6. 編集画面の下の「Publish release」を押して公開する。
7. 最初の Release を公開したときは、README の「入手と起動」から「現在はまだありません」の文を削除する PR を作る。

## 失敗したとき

Release workflow が失敗した場合は、run のログで失敗した段階を確かめ、原因を `main` で直す。
同じタグ名で作り直すには、下書きの Release があれば **Releases** の画面で削除してから、タグを削除して手順 2 からやり直す。

```bash
git push origin --delete v0.1.0
git tag -d v0.1.0
```

公開した Release のタグは削除・付け替えをしない。
ダウンロードした人が持つ ZIP と、タグが指す版が食い違うため。
公開後に問題が見つかった場合は、修正して次の版番号で公開する。
