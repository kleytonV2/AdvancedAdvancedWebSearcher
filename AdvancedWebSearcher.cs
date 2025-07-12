using Microsoft.Playwright;
using System.Text.RegularExpressions;
using System.Web;

namespace AdvancedWebSearcher;

class AdvancedWebSearcher
{
    //private static readonly HttpClient Client = new();
    //private static string _keyword = string.Empty;

    private static async Task<List<string>> GetResults(string query, string keyword, string wordToFind, int maxResults)
    {
        var searchResults = new List<string>();

        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var resultsFilePath = Path.Combine(exePath, "AdvancedWebSearcherResults.txt");
        if (File.Exists(resultsFilePath))
        {
            try
            {
                File.Delete(resultsFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        await using var fileWriter = new StreamWriter(resultsFilePath, append: true);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false // Set to false to see the browser
        });

        var pageCount = 1;
        var page = await browser.NewPageAsync();
        var alreadyCheckedUrls = new List<string>();
        for (var start = 1; searchResults.Count < maxResults; start += 10)
        {
            var url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&first={start}";
            await page.GotoAsync(url);

            //await page.WaitForSelectorAsync("//li[contains(@class, 'b_algo')]//h2/a");

            var links = await page.QuerySelectorAllAsync("li.b_algo h2 a");
            if (links.Count == 0)
            {
                Console.WriteLine("\nAll the results have being checked!");
                break;
            }

            var hrefsToBeChecked = new List<string>();
            foreach (var link in links)
            {
                var href = await link.GetAttributeAsync("href");
                if (string.IsNullOrWhiteSpace(href)) continue;

                if (href.StartsWith("https://www.bing.com"))
                {
                    // Extract the actual URL from 'u=' query param
                    var match = Regex.Match(href, @"[?&;]u=([^&]+)");
                    if (!match.Success)
                        continue;

                    var encodedUrl = match.Groups[1].Value;
                    href = CleanAndDecodeBingUrl(encodedUrl);
                }

                var mainDomainMatch = Regex.Match(href, @"^(https?://[^/]+)");
                if (mainDomainMatch.Success)
                    href = mainDomainMatch.Value;

                if (alreadyCheckedUrls.Contains(href) || hrefsToBeChecked.Contains(href) || !href.StartsWith("http") || !UrlContainsKeyword(href, keyword))
                    continue;

                hrefsToBeChecked.Add(href);
                alreadyCheckedUrls.Add(href);
            }

            foreach (var href in hrefsToBeChecked)
            {
                try
                {
                    Console.Write($"\nPage {pageCount} - Link Number {(alreadyCheckedUrls.IndexOf(href) + 1)}. Visiting: {href}");
                    await page.GotoAsync(href);
                    var content = await page.ContentAsync();

                    if (content.Contains(wordToFind, StringComparison.OrdinalIgnoreCase))
                    {
                        if(searchResults.Contains(href))
                            continue;

                        Console.Write(" - [OK] match found!\n");
                        searchResults.Add(href);
                        await fileWriter.WriteLineAsync(href);
                    }

                    if (searchResults.Count >= maxResults) 
                        break;
                }
                catch (Exception ex)
                {
                    Console.Write($"- [ERROR] Error on {href}: {ex.Message}\n");
                }

                await Task.Delay(1000); // Be polite
            }

            pageCount++;

        }

        return searchResults;
    }

    //public static async Task<string> GetHtmlAsyncResponse(string url)
    //{
    //    // You must provide a dummy payload; empty content will return 405 from many servers
    //    var content = new FormUrlEncodedContent(new Dictionary<string, string>());

    //    try
    //    {
    //        var response = await Client.GetAsync(url);

    //        if (response.IsSuccessStatusCode)
    //        {
    //            string html = await response.Content.ReadAsStringAsync();
    //            return html;
    //        }

    //        Console.WriteLine($"Failed to POST to {url}. Status: {response.StatusCode}");
    //        return null;
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Exception while posting to {url}: {ex.Message}");
    //        return null;
    //    }
    //}

    //static async Task<List<string>> GetSearchResultsAsync(string query, int maxLinks)
    //{
    //    var searchResults = new List<string>();

    //    //var handler = new HttpClientHandler
    //    //{
    //    //    UseCookies = true,
    //    //    CookieContainer = new CookieContainer()
    //    //};

    //    //handler.CookieContainer.Add(new Uri("https://www.bing.com"), new Cookie("SRCHD", "AF=NOFORM"));

    //    //var client = new HttpClient(); //handler

    //    int first = 1;

    //    while (searchResults.Count < maxLinks && first < 100)
    //    {
    //        //client.DefaultRequestHeaders.Clear();

    //        var searchUrl = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&first={first}";
    //        var html = await GetHtmlAsyncResponse(searchUrl); //await client.GetStringAsync(searchUrl);

    //        if(string.IsNullOrEmpty(html))
    //            continue;

    //        var htmlDoc = new HtmlDocument();
    //        htmlDoc.LoadHtml(html);

    //        var nodes = htmlDoc.DocumentNode.SelectNodes("//li[contains(@class, 'b_algo')]//h2/a");
    //        if (nodes == null || nodes.Count == 0)
    //        {
    //            Console.WriteLine("No results found or structure changed. Dumping response:");
    //            Console.WriteLine(html.Substring(0, 1000)); // Debug
    //            break;
    //        }

    //        foreach (var node in nodes)
    //        {
    //            var href = node.GetAttributeValue("href", string.Empty);
    //            if (string.IsNullOrWhiteSpace(href)) continue;

    //            if (href.StartsWith("https://www.bing.com"))
    //            {
    //                // Extract the actual URL from 'u=' query param
    //                var match = Regex.Match(href, @"[?&;]u=([^&]+)");
    //                if (!match.Success)
    //                    continue;

    //                var encodedUrl = match.Groups[1].Value;
    //                href = CleanAndDecodeBingUrl(encodedUrl);
    //            }

    //            if (searchResults.Contains(href) || !href.StartsWith("http") || !UrlContainsKeyword(href)) 
    //                continue;

    //            searchResults.Add(href);
    //            if (searchResults.Count >= maxLinks)
    //                break;
    //        }

    //        first += 10;
    //        await Task.Delay(1000); // Be polite
    //    }

    //    return searchResults;
    //}

    static string CleanAndDecodeBingUrl(string encoded)
    {
        // Remove optional 'a1' prefix (2 characters) if present
        if (encoded.StartsWith("a1"))
            encoded = encoded.Substring(2);

        // Ensure it's trimmed and clean
        encoded = encoded.Trim();

        // Fix missing padding if needed
        int padding = encoded.Length % 4;
        if (padding > 0)
            encoded = encoded.PadRight(encoded.Length + (4 - padding), '=');

        try
        {
            var base64Bytes = Convert.FromBase64String(encoded);
            return System.Text.Encoding.UTF8.GetString(base64Bytes);
        }
        catch
        {
            // If base64 decoding fails, fallback to raw URL decoding
            return HttpUtility.UrlDecode(encoded);
        }
    }

    private static bool UrlContainsKeyword(string? url, string keyword)
    {
        return keyword.Equals(String.Empty) || (url is not null && url.ToLower().Contains(keyword.ToLower()));
    }

    //static async Task<List<string>> FindPagesWithWordAsync(List<string> urls, string word)
    //{
    //    var matchingPages = new List<string>();
    //    var client = new HttpClient();
    //    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

    //    foreach (var url in urls)
    //    {
    //        try
    //        {
    //            Console.WriteLine("Checking: " + url);
    //            var content = await client.GetStringAsync(url);
    //            if (content.ToLower().Contains(word.ToLower()))
    //            {
    //                matchingPages.Add(url);
    //                Console.WriteLine("✔ Match found!");
    //            }
    //            await Task.Delay(1000); // Be polite
    //        }
    //        catch
    //        {
    //            Console.WriteLine("✖ Failed to load: " + url);
    //        }
    //    }
    //    return matchingPages;
    //}

    private static async Task Main(string[] args)
    {
        // Headers mimic a browser to avoid being blocked
        //Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        Console.Write("Search query: ");
        var query = Console.ReadLine() ?? string.Empty;

        Console.Write("Keyword to match in URLs: ");
        var keyword = Console.ReadLine() ?? string.Empty;

        Console.Write("Word to find in page HTML: ");
        var wordToFind = Console.ReadLine() ?? string.Empty;

        Console.Write("Maximum number of results: ");
        var maxLinks = int.Parse(Console.ReadLine() ?? string.Empty);

        var matches = await GetResults(query, keyword, wordToFind, maxLinks);
        //var matches = await FindPagesWithWordAsync(linksToCheck, wordToFind);

        if (matches.Count > 0)
        {
            Console.WriteLine("\nMatching pages:");
            foreach (var match in matches)
            {
                Console.WriteLine(match);
            }

            //try
            //{
            //    var exePath = AppDomain.CurrentDomain.BaseDirectory;
            //    var resultsFilePath = Path.Combine(exePath, "AdvancedWebSearcherResults.txt");
            //    await File.WriteAllLinesAsync(resultsFilePath, matches);
            //    Console.WriteLine($"\nThe results file was created on this location: {resultsFilePath}");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"\nIt was not possible to save the results into a file: {ex.Message}");
            //}
        }

        Console.WriteLine("\nDone!");

        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine(); // Waits for user input


    }
}