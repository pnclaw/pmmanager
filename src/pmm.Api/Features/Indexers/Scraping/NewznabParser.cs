using System.Xml.Linq;

namespace pmm.Api.Features.Indexers.Scraping;

public static class NewznabParser
{
    private static readonly XNamespace Ns = "http://www.newznab.com/DTD/2010/feeds/attributes/";

    public static IReadOnlyList<ParsedIndexerRow> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var channel = doc.Root?.Element("channel");
        if (channel is null) return [];

        var results = new List<ParsedIndexerRow>();

        foreach (var item in channel.Elements("item"))
        {
            var attrs = item.Elements(Ns + "attr")
                .ToDictionary(
                    e => (string?)e.Attribute("name") ?? string.Empty,
                    e => (string?)e.Attribute("value") ?? string.Empty);

            attrs.TryGetValue("size", out var sizeStr);
            attrs.TryGetValue("category", out var categoryStr);

            var pubDateStr = (string?)item.Element("pubDate");
            DateTime? pubDate = null;
            if (!string.IsNullOrEmpty(pubDateStr) && DateTimeOffset.TryParse(pubDateStr, out var dto))
                pubDate = dto.UtcDateTime;

            var enclosureLength = (string?)item.Element("enclosure")?.Attribute("length");

            results.Add(new ParsedIndexerRow
            {
                Title = (string?)item.Element("title") ?? string.Empty,
                NzbId = (string?)item.Element("guid") ?? string.Empty,
                NzbUrl = (string?)item.Element("link") ?? string.Empty,
                NzbSize = long.TryParse(sizeStr, out var size) ? size : 0,
                NzbPublishedAt = pubDate,
                FileSize = long.TryParse(enclosureLength, out var fs) ? fs : null,
                Category = int.TryParse(categoryStr, out var cat) ? cat : 0,
            });
        }

        return results;
    }
}

public record ParsedIndexerRow
{
    public string Title { get; init; } = string.Empty;
    public string NzbId { get; init; } = string.Empty;
    public string NzbUrl { get; init; } = string.Empty;
    public long NzbSize { get; init; }
    public DateTime? NzbPublishedAt { get; init; }
    public long? FileSize { get; init; }
    public int Category { get; init; }
}
