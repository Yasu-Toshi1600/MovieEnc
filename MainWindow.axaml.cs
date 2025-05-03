using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Input;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;


namespace MovieEnc;

public partial class MainWindow : Window
{
    private WindowNotificationManager _notifier;
    private AppConfig _loadedConfig;

    public MainWindow()
    {
        InitializeComponent();
        
        AddHandler(DragDrop.DropEvent, DropFile);//ファイルドロップ処理呼び出し
        
        this.Closing += SaveConfig;//終了時config保存呼び出し
        
        Console.OutputEncoding = Encoding.GetEncoding("utf-8"); //これ無いと文字化けする
        
        //通知設定
        _notifier = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 1
        };
        
        //configロード
        var config = ConfigManager.Load();
        
        //ウィンドウ位置読み込み
        if (config.WindowX != 0 || config.WindowY != 0) // (0,0)はデフォルトだから無視
        {
            this.Position = new PixelPoint(config.WindowX, config.WindowY);
        }
        
        //保存先読み込み
        FolderPathTextBox.Text = config.OutputFolder ?? "";

        // エンコードモード読み込み（RadioButton）
        switch (config.SelectedEncodeOption)
        {
            case "Radio1080p":
                Radio1080p.IsChecked = true;
                break;
            case "Radio720p":
                Radio720p.IsChecked = true;
                break;
            case "Radio480p":
                Radio480p.IsChecked = true;
                break;
            case "Radio9_5MB"://将来名前を変更予定
                Radio9_5MB.IsChecked = true;
                break;
        }
        
        //Nvenc・AutoEnc読み込み
        NvencSwitch.IsChecked = config.UseNvenc;
        AutoEncodeSwitch.IsChecked = config.UseAutoE;
    }
    
    //config保存
    private void SaveConfig(object? sender, WindowClosingEventArgs e)
    {
        _loadedConfig = ConfigManager.Load();
        var config = new AppConfig
        {
            
            OutputFolder = FolderPathTextBox.Text,
            SelectedEncodeOption = GetSelectedEncodeOption(),
            UseNvenc = NvencSwitch.IsChecked == true,
            UseAutoE = AutoEncodeSwitch.IsChecked == true,
            WindowX = this.Position.X,
            WindowY = this.Position.Y,
            Audiobitrate = _loadedConfig.Audiobitrate, // もともと読み込んだ設定をそのまま保存
            Videotargetcapacity = _loadedConfig.Videotargetcapacity
            
        };
        //config書き込み呼び出し
        ConfigManager.Save(config);
    }
    
    //json用
    public class AppConfig
    {
        public int Audiobitrate { get; set; }
        public double Videotargetcapacity { get; set; }
        public string? OutputFolder { get; set; }
        public string? SelectedEncodeOption { get; set; }
        public bool UseNvenc { get; set; }
        public bool UseAutoE { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }

    }
    
    //config管理
    public static class ConfigManager
    {
        //configパス宣言
        private static readonly string ConfigDir = Path.Combine(AppContext.BaseDirectory, "Data");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "MEconfig.json");
        
        //config書き込み
        public static void Save(AppConfig config)
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        //configロード
        public static AppConfig Load()
        {
            if (!File.Exists(ConfigPath))
            {
                // 初回だけデフォルト値設定
                var defaultConfig = new AppConfig
                {
                    Audiobitrate = 128000,
                    Videotargetcapacity = 9.2,
                    OutputFolder = "",
                    SelectedEncodeOption = "Radio1080p",
                    UseNvenc = false,
                    UseAutoE = false,
                    WindowX = 100,
                    WindowY = 100
                };
                Save(defaultConfig); // 作ったデフォルトを書き込んで
                return defaultConfig; // それを返す
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
    }

    //動画情報確認
    public async Task <(double duration, Boolean vertical)> get_video_info(String filePath)
    {
        var ffprobePath = Path.Combine(AppContext.BaseDirectory, "Tools", "ffprobe.exe");
        var psi = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height,duration -of json \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        try
        {
            using JsonDocument doc = JsonDocument.Parse(output);
            var stream = doc.RootElement.GetProperty("streams")[0];

            int width = stream.GetProperty("width").GetInt32();
            int height = stream.GetProperty("height").GetInt32();
            string durationStr = stream.GetProperty("duration").GetString();
            double duration = double.TryParse(durationStr, out var d) ? d : 0;
            duration = Math.Round(duration, 1);
            
            bool vertical = width < height; //縦長ならtrue
            
            Console.WriteLine($"解像度: {width}x{height}, 時間: {duration} 秒, 縦長: {vertical}");
            
            return (duration,vertical);
        }
        catch
        {
            return ( 0,false);
        }
    }
    
    //ビットレート計算
    public int bitrate_calculation(double duration,int audioBitrate,double videoCapacity)
    {
        
        long targetSizeBits =  (long)(videoCapacity* 1024 * 1024 * 8);
        double audioTotalBits = audioBitrate * duration;
        double videoTotalBits = targetSizeBits - audioTotalBits;

        if (videoTotalBits <= 0) return -1;
        
        int videoBitrateKbps = (int)(videoTotalBits / duration / 1000);

        if (videoBitrateKbps <= 0) return -1;

        return videoBitrateKbps;
    }

    //EncodeOption管理
    private string? GetSelectedEncodeOption()
    {
        if (Radio1080p.IsChecked == true) return "Radio1080p";
        if (Radio720p.IsChecked == true) return "Radio720p";
        if (Radio480p.IsChecked == true) return "Radio480p";
        if (Radio9_5MB.IsChecked == true) return "Radio9_5MB";
        
        return null;
    }
    
    //動画パス参照
    public async void file_ref(object sender, RoutedEventArgs args)
    {
        var dialog = new FilePickerOpenOptions
        {
            Title = "動画ファイルを選択",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("動画ファイル")
                {
                    Patterns = new[] { "*.mp4", "*.mkv", "*.avi" }
                }
            }
        };

        var files = await StorageProvider.OpenFilePickerAsync(dialog);

        if (files is { Count: > 0 })
        {
            var file = files[0].Path.LocalPath; // 選択したファイルのパス
            FilePathTextBox.Text = file;

            if (AutoEncodeSwitch.IsChecked == true)
            {
                OnEncodeStart(this, new RoutedEventArgs());
            }
        }
    }
    
    //動画ドロップ処理
    private void DropFile(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFileNames();
        var file = files?.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(file))
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();

            // 対応拡張子だけ許可
            if (extension == ".mp4" || extension == ".mkv" || extension == ".avi")
            {
                Console.WriteLine($"ドロップファイル: {file}");
                FilePathTextBox.Text = file;
                if (AutoEncodeSwitch.IsChecked == true)
                {
                    OnEncodeStart(this, new RoutedEventArgs());
                }
            }
            else
            {
                Console.WriteLine("対応していないファイル形式です！");
                _notifier.Show(new Notification(
                    "エラー",
                    "対応していないファイル形式です（.mp4, .mkv, .aviのみ対応）",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
            }
        }
    }
    
    //保存パス参照
    public async void folder_ref(object sender, RoutedEventArgs args)
    {
        var dialog = new FolderPickerOpenOptions
        {
            Title = "保存先フォルダを選択"
        };

        var folder = await StorageProvider.OpenFolderPickerAsync(dialog);

        if (folder is { Count: > 0 })
        {
            var folderPath = folder[0].Path.LocalPath;
            FolderPathTextBox.Text = folderPath;
        }
    }
    
    //ログ処理
    private bool log_analysis()
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "Data", "ffmpeg_log.txt");
        string content = File.ReadAllText(logPath);
        var lowered = content.ToLower();
        
        string[] errorKeywords = {
            "error", "invalid", "data found", "not found" , "not supported" ,
            "failed", "unrecognized", "permission", "denied",
            "cannot", "unable", "empty", "corrupt"
        };

        var matchedKeywords = errorKeywords.Where(k => lowered.Contains(k)).ToList();

        if (matchedKeywords.Any())
        {
            //nvenc関連
            if (matchedKeywords.Contains("nvenc") && (matchedKeywords.Contains("not found") || matchedKeywords.Contains("not supported")))
            {
                _notifier.Show(new Notification(
                    "エンコード失敗",
                    $"このPCではNVENCは使用できません。"+$"詳細は{logPath}を確認してください。",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
                foreach (var word in matchedKeywords)
                {
                    Console.WriteLine($" - {word}");
                }
            }
            //データ破損
            else if (matchedKeywords.Contains("invalid") && matchedKeywords.Contains("data found"))
            {
                _notifier.Show(new Notification(
                    "エンコード失敗",
                    "動画データが破損してる可能性があります。"+$"詳細は{logPath}を確認してください。",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
                
                Console.WriteLine("エラーメッセージ:");
                foreach (var word in matchedKeywords)
                {
                    Console.WriteLine($" - {word}");
                }
            }
            //書き込み権限なし
            else if (matchedKeywords.Contains("permission") && matchedKeywords.Contains("denied"))
            {
                _notifier.Show(new Notification(
                    "エンコード失敗",
                    $"書き込み先のファイルにアクセス権限がありません。"+$"詳細は{logPath}を確認してください。",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
                
                Console.WriteLine("エラーメッセージ:");
                foreach (var word in matchedKeywords)
                {
                    Console.WriteLine($" - {word}");
                }
            }
            else
            {
                _notifier.Show(new Notification(
                    "エンコード失敗",
                    $"エラーが発生しました。"+$"詳細は{logPath}を確認してください。",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
                
                Console.WriteLine("エラーメッセージ:");
                foreach (var word in matchedKeywords)
                {
                    Console.WriteLine($" - {word}");
                }
            }
            
            Console.WriteLine("ログ:");
            Console.WriteLine(lowered); //全文出力
            return false;
        }
        else
        {
            Console.WriteLine("エンコード成功 !");
            //Console.WriteLine(lowered); //ログは無し
            return true;
        }
    }
    
    //本処理
    private async void OnEncodeStart(object? sender, RoutedEventArgs e)
    {
        var config = ConfigManager.Load();
        
        int audioBitrate = config.Audiobitrate;
        double videoCapacity = config.Videotargetcapacity;
        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg.exe");
        var inputPath = FilePathTextBox.Text;
        var outputPath = FolderPathTextBox.Text;
        String mode = GetSelectedEncodeOption() ?? "Radio1080p";
        var scalingFilter = "";
        var resolutionStr = ""; //今後使う予定
        
        //エンコード中は無効化
        EncodeStartButton.IsEnabled = false;
        
        //ファイルパス確認
        if (!File.Exists(inputPath) || !Directory.Exists(outputPath))
        {
            Console.WriteLine("入力ファイルまたは保存先が無効！");
            _notifier.Show(new Notification(
                "エンコード失敗",
                $"入力ファイルまたは保存先が無効。",
                NotificationType.Error)
                {
                    Expiration = TimeSpan.Zero
                }
            );
            return;
        }
        
        //動画情報取得
        var (duration,vertical) = await get_video_info(inputPath);
        
        //コマンド引数作成
        List<String> command = new List<string>
        {
            "-i",$"\"{inputPath}\"",
        };
        
        //解像度入力 
        if (mode == "Radio1080p" || mode == "Radio720p" || mode == "Radio480p")
        {
            Dictionary<string, string> presets;
            if (vertical)
            {
                presets = new Dictionary<string, string>
                {
                    { "Radio480p", "480:-1" },
                    { "Radio720p", "720:-1" },
                    { "Radio1080p", "1080:-1" }
                };
            }
            else
            {
                presets = new Dictionary<string, string>
                {
                    { "Radio480p", "-1:480" },
                    { "Radio720p", "-1:720" },
                    { "Radio1080p", "-1:1080" }
                };
            }
            scalingFilter = $"scale={presets[mode]}";
            Console.WriteLine($"エンコード解像度: {scalingFilter}\n");
            command.AddRange(new[] { "-vf", scalingFilter });
        }
        else if (mode == "Radio9_5MB")
        {
            scalingFilter = vertical ? "scale=360:-1" : "scale=-1:360";
            int videoBitrateKbps = bitrate_calculation(duration,audioBitrate,videoCapacity);
            if (videoBitrateKbps <= 0)
            {
                Console.WriteLine($"エンコード解像度: {scalingFilter}, ビットレート :{videoBitrateKbps}, 目標容量 :{videoCapacity}MB\n");
                _notifier.Show(new Notification(
                    "エンコード失敗",
                    "動画の時間が長すぎます。",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
                return;
            }
            Console.WriteLine($"エンコード解像度: {scalingFilter}, ビットレート :{videoBitrateKbps}, 目標容量 :{videoCapacity}MB\n");
            command.AddRange(new[] { "-vf", scalingFilter ,"-b:v", $"{videoBitrateKbps}k"});
        }
        
        //Nvencの関連
        bool useNvenc = NvencSwitch.IsChecked == true;
        if (mode == "Radio1080p" || mode == "Radio720p" || mode == "Radio480p")
        {
            if (useNvenc) //ビットレート未指定＆Nvenc使用
            {
                command.AddRange(new[] { "-c:v", "h264_nvenc", "-preset", "medium", "-cq", "23" });
            }
            else //ビットレート未指定&Nvenc未使用
            {
                command.AddRange(new[] { "-c:v", "libx264", "-preset", "medium", "-crf", "23" });
            }
            
        }
        else if (mode == "Radio9_5MB")
        {
            if (useNvenc) //ビットレート指定＆Nvenc使用
            {
                command.AddRange(new[] { "-c:v", "h264_nvenc", "-preset", "slow" });
            }
            else //ビットレート指定&Nvenc未使用
            {
                command.AddRange(new[] { "-c:v", "libx264", "-preset", "slower" });
            }
        }

        //出力ファイル処理
        String filename = Path.GetFileNameWithoutExtension(inputPath);
        String extension = ".mp4";
        String outputFilename = Path.Combine(outputPath, filename + extension);
        
        int counter = 1;
        while (File.Exists(outputFilename))
        {
            outputFilename = Path.Combine(outputPath, $"{filename}_{counter}{extension}");
            counter++;
        }
        
        
        // オーディオ設定&出力ファイル設定
        int audioBitRate = audioBitrate / 1000;
        command.AddRange(new[] { "-c:a", "aac", "-b:a", $"{audioBitRate}k", "-ar", "44100",  $"\"{outputFilename}\""});
        Console.WriteLine(string.Join(" ", command));
        
        //実行準備
        string arguments = string.Join(" ", command);
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process { StartInfo = psi };
        
        try
        {
            //プロセス起動
            bool started = process.Start();
            if (!started)
            {
                _notifier.Show(new Notification(
                    "エンコード失敗",
                    "エラーが発生しました。",
                    NotificationType.Error)
                    {
                        Expiration = TimeSpan.Zero
                    }
                );
                Console.WriteLine("\nProcess failed to start.");
                return;
            }
            
            Console.WriteLine($"\nProcess started! PID: {process.Id}");

            // ログ取得
            string logs = await process.StandardError.ReadToEndAsync();

            // プロセス終了を待つ
            await process.WaitForExitAsync();

            // リザルト取得
            File.Delete("Data/ffmpeg_log.txt");
            File.WriteAllText("Data/ffmpeg_log.txt", logs);
            bool result = log_analysis();
            
            //retultがtrueなら成功 falseの場合log_analysis確認
            if (result)
            {
                _notifier.Show(new Notification(
                    "エンコード成功",
                    $"ファイル名: {outputFilename}\n保存先: {outputPath}",
                    NotificationType.Success
                ));
            }
             
        }
        catch (Exception ex)
        {
            Console.WriteLine("プロセス起動エラー: " + ex.Message);
            _notifier.Show(new Notification(
                "エンコード失敗",
                $"エラーが発生しました。",
                NotificationType.Error)
                {
                    Expiration = TimeSpan.Zero
                }
            );
        }
        finally
        {
            EncodeStartButton.IsEnabled = true;
        }
    }
}

// resolutionStr 今後使用予定
//Radio9_5MB もっと汎用的にする