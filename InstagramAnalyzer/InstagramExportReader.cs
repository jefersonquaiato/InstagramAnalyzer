using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace InstagramAnalyzer.InstagramAnalyzer;

public sealed class InstagramExportReader
{
    private static readonly Regex UsernameRegex =
        new(@"^[A-Za-z0-9._]{1,30}$", RegexOptions.Compiled);

    public (HashSet<string> Followers, HashSet<string> Following) ReadZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .ToList();

        // Prefer one consistent export format. Instagram normally supplies either HTML or JSON.
        var htmlEntries = entries.Where(e =>
            e.FullName.Contains("followers_and_following", StringComparison.OrdinalIgnoreCase) &&
            e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)).ToList();

        var jsonEntries = entries.Where(e =>
            e.FullName.Contains("followers_and_following", StringComparison.OrdinalIgnoreCase) &&
            e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();

        if (htmlEntries.Count > 0)
            return ReadHtml(htmlEntries);

        if (jsonEntries.Count > 0)
            return ReadJson(jsonEntries);

        throw new InvalidDataException(
            "Não encontrei arquivos HTML ou JSON na pasta followers_and_following da exportação.");
    }

    private static (HashSet<string>, HashSet<string>) ReadHtml(List<ZipArchiveEntry> entries)
    {
        var followers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var following = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry.FullName);
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var html = reader.ReadToEnd();

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            foreach (var anchor in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
            {
                var href = HtmlEntity.DeEntitize(anchor.GetAttributeValue("href", ""));
                var username = ExtractUsernameFromUrl(href);
                if (username is null)
                    continue;

                if (IsFollowingFile(name))
                    following.Add(username);
                else if (IsFollowerFile(name))
                    followers.Add(username);
            }
        }

        ValidateFound(followers, following);
        return (followers, following);
    }

    private static (HashSet<string>, HashSet<string>) ReadJson(List<ZipArchiveEntry> entries)
    {
        var followers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var following = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry.FullName);
            using var stream = entry.Open();
            using var document = JsonDocument.Parse(stream);

            if (IsFollowingFile(name))
            {
                // Instagram's following.json wraps entries in "relationships_following".
                FindJsonProfiles(document.RootElement, following);
            }
            else if (IsFollowerFile(name))
            {
                FindJsonProfiles(document.RootElement, followers);
            }
        }

        ValidateFound(followers, following);
        return (followers, following);
    }

    private static void FindJsonProfiles(JsonElement element, HashSet<string> destination)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string? title = null;
            if (element.TryGetProperty("title", out var titleElement) &&
                titleElement.ValueKind == JsonValueKind.String)
                title = titleElement.GetString();

            if (!string.IsNullOrWhiteSpace(title) && IsValidUsername(title))
                destination.Add(title.Trim().TrimStart('@'));

            if (element.TryGetProperty("string_list_data", out var list) &&
                list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    if (item.TryGetProperty("value", out var value) &&
                        value.ValueKind == JsonValueKind.String)
                    {
                        var username = value.GetString()?.Trim().TrimStart('@');
                        if (IsValidUsername(username))
                            destination.Add(username!);
                    }
                    if (item.TryGetProperty("href", out var href) &&
                        href.ValueKind == JsonValueKind.String)
                    {
                        var username = ExtractUsernameFromUrl(href.GetString() ?? "");
                        if (username is not null)
                            destination.Add(username);
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
                FindJsonProfiles(property.Value, destination);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                FindJsonProfiles(child, destination);
        }
    }

    private static string? ExtractUsernameFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host.ToLowerInvariant();
        if (host != "instagram.com" && host != "www.instagram.com")
            return null;

        var segment = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(segment) ||
            segment.Equals("p", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("reel", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("stories", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("explore", StringComparison.OrdinalIgnoreCase))
            return null;

        segment = Uri.UnescapeDataString(segment).TrimStart('@');
        return IsValidUsername(segment) ? segment : null;
    }

    private static bool IsValidUsername(string? value) =>
        !string.IsNullOrWhiteSpace(value) && UsernameRegex.IsMatch(value.Trim().TrimStart('@'));

    private static bool IsFollowingFile(string name) =>
        name.Contains("following", StringComparison.OrdinalIgnoreCase);

    private static bool IsFollowerFile(string name) =>
        name.StartsWith("followers", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("following", StringComparison.OrdinalIgnoreCase);

    private static void ValidateFound(HashSet<string> followers, HashSet<string> following)
    {
        if (followers.Count == 0 || following.Count == 0)
            throw new InvalidDataException(
                $"Não consegui identificar os dois conjuntos. Seguidores encontrados: {followers.Count}; seguindo encontrados: {following.Count}. Confira se a exportação inclui 'Seguidores e seguindo'.");
    }
}
