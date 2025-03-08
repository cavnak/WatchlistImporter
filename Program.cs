// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Text.Json.Nodes;

using System.Web;


class Program

 
{
    private static HttpClient _client = new HttpClient();


    public class PlexMovie
    {
        public PlexMovie(decimal score, string resultKey, string title, string originalSearchTerm)
        {
            Score = score;
            ResultKey = resultKey;
            Title = title;
            OriginalSearchTerm = originalSearchTerm;
        }

        public decimal Score { get; set; }
        public string ResultKey { get; set; }

        public string Title { get; set; }
        public string OriginalSearchTerm { get; set; }
    }

    public static async Task<bool> AddToPlexWatchlist(string resultKey, string plexToken)
    {
        var plexMsg = new HttpRequestMessage(HttpMethod.Put, new Uri("https://discover.provider.plex.tv/actions/addToWatchlist?ratingKey=" + resultKey));
        {
            plexMsg.Headers.TryAddWithoutValidation("X-Plex-Token", plexToken);
            plexMsg.Headers.TryAddWithoutValidation("X-Plex-Client-Identifier", "3l4vjkg9yu7m16oh18id309y");
            using (var resp = await _client.SendAsync(plexMsg))
            {
                var test = await resp.Content.ReadAsStringAsync();
                resp.EnsureSuccessStatusCode();
            }
        }
        return true;
    }

    public static async Task<PlexMovie> GetBestMatchForMovie(string searchString)
    {
        var plexSearchRequestBaseUrl = @"https://discover.provider.plex.tv/library/search?searchTypes=movies&searchProviders=discover,plexAVOD,plexFAST&includeMetadata=1&filterPeople=1&limit=10&query=";
        var plexMsg = new HttpRequestMessage(HttpMethod.Get, new Uri(plexSearchRequestBaseUrl + searchString));
        plexMsg.Headers.TryAddWithoutValidation("Accept", "application/json");
        plexMsg.Headers.TryAddWithoutValidation("content-type", "application/json;charset=utf-8");
        using (var resp = await _client.SendAsync(plexMsg))
        {
            var individualSearchResults = new List<PlexMovie>();
            resp.EnsureSuccessStatusCode();
            var html = await resp.Content.ReadAsStringAsync(); //this is a bonkers data structure
            var parsedJson = JsonNode.Parse(html);
            var results = parsedJson["MediaContainer"]["SearchResults"].AsArray();
            foreach (var result in results)
            {
                if (result["size"].GetValue<int>() != 0)
                {
                    foreach (var individualMovie in result["SearchResult"].AsArray())
                    {
                        if (individualMovie["Metadata"] != null)
                        {
                            individualSearchResults.Add(new PlexMovie((decimal)individualMovie["score"].AsValue(), (string)individualMovie["Metadata"]["ratingKey"].AsValue(), (string)individualMovie["Metadata"]["title"].AsValue(), searchString));
                        }
                    }
                }
            }
            var bestMatch = individualSearchResults.OrderByDescending(x => x.Score).First();
            return bestMatch;
        }
    }
    public static async Task<List<PlexMovie>> GetPlexResourceKeysFromPublicLetterboxdWatchList(string lbUsername)
    {
        var ids = new List<PlexMovie>();
        var lbSlugs = new List<string>();
        var fullMovieTitlesWithYear = new List<string>();

        using (var msg = new HttpRequestMessage(HttpMethod.Get, new Uri("https://letterboxd.com/" + lbUsername + "/watchlist/")))
        {
            const int maxIndividualRequests = 3; //TODO: make configurable

            using (var resp = await _client.SendAsync(msg))
            {
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync();
                var subs = html.Split("data-target-link=");
                for (int i = 1; i < subs.Length; i++)
                {
                    var endOfAttribute = subs[i].IndexOf(' ');
                    var lbSlug = subs[i].Substring(1, endOfAttribute - 2); //clean extra slashes
                    lbSlugs.Add(lbSlug);

                };
            }
        }

        //ok now because letterboxd does not always put the release year for movies that share the same title, we have to make a call to get the film's poster, which DOES

        foreach (var slug in lbSlugs)
        {
            using (var msg = new HttpRequestMessage(HttpMethod.Get, new Uri("https://letterboxd.com/ajax/poster" + slug + "std/125x187/")))
            {
                using (var resp = await _client.SendAsync(msg))
                {
                    resp.EnsureSuccessStatusCode();
                    var html = await resp.Content.ReadAsStringAsync();
                    var chunkWithTitle= html.Split("title=")[1];
                    var endOfTitle = chunkWithTitle.IndexOf('>');
                    var fullTitleWithYear = chunkWithTitle.Substring(1, endOfTitle - 2);
                    fullTitleWithYear = HttpUtility.HtmlDecode(fullTitleWithYear);
                    fullTitleWithYear = fullTitleWithYear.Replace('(', ' ').Replace(')', ' ').Trim(); // the plex search algorithm HATES paranthesis (not really but it gives real bonkers search results)

                    fullMovieTitlesWithYear.Add(fullTitleWithYear);
                }
            }
        }
        int filmIterator = 0;
        while (ids.Count < fullMovieTitlesWithYear.Count)
               
        {
            //todo: use the max requests
            var bestMatch =  await GetBestMatchForMovie(fullMovieTitlesWithYear[filmIterator]);

            ids.Add(bestMatch);

            // var html = await resp.Content.ReadAsStreamAsync();
            //  var parsedJson = JsonNode.Parse(html);

            filmIterator++;
        }
        return ids;
    }
    

    public static async Task Main(string[] args)
    {

        try
        {
            string plexToken; 
            string letterboxdUsername;
            if (args.Count() == 2)
            {
                plexToken = args[0];
                letterboxdUsername = args[1];
            }
            else
            {
                Console.WriteLine("Please enter your personal Plex Token: ");
                plexToken = Console.ReadLine();
                Console.WriteLine("Please enter your letterboxd username (ensure your watchlist is public): ");
                letterboxdUsername = Console.ReadLine();
            }
            var resourceKeys = await GetPlexResourceKeysFromPublicLetterboxdWatchList(letterboxdUsername);
            foreach (var id in resourceKeys)
            {
                if (id.Score > (decimal)0.6)
                {
                    Console.WriteLine("Adding "+  id.Title + " with Plex identifier:  " + id.ResultKey + " to Plex Watchlist");
                    bool added = await AddToPlexWatchlist(id.ResultKey, plexToken);
                    if (added)
                    {
                        Console.WriteLine("Success!");
                    }
                    else
                    {
                        Console.WriteLine("Could not add to watchlist");
                    }
                }
                else
                {
                    Console.WriteLine("Could not find a match on Plex for the letterboxd title: " + id.OriginalSearchTerm + " with confidence and will not be added.  Closest was: " + id.Title); 
                }
               
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine("First argument should be a plex token.  See https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/");
            Console.WriteLine("Second argument should be a letterboxd username.  Please also make sure the watchlist is marked public");
        }
    }
};

