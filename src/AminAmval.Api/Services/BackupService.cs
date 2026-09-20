using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AminAmval.Data;
using Microsoft.Data.SqlClient;

namespace AminAmval.Services;
public sealed class BackupService(IServiceScopeFactory scopes, Storage storage, IConfiguration config, ILogger<BackupService> logger) : BackgroundService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string? LastError { get; private set; }
    public bool Running { get; private set; }
    public int Hour => Math.Clamp(config.GetValue("BACKUP_HOUR_UTC", 22), 0, 23);
    public int Minute => Math.Clamp(config.GetValue("BACKUP_MINUTE_UTC", 30), 0, 59);
    public int Retention => Math.Clamp(config.GetValue("BACKUP_RETENTION_DAYS", 14), 2, 365);
    public DateTime NextRun { get { var next = DateTime.UtcNow.Date.AddHours(Hour).AddMinutes(Minute); return next > DateTime.UtcNow ? next : next.AddDays(1); } }
    public IEnumerable<FileInfo> Files() => new DirectoryInfo(storage.Backups).GetFiles("amin-*.zip").OrderByDescending(x => x.LastWriteTimeUtc);
    public object Status() => new { enabled = true, running = Running, lastError = LastError, lastBackup = Files().FirstOrDefault()?.LastWriteTimeUtc, nextRun = NextRun, retentionDays = Retention, hourUtc = Hour, minuteUtc = Minute };
    public async Task<string> Create(HttpContext? context = null)
    {
        if (!await gate.WaitAsync(0)) throw new ApiException(409, "پشتیبان‌گیری دیگری در حال اجراست.");
        Running = true; var stage = Path.Combine(storage.Root, ".backup-" + Guid.NewGuid().ToString("N"));
        string? tempZip = null;
        try {
            Directory.CreateDirectory(stage);
            var connStr = config.GetConnectionString("DefaultConnection") ?? "";
            var dbName = new SqlConnectionStringBuilder(connStr).InitialCatalog;
            var backupFile = Path.Combine(stage, "amin.bak");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH INIT";
                cmd.Parameters.AddWithValue("@path", backupFile);
                await cmd.ExecuteNonQueryAsync();
            }
            foreach (var folder in new[] { "uploads", "keys" }) {
                Directory.CreateDirectory(Path.Combine(stage, folder));
                foreach (var file in Directory.GetFiles(Path.Combine(storage.Root, folder))) File.Copy(file, Path.Combine(stage, folder, Path.GetFileName(file)));
            }
            var hashes = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(stage, "*", SearchOption.AllDirectories)) {
                using var stream = File.OpenRead(file); hashes[Path.GetRelativePath(stage, file).Replace('\\', '/')] = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            }
            await File.WriteAllTextAsync(Path.Combine(stage, "manifest.json"), JsonSerializer.Serialize(new { version = 1, createdUtc = DateTime.UtcNow, files = hashes }, new JsonSerializerOptions { WriteIndented = true }));
            var filename = "amin-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + ".zip";
            var path = Path.Combine(storage.Backups, filename); tempZip = path + ".tmp";
            ZipFile.CreateFromDirectory(stage, tempZip, CompressionLevel.Optimal, false); File.Move(tempZip, path);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.Log(context, "backup.create", "System", null, context == null ? "پشتیبان‌گیری خودکار پایگاه داده، تصاویر و کلیدهای نشست" : "پشتیبان‌گیری دستی پایگاه داده، تصاویر و کلیدهای نشست", after: new { filename, size = new FileInfo(path).Length }); await db.SaveChangesAsync();
            foreach (var old in Files().Where(x => x.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-Retention))) old.Delete();
            LastError = null; logger.LogInformation("Backup completed: {Filename}", filename); return filename;
        }
        catch (Exception ex) { LastError = "پشتیبان‌گیری ناموفق بود؛ فضای دیسک و دسترسی پوشهٔ داده را بررسی کنید."; logger.LogError(ex, "Backup failed"); throw; }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); if (tempZip != null && File.Exists(tempZip)) File.Delete(tempZip); Running = false; gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            if (!Files().Any()) { try { await Create(); } catch (Exception e) { logger.LogWarning(e, "Initial backup unavailable"); } }
            while (!stoppingToken.IsCancellationRequested) {
                var wait = LastError == null ? NextRun - DateTime.UtcNow : TimeSpan.FromMinutes(15);
                await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1), stoppingToken);
                try { await Create(); logger.LogInformation("Daily scheduled backup completed"); } catch (Exception e) { logger.LogWarning(e, "Scheduled backup will retry"); }
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
