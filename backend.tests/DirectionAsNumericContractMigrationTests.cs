namespace TrumpStockAlert.Api.Tests;

public sealed class DirectionAsNumericContractMigrationTests
{
    [Fact]
    public void Migration_ConvertsLegacyDirectionLabelsToIntegers()
    {
        var migrationSource = File.ReadAllText(FindMigrationFile());

        Assert.Contains("WHEN lower(\"Direction\") = 'positive' THEN 25", migrationSource);
        Assert.Contains("WHEN lower(\"Direction\") = 'negative' THEN -25", migrationSource);
        Assert.Contains("WHEN lower(\"Direction\") = 'neutral' THEN 0", migrationSource);
        Assert.Contains("WHEN lower(\"Direction\") = 'mixed' THEN 0", migrationSource);
        Assert.Contains("WHEN \"Direction\" IS NULL THEN 0", migrationSource);
        Assert.Contains("CK_post_analyses_Direction_neg50_50", migrationSource);
    }

    private static string FindMigrationFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "backend",
                "Data",
                "Migrations",
                "20260526071330_DirectionAsNumericContract.cs");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("DirectionAsNumericContract migration file was not found.");
    }
}
