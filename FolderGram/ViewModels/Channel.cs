namespace FolderGram.ViewModels
{
    public class Channel
    {
        public long Id { get; set; }

        public TL.ChatBase? Chat { get; set; }

        public string Title { get; set; } = string.Empty;
    }
}
