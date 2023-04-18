using PuppeteerSharp;

namespace buff_scraper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (!File.Exists("list.txt"))
            {
                File.Create("list.txt");
                ColorWrite("File .\\list.txt created. Insert buff.163.com item links (one per line).", ConsoleColor.Yellow);
                Console.ReadKey();
                Environment.Exit(0);
            }
            string[] lines = File.ReadAllLines("list.txt");
            FetchBrowser().GetAwaiter().GetResult();
            ColorWrite($"{lines.Length} links loaded.\n", ConsoleColor.Green);
            while (true)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    ColorWrite($"{i+1}: ", ConsoleColor.DarkBlue);
                    GetSteamPrices(lines[i]).GetAwaiter().GetResult();
                }
                ColorWrite("\nList scanned. Waiting for 3 minutes.\n", ConsoleColor.Yellow);
                Thread.Sleep(180000);
            }
        }
        public static async Task FetchBrowser()
        {
            ColorWrite("Downloading browser...", ConsoleColor.Yellow);
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            ColorWrite("Done. \n", ConsoleColor.Green);
        }
        public static async Task GetSteamPrices(string activeLink)
        {
            string name;
            double[] price = new double[2];
            using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            { Headless = true }))
            {
                using (var page = await browser.NewPageAsync())
                {
                    await page.SetRequestInterceptionAsync(true);
                    page.Request += (sender, e) =>
                    {
                        if (e.Request.ResourceType == ResourceType.Image || e.Request.ResourceType == ResourceType.StyleSheet)
                            e.Request.AbortAsync();
                        else
                            e.Request.ContinueAsync();
                    };
                    page.DefaultTimeout = 30000;
                    await Console.Out.WriteAsync(".");
                    try
                    {
                        await page.GoToAsync(activeLink);
                        await Console.Out.WriteAsync(".");
                        await page.WaitForNetworkIdleAsync();
                        //await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        ColorWrite(ex.Message + '\n', ConsoleColor.Red);
                        await browser.CloseAsync();
                    }
                    try
                    {
                        const string lowestPriceEval = @"Array.from(document.querySelectorAll('.f_Strong')).map(price => price.textContent);";
                        const string nameEval = @"Array.from(document.querySelectorAll('.detail-cont h1')).map(name => name.textContent);";
                        string[] lowestPrices = await page.EvaluateExpressionAsync<string[]>(lowestPriceEval);
                        if (lowestPrices.Length == 0)
                        {
                            price[0] = 0;
                            price[1] = 0;
                        }
                        else
                        {
                            price[0] = CleanUpPrice(lowestPrices[1]);
                            price[1] = CleanUpPrice(lowestPrices[2]);
                        }
                        name = (await page.EvaluateExpressionAsync<string[]>(nameEval))[0];
                        await Console.Out.WriteAsync(". ");
                        if (price[0] / price[1] < 0.95)
                        {
                            ColorWrite("\n[======= Item found =======]\n", ConsoleColor.Green);
                            ColorWrite($"Name: {name}\n", ConsoleColor.Yellow);
                            Console.WriteLine($"Quotient: {price[0] / price[1]}");
                            Console.WriteLine($"Link: {activeLink}");
                        }
                    }
                    catch
                    {

                        ColorWrite(" Invalid prices.\n", ConsoleColor.Red);
                        await browser.CloseAsync();
                    }
                }
            }
        }
        public static double CleanUpPrice(string price)
        {
            try
            {
                return double.Parse(price.Substring(2).Replace('.', ','));
            }
            catch 
            {
                ColorWrite(" Invalid prices.\n", ConsoleColor.Red);
                return 0;
            }
        }
        public static void ColorWrite(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(msg);
            Console.ResetColor();
        }
    }
}