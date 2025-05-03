<h1>MovieEncについて</h1>
本アプリは FFmpeg を使用したWindows 11向けの動画変換ツールです。<br>
GUI から FFmpeg を呼び出し、CPU または NVENC  による高速変換をサポートしています。

## 主な機能
1. 解像度または容量指定による動画のエンコード
2. 対応ファイル :  `.mp4`  `.mkv`  `.avi`
3. 対応コーデック : `h.264`
4. 動画ファイルのドラッグ&ドロップに対応
5. CPU または NVENC による高速変換
6. エラー発生時は通知でお知らせ
7. 設定は自動で保存され、次回起動時に自動で読み込まれます

## セットアップ方法
1. 以下のいずれかの方法で、FFmpeg をダウンロードしてください：
    - https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip から直接ダウンロード
    - https://www.gyan.dev/ffmpeg/builds/ から 「Release builds」の```ffmpeg-release-essentials.zip```を選択
2. GitHub のリリース欄から `MovieEnc_v2.0.3.zip` をダウンロード
3. `ffmpeg-7.1.1-essentials_build.zip`と`MovieEnc_v2.0.3.zip` を解凍してください
4. `ffmpeg-7.1.1-essentials_build\ffmpeg-7.1.1-essentials_build\bin` の `ffmpeg.exe` と `ffprobe.exe` を、`MovieEnc_v2.0.3\Tools` の中にコピー
<p align="center">
 <img src="https://github.com/user-attachments/assets/1d3d0ed4-772c-4e4e-9dcd-2dbe4aa8f7fa" width="500" >
</p>
5. この状態になればセットアップ完了です

## 初回起動時のご注意

本アプリは個人で開発・配布しています。そのため、Windows Defender SmartScreen によって  
「発行元不明」として警告が表示される場合があります。  
その際は「詳細情報」→「実行」を選択して起動してください。

※ソースコードはすべて公開されており、不正な処理は一切行っておりません。


## 使い方
1. `MovieEnc_v2.0.3` を実行
2. 対象の動画を **参照** または **動画ファイルを選択** の部分にドロップ
3. 動画の保存先を **参照** から選択
4. エンコード品質・NVENC使用の有無などを設定し、**エンコード開始**を押すだけ

## 注意事項
1. NVENCを使用する際はGPU Driverを **バージョン 572.70以上** で使用してください
2. 一部の古いGPUではNVENCが使えない可能性があります
3. このソフトウェアはMITライセンスのもとで提供されています。  
ご利用は **自己責任** でお願いします。

## バージョン履歴

### v2.0.3
- このバージョンは初の一般公開リリースです。<br>
以前のバージョンはすべて開発用バージョンです
### v2.0.2以下
- 開発用バージョンのため現在非公開
  
