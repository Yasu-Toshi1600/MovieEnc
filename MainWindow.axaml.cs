using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace SampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, DropFile);
        
    }
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
    private void DropFile(object? sender, DragEventArgs e)
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

    
    
}