namespace HttpFileServer.Model
{
    public class BlobMetadata
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
    }
}
