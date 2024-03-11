﻿using System.Net.Http;
using System.Threading;
using Mewdeko.Modules.Nsfw.Common;
using Mewdeko.Modules.Nsfw.Common.Downloaders;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace Mewdeko.Modules.Nsfw;

public class SearchImageCacher : INService
{
    private readonly IHttpClientFactory httpFactory;
    private readonly Random rng;

    private static readonly ISet<string> DefaultTagBlacklist = new HashSet<string>
    {
        "loli",
        "lolicon",
        "shota",
        "shotacon",
        "cub"
    };

    private readonly Dictionary<Booru, object> typeLocks = new();
    private readonly Dictionary<Booru, HashSet<string>> usedTags = new();
    private readonly IMemoryCache cache;

    public SearchImageCacher(IHttpClientFactory httpFactory, IMemoryCache cache)
    {
        this.httpFactory = httpFactory;
        rng = new MewdekoRandom();
        this.cache = cache;

        // initialize new cache with empty values
        foreach (var type in Enum.GetValues<Booru>())
        {
            typeLocks[type] = new object();
            usedTags[type] = [];
        }
    }

    private static string Key(Booru boory, string tag)
        => $"booru:{boory}__tag:{tag}";

    /// <summary>
    /// Download images of the specified type, and cache them.
    /// </summary>
    /// <param name="tags">Required tags</param>
    /// <param name="forceExplicit">Whether images will be forced to be explicit</param>
    /// <param name="type">Provider type</param>
    /// <param name="cancel">Cancellation token</param>
    /// <returns>Whether any image is found.</returns>
    private async Task<bool> UpdateImagesInternalAsync(string[] tags, bool forceExplicit, Booru type,
        CancellationToken cancel)
    {
        var images = await DownloadImagesAsync(tags, forceExplicit, type, cancel).ConfigureAwait(false);
        if (!images.Any())
        {
            // Log.Warning("Got no images for {0}, tags: {1}", type, string.Join(", ", tags));
            return false;
        }
#if DEBUG
        Log.Information("Updating {0}...", type);
#endif
        lock (typeLocks[type])
        {
            var typeUsedTags = usedTags[type];
            foreach (var tag in tags)
                typeUsedTags.Add(tag);

            // if user uses no tags for the hentai command and there are no used
            // tags atm, just select 50 random tags from downloaded images to seed
            if (typeUsedTags.Count == 0)
            {
                images.SelectMany(x => x.Tags)
                    .Distinct()
                    .Shuffle()
                    .Take(50)
                    .ForEach(x => typeUsedTags.Add(x));
            }

            foreach (var img in images)
            {
                // if any of the tags is a tag banned by discord
                // do not put that image in the cache
                if (DefaultTagBlacklist.Overlaps(img.Tags))
                    continue;

                // if image doesn't have a proper absolute uri, skip it
                if (!Uri.IsWellFormedUriString(img.FileUrl, UriKind.Absolute))
                    continue;

                // i'm appending current tags because of tag aliasing
                // this way, if user uses tag alias, for example 'kissing' -
                // both 'kiss' (real tag returned by the image) and 'kissing' will be populated with
                // retreived images
                foreach (var tag in img.Tags.Concat(tags).Distinct())
                {
                    if (!typeUsedTags.Contains(tag)) continue;
                    var set = cache.GetOrCreate(Key(type, tag), e =>
                    {
                        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                        return new HashSet<ImageData>();
                    });

                    if (set.Count < 100)
                        set.Add(img);
                }
            }
        }

        return true;
    }

    private ImageData? QueryLocal(string[] tags, Booru type, IReadOnlySet<string> blacklistedTags)
    {
        var setList = new List<HashSet<ImageData>>();

        // ofc make sure no changes are happening while we're getting a random one
        lock (typeLocks[type])
        {
            // if no tags are provided, get a random tag
            if (tags.Length == 0)
            {
                // get all tags in the cache
                if (usedTags.TryGetValue(type, out var allTags)
                    && allTags.Count > 0)
                {
                    tags =
                    [
                        allTags.ToList()[rng.Next(0, allTags.Count)]
                    ];
                }
                else
                {
                    return null;
                }
            }

            foreach (var tag in tags)
            {
                // if any tag is missing from cache, that means there is no result
                if (cache.TryGetValue<HashSet<ImageData>>(Key(type, tag), out var set))
                    setList.Add(set);
                else
                    return null;
            }

            if (setList.Count == 0)
                return null;

            List<ImageData> resultList;
            // if multiple tags, we need to interesect sets
            if (setList.Count > 1)
            {
                // now that we have sets, interesect them to find eligible items
                // make a copy of the 1st set
                var resultSet = new HashSet<ImageData>(setList[0]);

                // go through all other sets, and
                for (var i = 1; i < setList.Count; ++i)
                {
                    // if any of the elements in result set are not present in the current set
                    // remove it from the result set
                    resultSet.IntersectWith(setList[i]);
                }

                resultList = resultSet.ToList();
            }
            else
            {
                // if only one tag, use that set
                resultList = setList[0].ToList();
            }

            // return a random one which doesn't have blacklisted tags in it
            resultList = resultList.Where(x => !blacklistedTags.Overlaps(x.Tags)).ToList();

            // if no items in the set -> not found
            if (resultList.Count == 0)
                return null;

            var toReturn = resultList[rng.Next(0, resultList.Count)];

            // remove from cache
            foreach (var tag in tags)
            {
                if (cache.TryGetValue<HashSet<ImageData>>(Key(type, tag), out var items))
                {
                    items.Remove(toReturn);
                }
            }

            return toReturn;
        }
    }

    public async Task<ImageData?> GetImageNew(string?[] tags, bool forceExplicit, Booru type,
        HashSet<string> blacklistedTags, CancellationToken cancel)
    {
        // make sure tags are proper
        tags = tags
            .Where(x => x is not null)
            .Select(tag => tag.ToLowerInvariant().Trim())
            .Distinct()
            .ToArray();

        if (tags.Length > 2 && type == Booru.Danbooru)
            tags = tags[..2];

        // use both tags banned by discord and tags banned on the server
        if (blacklistedTags.Overlaps(tags) || DefaultTagBlacklist.Overlaps(tags))
            return default;

        // query for an image
        var image = QueryLocal(tags, type, blacklistedTags);
        if (image is not null)
            return image;

        bool success;
        try
        {
            // if image is not found, update the cache and query again
            success = await UpdateImagesInternalAsync(tags, forceExplicit, type, cancel).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return default;
        }

        return !success ? default : QueryLocal(tags, type, blacklistedTags);
    }

    private readonly ConcurrentDictionary<(Booru, string), int> maxPages = new();

    public async Task<List<ImageData?>> DownloadImagesAsync(string[] tags, bool isExplicit, Booru type,
        CancellationToken cancel)
    {
        var tagStr = string.Join(' ', tags.OrderByDescending(x => x));

        var attempt = 0;
        while (attempt++ <= 10)
        {
            int page;
            if (maxPages.TryGetValue((type, tagStr), out var maxPage))
            {
                if (maxPage == 0)
                {
#if DEBUG
                    Log.Information("Tag {0} yields no result on {1}, skipping", tagStr, type);
#endif
                    return new List<ImageData>();
                }

                page = rng.Next(0, maxPage);
            }
            else
            {
                page = rng.Next(0, 11);
            }

            var result = await DownloadImagesAsync(tags, isExplicit, type, page, cancel).ConfigureAwait(false);

            if (result is not (null or { Count: 0 })) return result;
#if DEBUG
            Log.Information("Tag {0}, page {1} has no result on {2}", string.Join(", ", tags), page, type.ToString());
#endif
        }

        return new List<ImageData>();
    }

    private IImageDownloader GetImageDownloader(Booru booru)
        => booru switch
        {
            Booru.Danbooru => new DanbooruImageDownloader(httpFactory),
            Booru.Yandere => new YandereImageDownloader(httpFactory),
            Booru.Konachan => new KonachanImageDownloader(httpFactory),
            Booru.Safebooru => new SafebooruImageDownloader(httpFactory),
            Booru.E621 => new E621ImageDownloader(httpFactory),
            Booru.Derpibooru => new DerpibooruImageDownloader(httpFactory),
            Booru.Gelbooru => new GelbooruImageDownloader(httpFactory),
            Booru.Rule34 => new Rule34ImageDownloader(httpFactory),
            Booru.Sankaku => new SankakuImageDownloader(httpFactory),
            Booru.Realbooru => new RealbooruImageDownloader(httpFactory),
            _ => throw new NotImplementedException($"{booru} downloader not implemented.")
        };


    private async Task<List<ImageData>> DownloadImagesAsync(string[] tags, bool isExplicit, Booru type, int page,
        CancellationToken cancel)
    {
        try
        {
#if DEBUG
            Log.Information("Downloading from {0} (page {1})...", type, page);
#endif

            using var http = httpFactory.CreateClient();
            var downloader = GetImageDownloader(type);

            var images = await downloader.DownloadImageDataAsync(tags, page, isExplicit, cancel).ConfigureAwait(false);
            if (images.Count != 0) return images;
            var tagStr = string.Join(' ', tags.OrderByDescending(x => x));
            maxPages[(type, tagStr)] = page;

            return images;
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException or TaskCanceledException)
                return [];
            Log.Error(ex, "Error downloading an image:\nTags: {0}\nType: {1}\nPage: {2}\nMessage: {3}",
                string.Join(", ", tags),
                type,
                page,
                ex.Message);
            return [];
        }
    }
}