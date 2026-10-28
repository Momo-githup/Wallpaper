using Microsoft.Data.Sqlite;
using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class LibraryDatabase
{
    private readonly string _connectionString;

    public LibraryDatabase(AppPaths paths)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = paths.Database }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Wallpapers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FilePath TEXT NOT NULL COLLATE NOCASE UNIQUE,
                DisplayName TEXT NOT NULL,
                PixelWidth INTEGER NOT NULL DEFAULT 0,
                PixelHeight INTEGER NOT NULL DEFAULT 0,
                FileSize INTEGER NOT NULL DEFAULT 0,
                ImportedAt TEXT NOT NULL,
                IsFavorite INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<WallpaperItem>> GetAllAsync()
    {
        var result = new List<WallpaperItem>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FilePath, DisplayName, PixelWidth, PixelHeight, FileSize, ImportedAt, IsFavorite FROM Wallpapers ORDER BY ImportedAt DESC";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new WallpaperItem(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4),
                reader.GetInt64(5), DateTime.Parse(reader.GetString(6)), reader.GetInt64(7) != 0));
        }
        return result;
    }

    public async Task<int> AddAsync(IEnumerable<(string Path, int Width, int Height, long Size)> files)
    {
        var added = 0;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var file in files)
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO Wallpapers(FilePath, DisplayName, PixelWidth, PixelHeight, FileSize, ImportedAt)
                VALUES($path, $name, $width, $height, $size, $importedAt);
                """;
            command.Parameters.AddWithValue("$path", file.Path);
            command.Parameters.AddWithValue("$name", Path.GetFileNameWithoutExtension(file.Path));
            command.Parameters.AddWithValue("$width", file.Width);
            command.Parameters.AddWithValue("$height", file.Height);
            command.Parameters.AddWithValue("$size", file.Size);
            command.Parameters.AddWithValue("$importedAt", DateTime.UtcNow.ToString("O"));
            added += await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        return added;
    }

    public async Task RemoveAsync(long id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Wallpapers WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }
}
