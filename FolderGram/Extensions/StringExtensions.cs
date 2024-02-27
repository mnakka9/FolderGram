namespace FolderGram.Extensions
{
    public static class StringExtensions
    {
        public static bool IsNull (this string? value)
        {
            return value switch
            {
                null => true,
                "" => true,
                " " => true,
                _ => false
            };
        }
    }
}
