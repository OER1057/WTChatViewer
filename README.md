# WTChatViewer

[War Thunder](https://warthunder.com)のゲーム内チャットを取得し、表示、翻訳、外部アプリへ転送するアプリケーションです。PowerShellスクリプト[Read-WTChat](https://github.com/OER1057/Read-WTChat)をC#で書き直し、たぶん若干高速、軽量になっています。

たとえば、[AssistantSeika](https://hgotoh.jp/wiki/doku.php/documents/voiceroid/assistantseika/)や[SofTalk](https://w.atwiki.jp/softalk/)などのコマンドラインインターフェースを持つソフトを介し、CeVIO AIなどに読み上げてもらうことができます。

## 動作確認環境

- OS: Windows 11 64bit
- War Thunder: 最新版

頑張ればLinux用とかにもコンパイルできると思います、知らんけど。

## 機能・設定

サンプルを参考に設定ファイルを作成してください。このファイルは本体に更新があっても引き続き使え(るように努力し)ます。`Read-WTChat`と内容は同じで形式は似ていますが互換性はありません。欠けている項目があった場合、そこには既定の設定(`config_minimum.json`と同様)が適用されます。

### チャット取得

- `Interval` : チャット取得間隔をミリ秒単位で指定。CPUを食いすぎな場合は大きくする。
- `IgnoreEnemy` : 敵チームからの全体チャットを無視する。`true`で有効、`false`で無効。
- `IgnoreSenders` : 特定のユーザ(自分自身等)からのチャットを無視する。`[]`の中に`"ユーザ名"`を`,`区切りで指定。

### 翻訳

- `TrnsEnable` : チャット内容を翻訳する。`true`で有効、`false`で無効。
- `TargetLang` : 翻訳先。Google翻訳に対応している翻訳先の言語コードで指定。

有効の場合は`翻訳前の文 (翻訳後の文)`の形式で表示します。

### 外部連携

- `PassEnable` : チャット内容をコマンドライン経由で任意のアプリに渡す。`true`で有効、`false`で無効。
- `PassFileName` : 実行ファイルへのパスを指定。(バックスラッシュ`\`は`\\`と表記)
- `PassArguments` : 実行時に渡す引数を指定。`%Text`がチャット内容に置換される。
- `ReplaceList` : 特定の文字列を別の文字列で置き換える。`[]`の中に`{"From": "置換前", "To": "置換後"}`を`,`区切りで指定。

翻訳が有効の場合は翻訳結果を渡します。AssistantSeikaとSofTalkの設定例を用意しているのでそちらも参考にしてください。

## ランタイムのインストール

[Microsoft .NET Runtime 7.0](https://dotnet.microsoft.com/ja-jp/download/dotnet/7.0)がインストールされていない場合はインストールしてください。

`winget list --id Microsoft.DotNet`を実行し、`Microsoft.DotNet.Runtime.7`や`Microsoft.DotNet.DesktopRuntime.7`があればインストール済みです。

`winget install Microsoft.DotNet.Runtime.7`でインストールできます。

## 実行

設定ファイルを`WTChatViewer.exe`にドラッグアンドドロップするか、`WTChatViewer.exe 設定ファイル名`コマンドを実行してください。

ファイル名を指定せず実行した場合は、`WTChatViewer.exe`と同じディレクトリにある`config.json`の読み込みを試み、存在しなかった場合は既定の設定で動作します。

バッチファイルを用意すると毎回ファイル名を指定せずに済みます。

## 仕組み

チャット内容はWar Thunderの「ブラウザでマップを開く」機能で用いられているAPIで取得しています。`http://localhost:8111/gamechat?lastId=(整数n)`にアクセスすると、n+1以降のidがついているチャットがJson形式で返ってきます。idはWar Thunder起動時からの通し番号のようです。自分のPC以外に負荷を与えたり、不正行為と判定されたりする可能性は原理上ない(はず)です。

翻訳はGoogle翻訳の非公開APIを使用しています。制限等は不明ですので、あらかじめご了承ください。

## 注意点

### 免責事項

公開用に作ったものではなく色々と雑かもしれないので、自己責任でご利用ください。OSごと落ちて戦績の悪化等あっても責任は負いかねます。

### 既知の不具合

[Issues](https://github.com/OER1057/WTChatViewer/issues)参照