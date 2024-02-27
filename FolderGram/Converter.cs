using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FFmpeg.NET;
using FFmpeg.NET.Events;

using FolderGram.ViewModels;

using InputFile = FFmpeg.NET.InputFile;

namespace FolderGram
{
    public class Converter (MainWindowViewModel viewModel)
    {
        private double TotalDuration { get; set; }
        public async Task StartConverting (string input, string output, string enginePath, CancellationToken cancellationToken)
        {
            var inputFile = new InputFile(input);
            var outputFile = new OutputFile(output);

            var ffmpeg = new Engine(enginePath + "\\ffmpeg.exe");

            var metadata = await ffmpeg.GetMetaDataAsync(inputFile, cancellationToken);

            TotalDuration = metadata.Duration.TotalMilliseconds;

            ffmpeg.Progress += OnProgress!;
            ffmpeg.Data += OnData!;
            ffmpeg.Error += OnError!;
            ffmpeg.Complete += OnComplete!;
            await ffmpeg.ConvertAsync(inputFile, outputFile, cancellationToken);
        }

        private void OnProgress (object sender, ConversionProgressEventArgs e)
        {
            StringBuilder builder = new();
            builder.AppendFormat("[{0} => {1}]", e.Input.MetaData.FileInfo.Name, e.Output.Name).AppendLine();
            builder.AppendFormat("Bitrate: {0}", e.Bitrate).AppendLine();
            builder.AppendFormat("Fps: {0}", e.Fps).AppendLine();
            builder.AppendFormat("Frame: {0}", e.Frame).AppendLine();
            builder.AppendFormat("ProcessedDuration: {0}", e.ProcessedDuration).AppendLine();
            builder.AppendFormat("Size: {0} kb", e.SizeKb).AppendLine();
            builder.AppendFormat("TotalDuration: {0}\n", e.TotalDuration).AppendLine();
            viewModel.Output = builder.ToString();

            if (TotalDuration > 0)
            {
                viewModel.Progress = 100 * e.ProcessedDuration.TotalMilliseconds / TotalDuration;
            }
        }

        private void OnData (object sender, ConversionDataEventArgs e)
        {
            viewModel.Output = string.Format("[{0} => {1}]: {2}", e.Input.Name, e.Output.Name, e.Data);
        }

        private void OnComplete (object sender, ConversionCompleteEventArgs e)
        {
            viewModel.Output = string.Format("Completed conversion from {0} to {1}", e.Input.MetaData.FileInfo.FullName, e.Output.Name);
        }

        private void OnError (object sender, ConversionErrorEventArgs e)
        {
            viewModel.Output = string.Format("[{0} => {1}]: Error: {2}\n{3}", e.Input.MetaData.FileInfo.Name, e.Output.Name, e.Exception.ExitCode, e.Exception.InnerException);
        }
    }
}
