using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using FolderGram.Extensions;
using FolderGram.ViewModels;

using WTelegram;

namespace FolderGram.Views
{
    public partial class MainWindow : Window
    {
        const string extensions = ".webm, .mkv, .flv, .mov, .wmv, .avi, .mkv, .mpg, .m4v, .flv";
        private Client? _client;
        private IStorageFolder? Folder { get; set; }
        private MainWindowViewModel? Model { get; set; }

        public MainWindow ()
        {
            InitializeComponent();
        }

        protected async override void OnLoaded (RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                Model = viewModel;
            }

            if (this.VisualRoot is Window window)
            {
                var file = await window.StorageProvider.TryGetFileFromPathAsync("telegram.json");

                if (file != null)
                {
                    var allTextStream = await file.OpenReadAsync();

                    using StreamReader streamReader = new(allTextStream);
                    var allTextJson = streamReader.ReadToEnd();

                    var telegram = JsonSerializer.Deserialize<Telegram>(allTextJson);

                    if (telegram is not null)
                    {
                        txtApiId.Text = telegram.ApiId;
                        txtHash.Text = telegram.ApiHash;
                        txtPhone.Text = telegram.Phone;
                        txtFFPath.Text = telegram.EnginePath;
                    }
                }
            }

            base.OnLoaded(e);
        }

        private async void Login_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (txtApiId.Text.IsNull())
            {
                txtApiId.Focus();
                return;
            }

            if (txtHash.Text.IsNull())
            {
                txtApiId.Focus();
                return;
            }

            if (txtPhone.Text.IsNull())
            {
                txtApiId.Focus();
                return;
            }

            _ = int.TryParse(txtApiId.Text, out var apiId);

            _client = new Client(apiId, txtHash.Text);
            Helpers.Log = (l, s) =>
            {
                if (Model is not null)
                {
                    Model.Output = s;
                }
            };

            await DoLogin(txtPhone.Text!);
        }

        private async void SendCode_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (confirmCode.Text.IsNull())
            {
                confirmCode.Focus();
                return;
            }

            await DoLogin(confirmCode.Text!);
        }

        private async void SelectFolder_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var root = this.VisualRoot as Window;

            var folders = await root!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select a folder"
            });

            var folder = folders?[0];

            if (folder is not null)
            {
                Folder = folder;
                selectedFolder.Content = folder.Name;
            }
        }

        private async void Upload_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            StringBuilder errors = new();
            if (Folder is null)
            {
                return;
            }

            if (channelsList.SelectedItem is null)
            {
                return;
            }

            bool convert = false;

            if (convertToMp4.IsChecked is true)
            {
                if (txtFFPath.Text.IsNull())
                {
                    txtFFPath.Focus();
                    return;
                }
                convert = true;
            }

            await foreach (var file in Folder.GetItemsAsync())
            {
                if (file is not IStorageFile)
                {
                    continue;
                }

                try
                {
                    FileInfo fileInfo = new(file.Path.LocalPath);

                    if (fileInfo.Length > 2040109465.6)
                    {
                        errors.Append("File exceeds the telegram limit of 2 GB ").Append(fileInfo.Name).AppendLine();
                        continue;
                    }

                    if (convert && extensions.Contains(fileInfo.Extension, System.StringComparison.OrdinalIgnoreCase))
                    {
                        await ConvertToMp4Upload(fileInfo, txtFFPath.Text!);
                    }
                    else
                    {
                        await UploadAndSendFile(fileInfo.FullName, fileInfo.Name);
                    }
                }
                catch (Exception ex)
                {
                    errors.Append("Error while uploading file ").Append(file.Name).AppendLine();
                    errors.Append("Error: ").Append(ex.Message).AppendLine();
                    continue;
                }
            }

            Model!.Progress = 0;
            statusText.Text = errors.ToString();
        }

        private async Task ConvertToMp4Upload (FileInfo file, string path)
        {
            var converter = new Converter(Model!);
            var mp4Path = file.Name + ".mp4";
            statusText.Text = $"Converting {file.Extension} file to mp4";
            await converter.StartConverting(file.FullName, mp4Path, path, CancellationToken.None);

            await UploadAndSendFile(mp4Path, file.Name);

            File.Delete(mp4Path);
        }

        private async Task UploadAndSendFile (string filePath, string fileName)
        {
            var channel = (Channel)channelsList.SelectedItem!;

            if (channel.Chat!.IsBanned())
            {
                return;
            }
            Model!.Progress = 0;
            statusText.Text = $"Uploading file {filePath}";
            var file = await _client!.UploadFileAsync(filePath, (p, r) =>
            {
                Model!.Progress = p * 100 / r;
            });

            statusText.Text = $"Sending file message to {channel.Title}";
            await _client.SendMediaAsync(channel.Chat, fileName, file);
            statusText.Text = "Done";
            await Task.Delay(100);

            statusText.Text = "";
        }

        private async Task DoLogin (string loginInfo)
        {
            string what = await _client!.Login(loginInfo);
            if (what != null)
            {
                labelCode.Content = what + ':';
                confirmCode.Text = "";
                secretCodePanel.IsVisible = labelCode.IsVisible = confirmCode.IsVisible = true;
                confirmCode.Focus();
                return;
            }

            if (_client.User != null)
            {
                secretCodePanel.IsVisible = false;
                userLable.Content = $"Logged in as {_client.User.first_name}";

                var allChats = await _client.Messages_GetAllChats();

                var channels = allChats.chats.Where(x => x.Value.IsActive).Select(x => new Channel
                {
                    Chat = x.Value,
                    Id = x.Key,
                    Title = x.Value.Title
                }).OrderBy(x => x.Title).ToList();

                if (channels.Count != 0)
                {
                    channelsList.ItemsSource = channels;
                }

                if (this.VisualRoot is Window window)
                {
                    var file = await window.StorageProvider.TryGetFileFromPathAsync("telegram.json");

                    if (file is null)
                    {
                        Telegram telegram = new
                        (
                            txtApiId.Text,
                            txtHash.Text,
                            txtPhone.Text,
                            txtFFPath.Text
                        );

                        var json = JsonSerializer.Serialize(file);

                        System.IO.File.WriteAllText("telegram.json", json);
                    }
                }
            }
        }
    }
}