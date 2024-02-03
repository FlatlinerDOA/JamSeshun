using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace JamSeshun.Services;

internal sealed class GuitarTabsService
{
    public async Task<IReadOnlyList<TabReference>> SearchAsync(string searchText) => await WikiTab.SearchAsync(searchText);

    public async Task<Tab> GetTabAsync(TabReference tabReference) => await WikiTab.DownloadTabAsync(tabReference.Url);
}


internal static class WikiTab
{
    private const string HtmlCodeTable = @""" &quot;
& &amp;
  &nsbp;
< &lt;
> &gt;
- &ndash;
' &rsquo;
À &Agrave;
Á &Aacute;
Â &Acirc;
Ã &Atilde;
Ä &Auml;
Å &Aring;
à &agrave;
á &aacute;
â &acirc;
ã &atilde;
ä &auml;
å &aring;
Æ &AElig;
æ &aelig;
ß &szlig;
Ç &Ccedil;
ç &ccedil;
È &Egrave;
É &Eacute;
Ê &Ecirc;
Ë &Euml;
è &egrave;
é &eacute;
ê &ecirc;
ë &euml;
ƒ &#131;
Ì &Igrave;
Í &Iacute;
Î &Icirc;
Ï &Iuml;
ì &igrave;
í &iacute;
î &icirc;
ï &iuml;
Ñ &Ntilde;
ñ &ntilde;
Ò &Ograve;
Ó &Oacute;
Ô &Ocirc;
Õ &Otilde;
Ö &Ouml;
ò &ograve;
ó &oacute;
ô &ocirc;
õ &otilde;
ö &ouml;
Ø &Oslash;
ø &oslash;
Œ &#140;
œ &#156;
Š &#138;
š &#154;
Ù &Ugrave;
Ú &Uacute;
Û &Ucirc;
Ü &Uuml;
ù &ugrave;
ú &uacute;
û &ucirc;
ü &uuml;
µ &#181;
× &#215;
Ý &Yacute;
Ÿ &#159;
ý &yacute;
ÿ &yuml;
° &#176;
† &#134;
‡ &#135;
< &lt;
> &gt;
± &#177;
« &#171;
» &#187;
¿ &#191;
¡ &#161;
· &#183;
• &#149;
™ &#153;
© &copy;
® &reg;
§ &#167;
¶ &#182;
• &bull;
… &hellip;
′ &prime;
″ &Prime;
‾ &oline;
⁄ &frasl;
℘ &weierp;
ℑ &image;
ℜ &real;
™ &trade;
ℵ &alefsym;
← &larr;
↑ &uarr;
→ &rarr;
↓ &darr;
↔ &barr;
↵ &crarr;
⇐ &lArr;
⇑ &uArr;
⇒ &rArr;
⇓ &dArr;
⇔ &hArr;
∀ &forall;
∂ &part;
∃ &exist;
∅ &empty;
∇ &nabla;
∈ &isin;
∉ &notin;
∋ &ni;
∏ &prod;
∑ &sum;
− &minus;
∗ &lowast
√ &radic;
∝ &prop;
∞ &infin;
Œ &OEig;
œ &oelig;
Ÿ &Yuml;
♠ &spades;
♣ &clubs;
♥ &hearts;
♦ &diams;
ϑ &thetasym;
ϒ &upsih;
ϖ &piv;
Š &Scaron;
š &scaron;
∠ &ang;
∧ &and;
∨ &or;
∩ &cap;
∪ &cup;
∫ &int;
∴ &there4;
∼ &sim;
≅ &cong;
≈ &asymp;
≠ &ne;
≡ &equiv;
≤ &le;
≥ &ge;
⊂ &sub;
⊃ &sup;
⊄ &nsub;
⊆ &sube;
⊇ &supe;
⊕ &oplus;
⊗ &otimes;
⊥ &perp;
⋅ &sdot;
⌈ &lcell;
⌉ &rcell;
⌊ &lfloor;
⌋ &rfloor;
⟨ &lang;
⟩ &rang;
◊ &loz;";

    private static readonly HttpClient client = new HttpClient();

    private static readonly IReadOnlyList<(string Text, string Code)> HtmlCodes = (from r in HtmlCodeTable.Split('\n')
                                                                                   let index = r.LastIndexOf(' ')
                                                                                   select (Text: r.Substring(0, index), Code: r.Substring(index + 1).Trim())).ToList();

    public static async Task<IReadOnlyList<TabReference>> SearchAsync(string searchText)
    {
        var pageToLoad = 1;
        var value = Uri.EscapeUriString(searchText);
        var finalResults = new List<TabReference>();
        bool moreToDownload = false;
        do
        {
            var html = await client.GetStringAsync($"https://www.ultimate-guitar.com/search.php?search_type=title&value={value}&page={pageToLoad}");
            var root = GetJsStore(html);
            var data = root["store"]["page"]["data"];
            var pageCount = (int)data["pagination"]["total"];
            var currentPage = (int)data["pagination"]["current"];
            var results = data["results"] as JArray;
            moreToDownload = pageCount > 1 && currentPage < pageCount && currentPage == pageToLoad;
            var urls = from result in results
                       let artist = (string)result["artist_name"]
                       let song = (string)result["song_name"]
                       let rating = (decimal?)result["rating"] ?? 0m
                       let url = (string)result["tab_url"]
                       let version = (int?)result["version"] ?? 0
                       let type = (string)result["type"]
                       let votes = (int?)result["votes"] ?? 0
                       select new TabReference(artist, song, version, type, votes, rating, url);
            finalResults.AddRange(urls);
            pageToLoad++;
        }
        while (moreToDownload);
        return finalResults;
    }

    public static async Task<Tab> DownloadTabAsync(string url)
    {
        var html = await client.GetStringAsync(url);
        var root = GetJsStore(html);
        var data = root["store"]["page"]["data"];
        var wiki_tab = data["tab_view"]["wiki_tab"]["content"].ToString();
        var rating = (decimal)data["tab"]["rating"];
        var artist = data["tab"]["artist_name"].ToString();
        var song = data["tab"]["song_name"].ToString();
        var content = SimplifyWiki(wiki_tab);
        var type = data["tab"]["type"].ToString();
        var version = (int)data["tab"]["version"];
        var votes = (int)data["tab"]["votes"];
        var name = new TabReference(artist, song, version, type, votes, rating, url);
        var tuningRef = data["tab_view"]?["meta"]?["tuning"] as JObject;
        var tuning = new Tuning(
            (string)tuningRef?["name"] ?? string.Empty,
            (string)tuningRef?["value"] ?? string.Empty,
            (int?)data["tab_view"]["meta"]["capo"] ?? 0);

        var chords = from key in ((JObject)data["tab_view"]["applicature"]).Properties()
                     let item = ((JArray)key.Value).First()
                     select new Chord(key.Name, (string)item["id"], (string)item["type"], ((JArray)item["frets"]).Select(r => (int)r).ToArray(), ((JArray)item["fingers"]).Select(r => (int)r).ToArray());
        return new Tab(name, tuning, content, chords.ToList());
    }

    private static JObject GetJsStore(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var js = doc.DocumentNode.Descendants("div").FirstOrDefault(dn => dn.GetAttributeValue("class", string.Empty) == "js-store")?.GetAttributeValue("data-content", string.Empty);
        js = ReplaceHtmlCodes(js);
        var root = (JObject)JsonConvert.DeserializeObject(js);
        return root;
    }

    private static string ReplaceHtmlCodes(string html)
    {
        var sb = new StringBuilder(html);
        foreach (var replacement in HtmlCodes)
        {
            sb.Replace(replacement.Code, replacement.Text);
        }

        return sb.ToString();
    }

    private static string SimplifyWiki(string wikiTab)
    {
        var sb = new StringBuilder(wikiTab);
        sb.Replace("[tab]", string.Empty)
            .Replace("[/tab]", string.Empty)
            .Replace("[ch]", string.Empty)
            .Replace("[/ch]", string.Empty);
        return sb.ToString();
    }
}

public record TabReference(string Artist, string Song, int Version, string Type, int Votes, decimal Rating, string Url)
{
    public decimal Score => (decimal)(Math.Sqrt((double)this.Votes) * (double)this.Rating);
    public string FileName => SafeFileName($"{this.Artist} - {this.Song} V{this.Version}.{this.Type}.txt");

    public bool Exists(string targetFolder) => File.Exists(Path.Combine(targetFolder, this.FileName));

    private string SafeFileName(string fileName)
    {
        var s = new StringBuilder(fileName);
        foreach (var c in Path.GetInvalidPathChars())
        {
            s.Replace(c, '_');
        }

        return s.ToString();
    }

    public override string ToString() => $"{this.Artist} - {this.Song} V{this.Version}";
}

public record Tuning(string Name, string Notes, int Capo)
{
    private string CapoSummary => this.Capo == 0 ? "(None)" : this.Capo.ToString();
    public override string ToString() => $"{this.Name} Tuning: {this.Notes}\nCapo: {this.CapoSummary}";
}

public record Chord(string Name, string Id, string Type, int[] Frets, int[] Fingers)
{
    public override string ToString() => (this.Name + ":").PadRight(6) + $"[{this.Id}]";
}

public record Tab(TabReference Name, Tuning Tuning, string WikiTab, IReadOnlyList<Chord> Chords)
{
    public string ChordSummary => this.Chords.Any() ? $"Chords:\n{string.Join('\n', this.Chords)}" : string.Empty;

    public override string ToString() => $"{this.Tuning}\n{this.ChordSummary}\n\n{this.WikiTab}";

    public async Task SaveAsync(string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);
        var fileName = Path.Combine(targetFolder, this.Name.FileName);
        await File.WriteAllTextAsync(fileName, this.ToString());
    }
}