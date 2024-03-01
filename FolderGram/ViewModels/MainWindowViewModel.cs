using ReactiveUI;

namespace FolderGram.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
#pragma warning disable CA1822 // Mark members as static
        public string Greeting => "Welcome to Avalonia!";
#pragma warning restore CA1822 // Mark members as static

        private string _output = string.Empty;
        public string Output
        {
            get => _output;
            set => this.RaiseAndSetIfChanged(ref _output, value);
        }

        private double _progress = 0;
        public double Progress
        {
            get => _progress;
            set => this.RaiseAndSetIfChanged(ref _progress, value);
        }

        private double _downProgress = 0;
        public double DownProgress
        {
            get => _downProgress;
            set => this.RaiseAndSetIfChanged(ref _downProgress, value);
        }

        private string[] _downFiles = [];
        public string[] DownFiles
        {
            get => _downFiles;
            set => this.RaiseAndSetIfChanged(ref _downFiles, value);
        }
    }
}
