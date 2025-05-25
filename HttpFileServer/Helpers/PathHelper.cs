using System.Security.Cryptography;
using System.Text;

namespace HttpFileServer.Helpers
{
    public static class PathHelper
    {
        public static (string Directory, string FilePath) GetBlobPath(string dataDir, string id)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(id));
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();

            var hashSuffix = hashString[^5..];             
            var subDir1 = hashSuffix[..2]; 
            var subDir2 = hashSuffix[2..4];  
            var subDir3 = hashSuffix[4..5];

            var directory = Path.Combine(dataDir, subDir1, subDir2, subDir3);
            var filePath = Path.Combine(directory, $"{id}.blob");

            return (directory, filePath);
        }
    }
}