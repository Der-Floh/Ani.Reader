using Ani.Reader;

using ExampleAniReader;

internal class Program
{
    private static async Task Main(string[] args)
    {
        //Console.WriteLine("Hello, World!");
        //var aniFile = @"C:\Users\Gabri\Downloads\Cursors\Oxygen-Blue\watch.ani";
        //var aniSaveDir = @"C:\Users\Gabri\Downloads\AniTest\watchAni";
        //var aniWebP = @"C:\Users\Gabri\Downloads\AniTest\watchAni\watch.webp";
        //var aniReader = new AniReader();
        //var aniDatas = aniReader.Read(aniFile);
        //await aniDatas.SaveImages(aniSaveDir);
        //await aniDatas.SaveAsWebP(aniWebP);
        //Console.WriteLine(aniDatas);

        //Console.WriteLine("Hello, World!");
        //var aniFile = @"C:\Windows\System32\user32.dll";
        //var aniSaveDir = @"C:\Users\Gabri\Downloads\AniTest";
        //var aniReader = new AniReader();
        //var aniDatas = aniReader.Read(aniFile);

        //foreach (var ani in aniDatas)
        //{
        //    await ani.SaveImages(aniSaveDir);
        //    await ani.SaveAsWebP(aniSaveDir);
        //}

        //Console.WriteLine(aniDatas);

        Console.WriteLine("Hello, World!");
        var rootDir = @"C:\Users\Gabri\Downloads\AniTest\downloads";
        var allAniFiles = Directory.GetFiles(rootDir, "*.ani", SearchOption.AllDirectories);
        var aniSaveDir = @"C:\Users\Gabri\Downloads\AniTest\downloads\export";
        var errorFile = Path.Combine(aniSaveDir, "errors.txt");
        var errorFileStream = new FileStream(errorFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 0, false);
        await errorFileStream.AppendLineToFileAsync("######### START NEW RUN #########");
        var aniReader = new AniReader();

        int totalFiles = allAniFiles.Length;
        int processedFiles = 0;
        object progressLock = new object();

        // Liste von Tasks für parallele Verarbeitung
        //List<Task> tasks = new List<Task>();

        //foreach (var file in allAniFiles)
        //{
        //    tasks.Add(Task.Run(async () =>
        //    {
        //        try
        //        {
        //            var aniDatas = aniReader.Read(file);

        //            foreach (var ani in aniDatas)
        //            {
        //                foreach (var animation in ani.Animations)
        //                {
        //                    await ani.SaveImages(aniSaveDir, animation);
        //                    await ani.SaveAsWebP(aniSaveDir, animation);
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            await errorFileStream.AppendLineToFileAsync($"Error reading {file}: {ex.Message}");
        //            Console.WriteLine($"Error reading {file}: {ex.Message}");
        //        }
        //        finally
        //        {
        //            lock (progressLock)
        //            {
        //                processedFiles++;
        //                double progress = (double)processedFiles / totalFiles * 100;
        //                Console.WriteLine($"Progress: {progress:F2}% ({processedFiles}/{totalFiles})");
        //            }
        //        }
        //    }));
        //}

        //// Warte auf alle Tasks
        //await Task.WhenAll(tasks);

        //var user32 = @"C:\Windows\System32\user32.dll";
        //var icoReader = new IcoReader();
        //var icoData = icoReader.Read(user32);
        //await icoData.SaveAllGroupsToDirectory(@"C:\Users\Gabri\Downloads\AniTest\IcoTest");
        //await icoData.SaveAllImagesToDirectory(@"C:\Users\Gabri\Downloads\AniTest\IcoTest");

        // Is CUR
        // @"C:\Users\Gabri\Downloads\AniTest\downloads\vanossgaming.ani"
        // @"C:\Users\Gabri\Downloads\AniTest\downloads\aero_busy_6.ani"
        //  @"C:\Users\Gabri\Downloads\AniTest\downloads\Transparent_Rainbow_Working_In_Background.ani"

        // Missing Image in IcoData
        // @"C:\Users\Gabri\Downloads\AniTest\downloads\so cool.ani"

        var notOkeyList = new string[]{
   //@"C:\Users\Gabri\Downloads\AniTest\downloads\so cool.ani",
   @"C:\Users\Gabri\Downloads\AniTest\downloads\Rainbow Advanced - No.ani"
};

        for (int i = 0; i < allAniFiles.Length; i++)
        {
            var file = allAniFiles[i];

            //if (!notOkeyList.Contains(file))
            //{
            //    continue;
            //}

            var progress = (double)i / allAniFiles.Length * 100;
            Console.WriteLine($"Progress: {progress}% ({i}/{allAniFiles.Length})");
            try
            {
                var aniDatas = aniReader.Read(file);

                foreach (var ani in aniDatas)
                {
                    foreach (var animation in ani.Animations)
                    {
                        await ani.SaveImages(aniSaveDir, animation);
                        //await ani.SaveAsWebP(aniSaveDir, animation);
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {file}: {ex.Message}");
            }
        }

        Console.WriteLine("Finished");
    }
}
