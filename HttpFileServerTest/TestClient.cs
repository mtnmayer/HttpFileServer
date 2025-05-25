using System.Text;

namespace HttpFileServer.Tests
{
    public class TestClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public TestClient(string baseUrl = "http://localhost:5000")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();

            // Increase timeout for debugging (5 minutes)
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task RunAllTestsAsync()
        {
            Console.WriteLine("Starting HTTP File Server Tests...");
            Console.WriteLine("Make sure the server is running on the specified port");

            // Wait for server to be ready
            await Task.Delay(2000);

            await RunSingleTestAsync("Health Check", TestHealthCheckAsync);
            await RunSingleTestAsync("Store Blob", TestStoreBlobAsync);
            await RunSingleTestAsync("Retrieve Blob", TestRetrieveBlobAsync);
            await RunSingleTestAsync("Invalid ID", TestInvalidIdAsync);
            await RunSingleTestAsync("Missing Content-Length", TestMissingContentLengthAsync);
            await RunSingleTestAsync("Large Headers", TestLargeHeadersAsync);
            await RunSingleTestAsync("Exceed Size Limit", TestExceedSizeLimitAsync);
            await RunSingleTestAsync("Delete Blob", TestDeleteBlobAsync);
            await RunSingleTestAsync("Retrieve Deleted Blob", TestRetrieveDeletedBlobAsync);

            Console.WriteLine("\n=== Tests Complete ===");
        }

        private async Task RunSingleTestAsync(string testName, Func<Task> testMethod)
        {
            Console.WriteLine($"Running: {testName}");
            Console.WriteLine("Press any key to continue, or 'q' to quit...");

            var key = Console.ReadKey(true);
            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
            {
                Console.WriteLine("Tests cancelled by user.");
                return;
            }

            try
            {
                await testMethod();
                Console.WriteLine($"{testName} completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{testName} failed: {ex.Message}");
            }
        }

        private async Task TestHealthCheckAsync()
        {
            Console.WriteLine("\n=== Health Check Test ===");
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Health check failed: {ex.Message}");
            }
        }

        private async Task TestStoreBlobAsync()
        {
            Console.WriteLine("\n=== Store Blob Test ===");
            try
            {
                var testData = "Hello, World! This is test blob data from .NET Core server.";
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(testData));

                content.Headers.Add("Content-Type", "text/plain");
                content.Headers.Add("X-Rebase-Custom", "test-value-dotnet");
                content.Headers.Add("X-Rebase-Another", "another-value");
                content.Headers.Add("Content-Length", testData.Length.ToString());

                var response = await _httpClient.PostAsync($"{_baseUrl}/blobs/test-blob-1", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Store blob failed: {ex.Message}");
            }
        }

        private async Task TestRetrieveBlobAsync()
        {
            Console.WriteLine("\n=== Retrieve Blob Test ===");
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/blobs/test-blob-1");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine("Headers:");
                foreach (var header in response.Headers)
                {
                    Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                foreach (var header in response.Content.Headers)
                {
                    Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                Console.WriteLine($"Data: {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Retrieve blob failed: {ex.Message}");
            }
        }

        private async Task TestInvalidIdAsync()
        {
            Console.WriteLine("\n=== Invalid ID Test ===");
            try
            {
                var testData = "Test data";
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(testData));
                content.Headers.Add("Content-Length", testData.Length.ToString());

                var response = await _httpClient.PostAsync($"{_baseUrl}/blobs/invalid@id!", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid ID test failed: {ex.Message}");
            }
        }

        private async Task TestMissingContentLengthAsync()
        {
            Console.WriteLine("\n=== Missing Content-Length Test ===");
            try
            {
                var testData = "Test data";
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(testData));
                content.Headers.Add("Content-Type", "text/plain");
                // Intentionally not adding Content-Length

                var response = await _httpClient.PostAsync($"{_baseUrl}/blobs/test-no-length", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Missing Content-Length test failed: {ex.Message}");
            }
        }

        private async Task TestLargeHeadersAsync()
        {
            Console.WriteLine("\n=== Large Headers Test ===");
            try
            {
                var testData = "Test data";
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(testData));

                // Create a header value that exceeds MAX_HEADER_LENGTH (100)
                var largeHeaderValue = new string('x', 120);

                content.Headers.Add("Content-Type", "text/plain");
                content.Headers.Add("X-Rebase-Large", largeHeaderValue);
                content.Headers.Add("Content-Length", testData.Length.ToString());

                var response = await _httpClient.PostAsync($"{_baseUrl}/blobs/large-header-test", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Large headers test failed: {ex.Message}");
            }
        }

        private async Task TestExceedSizeLimitAsync()
        {
            Console.WriteLine("\n=== Exceed Size Limit Test ===");
            try
            {
                // Create data that exceeds MAX_LENGTH (10MB)
                var largeData = new byte[11 * 1024 * 1024]; // 11MB
                var content = new ByteArrayContent(largeData);

                content.Headers.Add("Content-Type", "application/octet-stream");
                content.Headers.Add("Content-Length", largeData.Length.ToString());

                var response = await _httpClient.PostAsync($"{_baseUrl}/blobs/large-blob-test", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exceed size limit test failed: {ex.Message}");
            }
        }

        private async Task TestDeleteBlobAsync()
        {
            Console.WriteLine("\n=== Delete Blob Test ===");
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/blobs/test-blob-1");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete blob failed: {ex.Message}");
            }
        }

        private async Task TestRetrieveDeletedBlobAsync()
        {
            Console.WriteLine("\n=== Retrieve Deleted Blob Test ===");
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/blobs/test-blob-1");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Retrieve deleted blob failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}