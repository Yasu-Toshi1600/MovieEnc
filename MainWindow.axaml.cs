using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Avalonia.Input;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.Diagnostics;

namespace SampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, DropFile);//ドロップ処理呼び出し
        this.Closing += SaveConfig;//終了時config保存呼び出し
        Console.OutputEncoding = Encoding.GetEncoding("utf-8"); //これ無いと文字化けする
        
        var config = ConfigManager.Load();
        //保存パス読み込み
        FolderPathTextBox.Text = config.OutputFolder ?? "";

        // エンコードモード読み込み（RadioButton）
        switch (config.SelectedEncodingMode)
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
            case "Radio9_5MB":
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
        var config = new AppConfig
        {
            OutputFolder = FolderPathTextBox.Text,
            SelectedEncodingMode = GetSelectedEncodingMode(),
            UseNvenc = NvencSwitch.IsChecked == true,
            UseAutoE = AutoEncodeSwitch.IsChecked == true,
        };
        //config書き込み呼び出し
        ConfigManager.Save(config);
    }

    private string? GetSelectedEncodingMode()
    {
        if (Radio1080p.IsChecked == true) return "Radio1080p";
        if (Radio720p.IsChecked == true) return "Radio720p";
        if (Radio480p.IsChecked == true) return "Radio480p";
        if (Radio9_5MB.IsChecked == true) return "Radio9_5MB";
        
        return null;
    }
    
    //MEconfig.json定義
    public class AppConfig
    {
        public string? OutputFolder { get; set; }
        public string? SelectedEncodingMode { get; set; }
        public bool UseNvenc { get; set; }
        public bool UseAutoE { get; set; }
    }
    
    //config設定
    public static class ConfigManager
    {
        //configパス宣言
        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "MEconfig.json");

        //config書き込み
        public static void Save(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        //configロード
        public static AppConfig Load()
        {
            if (!File.Exists(ConfigPath))
                return new AppConfig(); // デフォルト値

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
    }

    //動画情報取得
    public async Task <(int width, int height, double duration)> get_video_info(String filePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
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
            
            Console.WriteLine($"解像度: {width}x{height}, 時間: {duration} 秒");
            return (width, height, duration);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    //動画パス参照
    public async void file_ref(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = "動画ファイルを選択",
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "動画ファイル", Extensions = { "mp4", "mkv", "avi" } }
            }
        };

        var result = await dialog.ShowAsync(this);

        if (result != null && result.Length > 0)
        {
            FilePathTextBox.Text = result[0];
        }
    }
    
    //動画ドロップ処理
    private async void DropFile(object? sender, DragEventArgs e)
    {
        Console.WriteLine("DropFile fired！");

        var files = e.Data.GetFileNames();
        var file = files?.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(file))
        {
            Console.WriteLine($"ドロップファイル: {file}");
            FilePathTextBox.Text = file;
        }
    }
    
    //保存パス参照
    public async void folder_ref(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "保存先フォルダを選択"
        };

        var result = await dialog.ShowAsync(this);

        if (!string.IsNullOrWhiteSpace(result))
        {
            FolderPathTextBox.Text = result;
        }
    }
    
    //本処理
    private async void OnEncodeStart(object? sender, RoutedEventArgs e)
    {
        // 入力と出力のパスをここで取得（例として仮のパスを使用）
        var inputPath = FilePathTextBox.Text;
        var outputPath = FolderPathTextBox.Text;
        var (width, height, duration) = await get_video_info(inputPath);
        var outputname = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(inputPath)!, "output.mp4");
        
        if (!File.Exists(inputPath) || !Directory.Exists(outputPath))
        {
            Console.WriteLine("入力ファイルまたは保存先が無効！");
            return;
        }
        
        string mode = GetSelectedEncodingMode() ?? "Fast";
        bool useNvenc = NvencSwitch.IsChecked == true;
        string codec = useNvenc ? "h264_nvenc" : "libx264";
        
        var ffmpegPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg.exe");
        
        Console.WriteLine("▶ 実行コマンド:");
        Console.WriteLine($"\"{ffmpegPath}\" ");

        
        var arguments = $"-i \"{inputPath}\" -c:v libx264 -crf 23 \"{outputPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string errorLog = await process.StandardError.ReadToEndAsync(); // エラーログ見るなら
            await process.WaitForExitAsync();

            Console.WriteLine("変換完了！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex.Message}");
        }
    }

    
}