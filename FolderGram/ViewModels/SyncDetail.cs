using System;

namespace FolderGram.ViewModels
{
    public class SyncDetail
    {
        public string? FolderPath { get; set; }

        public DateTime? LastSync { get; set; }

        public int FileCount { get; set; }
    }
}
