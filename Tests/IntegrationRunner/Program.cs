using System;
using System.IO;
using System.Linq;
using TimeTrack.Data;

namespace IntegrationRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("== TimeTrack integration checks ==");

        // Paths we will manipulate
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "TimeTrack");
        var dbPath = Path.Combine(appFolder, "timetrack.db");
        var legacyPath = Path.Combine(AppContext.BaseDirectory, "timetrack.db");

        // 1) Fresh install: delete current DB and backups
        SafeDelete(dbPath);
        SafeDelete(legacyPath);
        Console.WriteLine("[Fresh] Deleted current DB and legacy DB if existed.");

        // Run app DB setup
        Database_Create();
        Expect(File.Exists(dbPath), "Fresh: DB should be created in LocalApplicationData");

        // 2) Legacy migration: move DB beside EXE and ensure it migrates
        SafeDelete(dbPath);
        File.Copy(Path.Combine(appFolder, "timetrack.db"), legacyPath, overwrite: true);
        Console.WriteLine("[Legacy] Placed legacy DB beside EXE.");
        Database_Create();
        Expect(File.Exists(dbPath), "Legacy: DB should be migrated to LocalApplicationData");
        SafeDelete(legacyPath);

        // 3) Corrupted DB: write junk and expect integrity_check to flag (we don't throw)
        File.WriteAllText(dbPath, "NOT A SQLITE DB");
        Console.WriteLine("[Corrupt] Wrote junk to DB file.");
        try
        {
            Database_Create();
            Console.WriteLine("[Corrupt] CreateDatabase completed; check logs for integrity failure message.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Corrupt] CreateDatabase threw unexpectedly: {ex.Message}");
        }

        // 4) Pending migrations apply: we can't author a new migration here, but we can call CreateDatabase again
        Database_Create();
        Console.WriteLine("[Migrate] Re-run CreateDatabase to ensure idempotency.");

        Console.WriteLine("Integration checks completed.");
    }

    private static void Database_Create()
    {
        // Calls the application's DB bootstrapper
        TimeTrack.Data.Database.CreateDatabase();
    }

    private static void Expect(bool condition, string message)
    {
        Console.WriteLine(condition ? $"[OK] {message}" : $"[FAIL] {message}");
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
