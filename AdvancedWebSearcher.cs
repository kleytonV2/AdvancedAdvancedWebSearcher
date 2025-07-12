using Microsoft.Playwright;
using System.Text.RegularExpressions;
using System.Web;

namespace AdvancedWebSearcher;

class AdvancedWebSearcher
{

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
			Headless = false // Set to true to hide the browser (the app might be blocked)
		});

        var pageCount = 1;
        var page = await browser.NewPageAsync();
        var alreadyCheckedUrls = new List<string>();
        for (var start = 1; searchResults.Count < maxResults; start += 10)
        {
            var url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&first={start}";
            await page.GotoAsync(url);

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
                if (string.IsNullOrWhiteSpace(href)) 
                    continue;

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

				// Wait a bit to avoid overwhelming the server
				await Task.Delay(1000);
            }

            pageCount++;

        }

        return searchResults;
    }

    static string CleanAndDecodeBingUrl(string encoded)
    {
        // Remove optional 'a1' prefix (2 characters) if present
        if (encoded.StartsWith("a1"))
            encoded = encoded.Substring(2);

        // Fix missing padding if needed
        int padding = encoded.Trim().Length % 4;
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

    private static async Task Main(string[] args)
    {
        Console.Write("Search query: ");
        var query = Console.ReadLine() ?? string.Empty;

        Console.Write("Keyword to match in URLs: ");
        var keyword = Console.ReadLine() ?? string.Empty;

        Console.Write("Word to find in page HTML: ");
        var wordToFind = Console.ReadLine() ?? string.Empty;

        Console.Write("Maximum number of results: ");
        var maxLinks = int.Parse(Console.ReadLine() ?? string.Empty);

        var matches = await GetResults(query, keyword, wordToFind, maxLinks);
        if (matches.Count > 0)
        {
            Console.WriteLine("\nMatching pages:");

            foreach (var match in matches)
                Console.WriteLine(match);

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
        Console.ReadLine();


    }
}