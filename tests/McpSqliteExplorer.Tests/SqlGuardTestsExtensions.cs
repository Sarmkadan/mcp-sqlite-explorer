using System;
using System.Collections.Generic;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Extension methods for <see cref="SqlGuardTests"/> that provide convenient test data generation
    /// for testing SQL guard behavior and validation scenarios.
    /// </summary>
    public static class SqlGuardTestsExtensions
    {
        /// <summary>
        /// Creates a collection of read-only statements that should be allowed by a select-only guard.
        /// </summary>
        /// <param name="guard">The guard instance (unused but required for extension method pattern).</param>
        /// <returns>A read-only list of SQL statements that are valid read operations.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guard"/> is null.</exception>
        public static IReadOnlyList<string> GetAllowedReadStatements(this SqlGuardTests guard) =>
        [
            "SELECT * FROM users",
            "SELECT id, name FROM products WHERE price > 100",
            "SELECT COUNT(*) FROM orders",
            "SELECT name, email FROM customers ORDER BY created_date DESC",
            "SELECT DISTINCT category FROM items"
        ];

        /// <summary>
        /// Creates a collection of write statements that should be rejected by a select-only guard.
        /// </summary>
        /// <param name="guard">The guard instance (unused but required for extension method pattern).</param>
        /// <returns>A read-only list of SQL statements that are invalid write operations.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guard"/> is null.</exception>
        public static IReadOnlyList<string> GetRejectedWriteStatements(this SqlGuardTests guard) =>
        [
            "INSERT INTO users (name, email) VALUES ('test', 'test@example.com')",
            "UPDATE products SET price = 99.99 WHERE id = 1",
            "DELETE FROM orders WHERE id = 5",
            "INSERT INTO customers SELECT * FROM temp_customers",
            "UPDATE items SET category = 'new'"
        ];

        /// <summary>
        /// Creates a collection of statements that contain write operations hidden behind CTEs
        /// that should be rejected by a select-only guard.
        /// </summary>
        /// <param name="guard">The guard instance (unused but required for extension method pattern).</param>
        /// <returns>A read-only list of SQL statements with write operations in CTEs.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guard"/> is null.</exception>
        public static IReadOnlyList<string> GetRejectedCteWriteStatements(this SqlGuardTests guard) =>
        [
            "WITH deleted_users AS (DELETE FROM users WHERE inactive = 1) SELECT * FROM deleted_users",
            "WITH updated_products AS (UPDATE products SET price = price * 1.1) SELECT * FROM updated_products",
            "WITH inserted_orders AS (INSERT INTO orders (user_id) VALUES (1)) SELECT * FROM inserted_orders",
            "WITH temp_data AS (UPDATE temp SET processed = 1) SELECT COUNT(*) FROM temp_data"
        ];

        /// <summary>
        /// Creates a collection of statements with various limit values that should be bounded
        /// by the clamp functionality.
        /// </summary>
        /// <param name="guard">The guard instance (unused but required for extension method pattern).</param>
        /// <returns>A read-only list of SQL statements with different limit values.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guard"/> is null.</exception>
        public static IReadOnlyList<(string Statement, int OriginalLimit, int ExpectedClampedLimit)> GetLimitStatements(this SqlGuardTests guard) =>
        [
            ("SELECT * FROM users LIMIT 1000", 1000, 1000),
            ("SELECT * FROM products LIMIT 5000", 5000, 5000),
            ("SELECT * FROM orders LIMIT 10000", 10000, 10000),
            ("SELECT * FROM customers LIMIT 100", 100, 100),
            ("SELECT * FROM items LIMIT 999999", 999999, 10000)
        ];

        /// <summary>
        /// Creates a collection of empty or whitespace SQL statements that should be rejected.
        /// </summary>
        /// <param name="guard">The guard instance (unused but required for extension method pattern).</param>
        /// <returns>A read-only list of empty or whitespace SQL statements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guard"/> is null.</exception>
        public static IReadOnlyList<string> GetEmptyStatements(this SqlGuardTests guard) =>
        [
            string.Empty,
            " ",
            "\t\t",
            "\n\n",
            " \n \t "
        ];

        /// <summary>
        /// Creates a collection of multiple statements separated by semicolons that should be rejected.
        /// </summary>
        /// <param name="guard">The guard instance (unused but required for extension method pattern).</param>
        /// <returns>A read-only list of multi-statement SQL strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guard"/> is null.</exception>
        public static IReadOnlyList<string> GetMultiStatements(this SqlGuardTests guard) =>
        [
            "SELECT * FROM users; UPDATE users SET name = 'admin'",
            "SELECT id FROM products; INSERT INTO logs (message) VALUES ('access')",
            "SELECT COUNT(*) FROM orders; DELETE FROM orders WHERE id = 1",
            "SELECT name FROM customers; UPDATE customers SET email = 'new@example.com'"
        ];
    }
}