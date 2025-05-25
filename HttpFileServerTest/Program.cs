namespace HttpFileServer.Tests
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5033";

            var testClient = new TestClient(baseUrl);
            try
            {
                await testClient.RunAllTestsAsync();
            }
            finally
            {
                testClient.Dispose();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}