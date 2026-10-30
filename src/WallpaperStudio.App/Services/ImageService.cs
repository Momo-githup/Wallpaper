using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace WallpaperStudio.App.Services;

public sealed class ImageService
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp"
    };

    private readonly AppPaths _paths;

    public ImageService(AppPaths paths) => _paths = paths;

    public bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public Task<(int Width, int Height, long Size)?> InspectAsync(string path)
    {
        return Task.Run<(int Width, int Height, long Size)?>(() =>
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var codec = SKCodec.Create(stream);
                return codec is null ? null : (codec.Info.Width, codec.Info.Height, new FileInfo(path).Length);
            }
            catch { return null; }
        });
    }

    public async Task<BitmapImage?> GetThumbnailAsync(string path)
    {
        try
        {
            var cachePath = GetCachePath(path, "thumb", ".jpg");
            if (!File.Exists(cachePath) || File.GetLastWriteTimeUtc(cachePath) < File.GetLastWriteTimeUtc(path))
            {
                await Task.Run(() => CreateThumbnail(path, cachePath));
            }
            return LoadBitmap(cachePath);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> PrepareDisplayFileAsync(string path)
    {
        if (!Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase)) return path;
        var cachePath = GetCachePath(path, "display", ".png");
        if (!File.Exists(cachePath) || File.GetLastWriteTimeUtc(cachePath) < File.GetLastWriteTimeUtc(path))
        {
            await Task.Run(() => ConvertToPng(path, cachePath));
        }
        return cachePath;
    }

    public async Task<CommunityUploadFiles> PrepareCommunityFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("图片文件不存在。", path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var uploadPath = path;
        var mimeType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/jpeg",
            _ => throw new InvalidDataException("社区仅支持 JPG、PNG、WebP 和 BMP 图片。")
        };
        if (extension == ".bmp")
        {
            uploadPath = GetCachePath(path, "community", ".jpg");
            if (!File.Exists(uploadPath) || File.GetLastWriteTimeUtc(uploadPath) < File.GetLastWriteTimeUtc(path))
                await Task.Run(() => ConvertToJpeg(path, uploadPath));
            extension = ".jpg";
        }
        else if (extension == ".jpeg")
        {
            extension = ".jpg";
        }

        var info = await InspectAsync(uploadPath) ?? throw new InvalidDataException("无法读取图片信息。");
        if (info.Width > 8192 || info.Height > 8192)
            throw new InvalidDataException("社区图片的宽度和高度不能超过 8192 像素。");
        if (info.Size > 25L * 1024 * 1024)
            throw new InvalidDataException("社区图片不能超过 25 MB。");

        var thumbnailPath = GetCachePath(path, "thumb", ".jpg");
        if (!File.Exists(thumbnailPath) || File.GetLastWriteTimeUtc(thumbnailPath) < File.GetLastWriteTimeUtc(path))
            await Task.Run(() => CreateThumbnail(path, thumbnailPath));

        await using var content = new FileStream(uploadPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var contentHash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken)).ToLowerInvariant();
        content.Position = 0;
#pragma warning disable CA5351 // MD5 is used only to match legacy Storage ETags, not for security.
        var legacyETag = Convert.ToHexString(await MD5.HashDataAsync(content, cancellationToken)).ToLowerInvariant();
#pragma warning restore CA5351
        return new CommunityUploadFiles(uploadPath, thumbnailPath, extension, mimeType,
            info.Width, info.Height, info.Size, contentHash, legacyETag);
    }

    public static BitmapImage LoadBitmap(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private string GetCachePath(string path, string suffix, string extension)
    {
        var value = $"{Path.GetFullPath(path).ToUpperInvariant()}|{File.GetLastWriteTimeUtc(path).Ticks}|{suffix}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return Path.Combine(_paths.Cache, hash + extension);
    }

    private static void CreateThumbnail(string source, string destination)
    {
        using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("无法解码图片。");
        var ratio = Math.Min(480d / bitmap.Width, 320d / bitmap.Height);
        ratio = Math.Min(1d, ratio);
        var info = new SKImageInfo(Math.Max(1, (int)(bitmap.Width * ratio)), Math.Max(1, (int)(bitmap.Height * ratio)));
        using var resized = bitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidDataException("无法创建缩略图。");
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 82);
        using var output = File.Create(destination);
        data.SaveTo(output);
    }

    private static void ConvertToPng(string source, string destination)
    {
        using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("无法解码 WebP 图片。");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(destination);
        data.SaveTo(output);
    }

    private static void ConvertToJpeg(string source, string destination)
    {
        using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("无法解码图片。");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var output = File.Create(destination);
        data.SaveTo(output);
    }
}

public sealed record CommunityUploadFiles(
    string FilePath,
    string ThumbnailPath,
    string Extension,
    string MimeType,
    int Width,
    int Height,
    long FileSize,
    string ContentHash,
    string LegacyETag);
