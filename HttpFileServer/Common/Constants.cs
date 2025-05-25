namespace HttpFileServer.Common
{
    public static class Constants
    {
        public const long MAX_LENGTH = 10 * 1024 * 1024; // 10MB
        public const long MAX_DISK_QUOTA = 1024L * 1024 * 1024; // 1GB
        public const int MAX_HEADER_LENGTH = 100;
        public const int MAX_HEADER_COUNT = 20;
        public const int MAX_ID_LENGTH = 200;
        public const int MAX_BLOBS_IN_FOLDER = 10000;
    }
}