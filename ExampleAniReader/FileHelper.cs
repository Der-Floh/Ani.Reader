using System.Text;

namespace ExampleAniReader;

public static class FileHelper
{
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public static async Task AppendLineToFileAsync(this FileStream fileStream, string line)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        await _semaphore.WaitAsync(); // Wait for access

        try
        {
            using var writer = new StreamWriter(fileStream, Encoding.UTF8, 4096, leaveOpen: true);
            await writer.WriteLineAsync(line);
            await writer.FlushAsync(); // Ensure that the data is written immediately
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to file: {ex.Message}");
        }
        finally
        {
            _semaphore.Release(); // Restore access
        }
    }
}
