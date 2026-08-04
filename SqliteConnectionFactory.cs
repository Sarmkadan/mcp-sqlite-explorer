public SqliteConnectionFactory(string databasePath)
        {
            if (!System.IO.File.Exists(databasePath))
            {
                throw new ArgumentException("Database file does not exist.", nameof(databasePath));
            }
            if (!databasePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Database file must be a SQLite file.", nameof(databasePath));
            }
        }