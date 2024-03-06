using System;
using System.Collections.Generic;
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

using TL;

using WTelegram;

namespace FolderGram.Views
{
    public partial class MainWindow : Window
    {
        FileSystemWatcher watcher;

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

                var syncFile = await window.StorageProvider.TryGetFileFromPathAsync("sync.json");

                if (syncFile is not null)
                {
                    var allTextStream = await syncFile.OpenReadAsync();

                    using StreamReader streamReader = new(allTextStream);
                    var allTextJson = streamReader.ReadToEnd();

                    var syncDetail = JsonSerializer.Deserialize<SyncDetail>(allTextJson);

                    if (syncDetail is not null)
                    {
                        Model!.SyncDetail = syncDetail;
                        txtSyncFolder.Text = syncDetail.FolderPath;
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

            var channel = (ViewModels.Channel)channelsList.SelectedItem!;

            if (channel.Chat!.IsBanned())
            {
                errors.AppendLine("Chat is banned");

                statusText.Text = errors.ToString();

                return;
            }

            await foreach (var file in Folder.GetItemsAsync())
            {
                if (file is not IStorageFile && file is IStorageFolder directory)
                {
                    try
                    {
                        await _client!.SendMessageAsync(channel.Chat!, $"==================={Environment.NewLine}{directory.Name}");

                        await foreach (var item in directory.GetItemsAsync())
                        {
                            if (item is not IStorageFile)
                            {
                                continue;
                            }

                            await UploadFiles(errors, convert, item, channel.Chat!);
                        }

                        await _client!.SendMessageAsync(channel.Chat!, $"{directory.Name}{Environment.NewLine}===================");
                    }
                    catch (Exception ex)
                    {
                        errors.Append("Error while sending message ").AppendLine(ex.Message);
                        continue;
                    }
                }

                try
                {
                    if (file is not IStorageFile)
                    {
                        continue;
                    }

                    await UploadFiles(errors, convert, file, channel.Chat!);
                }
                catch (Exception ex)
                {
                    errors.Append("Error while uploading file ").AppendLine(file.Name);
                    errors.Append("Error: ").AppendLine(ex.Message);
                    continue;
                }
            }

            Model!.Progress = 0;
            statusText.Text = errors.ToString();
        }

        private async Task UploadFiles (StringBuilder errors, bool convert, IStorageItem file, ChatBase chatBase)
        {
            FileInfo fileInfo = new(file.Path.LocalPath);

            if (fileInfo.Length > 2040109465.6)
            {
                errors.Append("File exceeds the telegram limit of 2 GB ").AppendLine(fileInfo.Name);

                return;
            }

            if (convert && extensions.Contains(fileInfo.Extension, System.StringComparison.OrdinalIgnoreCase))
            {
                await ConvertToMp4Upload(fileInfo, txtFFPath.Text!, chatBase);
            }
            else
            {
                await UploadAndSendFile(fileInfo.FullName, fileInfo.Name, chatBase, false);
            }
        }

        private async Task ConvertToMp4Upload (FileInfo file, string path, ChatBase chat)
        {
            var converter = new Converter(Model!);
            var mp4Path = file.Name + ".mp4";
            statusText.Text = $"Converting {file.Extension} file to mp4";
            await converter.StartConverting(file.FullName, mp4Path, path, CancellationToken.None);

            await UploadAndSendFile(mp4Path, file.Name, chat, false);

            File.Delete(mp4Path);
        }

        private async Task UploadAndSendFile (string filePath, string fileName, ChatBase channel, bool isSync = false)
        {
            Model!.Progress = 0;
            UpdateStatus($"Uploading file {filePath}", isSync);
            var file = await _client!.UploadFileAsync(filePath, (p, r) => Model!.Progress = p * 100 / r);

            UpdateStatus($"Sending file message to {channel.Title}", isSync);
            await _client.SendMediaAsync(channel, fileName, file);
        }

        private void UpdateStatus (string message, bool isSync)
        {
            if (isSync)
            {
                //syncStatus.Text = message;
                Model!.SyncOutput = message;
            }
            else
            {
                statusText.Text = message;
            }
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

                var channels = allChats.chats.Where(x => x.Value.IsActive).Select(x => new ViewModels.Channel
                {
                    Chat = x.Value,
                    Id = x.Key,
                    Title = x.Value.Title
                }).OrderBy(x => x.Title).ToList();

                if (channels.Count != 0)
                {
                    channelsList.ItemsSource = new List<ViewModels.Channel>();
                    channelsList.ItemsSource = channels;
                    downloadChannels.ItemsSource = new List<ViewModels.Channel>();
                    downloadChannels.ItemsSource = channels;
                    syncChannels.ItemsSource = new List<ViewModels.Channel>();
                    syncChannels.ItemsSource = channels;
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

        private IStorageFolder? DownFolder { get; set; }

        private async void Select_Upload_Folder_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
                DownFolder = folder;
                downFolderName.Content = folder.Name;
            }
        }

        private async void Download_Telegram_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DownFolder != null && downloadChannels.SelectedItem is ViewModels.Channel channel)
            {
                var messages = await _client.Messages_GetHistory(channel.Chat);

                var files = messages.Messages.OfType<TL.Message>().Select(m => m.media).ToList();

                foreach (var file in files)
                {
                    if (file is MessageMediaDocument { document: Document document })
                    {
                        string filename = document.Filename; // use document original filename, or build a name from document ID & MIME type:
                        filename ??= $"{document.id}.{document.mime_type[(document.mime_type.IndexOf('/') + 1)..]}";

                        downloadStatus.Text = $"Downloading file: {filename}";
                        _ = await DownloadFile(DownFolder, document, filename);
                    }
                    else if (file is MessageMediaPhoto { photo: Photo photo })
                    {
                        var filename = $"{photo.id}.jpg";
                        downloadStatus.Text = $"Downloading file: {filename}";
                        var (type, photoFile) = await DownloadFile(DownFolder, photo, filename);
                        if (type is not Storage_FileType.unknown and not Storage_FileType.partial)
                        {
                            var newPath = Path.Combine(DownFolder.Path.LocalPath, $"{photo.id}.{type}");
                            File.Move(photoFile!.Path.LocalPath, newPath, true);
                        }
                    }
                }
            }
        }

        private async Task<(Storage_FileType, IStorageFile?)> DownloadFile<T> (IStorageFolder folder, T document, string filename)
        {
            var storageFile = await folder.CreateFileAsync(filename);
            await using var stream = await storageFile!.OpenWriteAsync();
            //await using var fileStream = File.Create(filename);
            if (document is Document doc)
            {
                await _client!.DownloadFileAsync(doc, stream, null, (p, r) => Model!.DownProgress = p * 100 / r);

                return (Storage_FileType.mp4, storageFile);
            }
            else if (document is Photo photo)
            {
                var result = await _client!.DownloadFileAsync(photo, stream, null, (p, r) => Model!.DownProgress = p * 100 / r);

                return (result, storageFile);
            }

            return (Storage_FileType.unknown, storageFile);
        }

        private void Sync_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
        }

        private async void Sync_Setup_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (txtSyncFolder.Text.IsNull())
            {
                txtSyncFolder.Focus();
                return;
            }

            var window = this.VisualRoot as Window;

            var dir = await window!.StorageProvider.TryGetFolderFromPathAsync(txtSyncFolder.Text!);

            if (dir != null)
            {
                var allFiles = Directory.GetFiles(dir.Path.LocalPath);
                SyncDetail syncDetail = new SyncDetail
                {
                    FolderPath = dir.Path.LocalPath,
                    LastSync = DateTime.Now,
                    FileCount = allFiles.Length
                };
                Model!.SyncDetail = syncDetail;
                File.WriteAllText("sync.json", JsonSerializer.Serialize(syncDetail));

                watcher = new(dir.Path.LocalPath)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*" // Monitor all files
                };

                watcher.Created += Watcher_Created;
                //watcher.Changed += Watcher_Changed;
                watcher.Renamed += Watcher_Renamed;
            }
        }

        private async void Watcher_Renamed (object sender, RenamedEventArgs e)
        {
            await UploadSingleFile(e);
        }

        private async void Watcher_Changed (object sender, FileSystemEventArgs e)
        {
            await UploadSingleFile(e);
        }

        private async void Watcher_Created (object sender, FileSystemEventArgs e)
        {
            await UploadSingleFile(e);
        }

        private async Task UploadSingleFile (FileSystemEventArgs e)
        {
            if (syncChannels.SelectedItem is ViewModels.Channel channel)
            {
                await this.UploadAndSendFile(e.FullPath, e.Name ?? string.Empty, channel.Chat!, true);
                Model!.SyncDetail!.FileCount++;
                Model!.SyncDetail!.LastSync = DateTime.Now;
                Model!.Progress = 0;
                Model!.SyncOutput = string.Empty;
            }
        }
    }
}