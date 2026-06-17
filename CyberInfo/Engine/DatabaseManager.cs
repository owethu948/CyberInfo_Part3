using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Manages task persistence using a MySQL database.
    /// Creates the database and tasks table automatically on first run.
    /// Supports Add, Read (with filter), MarkComplete, and Delete operations.
    /// Task 1 requirement.
    /// </summary>
    public class DatabaseManager
    {
        private const string DefaultConnection =
            "Server=localhost;Database=cyberinfo;Uid=root;Pwd=;CharSet=utf8mb4;";

        private readonly string _connectionString;

        public DatabaseManager(string? customConnectionString = null)
        {
            _connectionString = customConnectionString ?? DefaultConnection;
            EnsureDatabaseAndTable();
        }

        // ── Schema setup ───────────────────────────────────────────────────────

        private void EnsureDatabaseAndTable()
        {
            var builder   = new MySqlConnectionStringBuilder(_connectionString);
            string dbName = builder.Database;
            builder.Database = string.Empty;           // connect without DB first

            using var conn = new MySqlConnection(builder.ConnectionString);
            conn.Open();

            // Create DB if absent
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4";
                cmd.ExecuteNonQuery();
            }

            conn.ChangeDatabase(dbName);

            // Create tasks table if absent
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tasks (
                        id            INT AUTO_INCREMENT PRIMARY KEY,
                        title         VARCHAR(255)  NOT NULL,
                        description   TEXT          NULL,
                        created_date  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        reminder_date DATETIME      NULL,
                        is_completed  TINYINT(1)    NOT NULL DEFAULT 0
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                cmd.ExecuteNonQuery();
            }
        }

        // ── CRUD operations ────────────────────────────────────────────────────

        /// <summary>Inserts a new task and returns its auto-generated ID.</summary>
        public int AddTask(string title, string? description = null, DateTime? reminderDate = null)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO tasks (title, description, reminder_date)
                VALUES (@title, @desc, @reminder);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@title",    title);
            cmd.Parameters.AddWithValue("@desc",     (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@reminder", reminderDate ?? (object)DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// Returns all tasks. When <paramref name="includeCompleted"/> is false,
        /// only pending tasks are returned, ordered by reminder date.
        /// </summary>
        public List<TaskRecord> GetTasks(bool includeCompleted = false)
        {
            var results = new List<TaskRecord>();
            string sql  = includeCompleted
                ? "SELECT * FROM tasks ORDER BY is_completed ASC, created_date DESC"
                : "SELECT * FROM tasks WHERE is_completed = 0 ORDER BY reminder_date ASC, created_date DESC";

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd    = conn.CreateCommand();
            cmd.CommandText  = sql;
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                results.Add(new TaskRecord(
                    Id:          reader.GetInt32("id"),
                    Title:       reader.GetString("title"),
                    Description: reader.IsDBNull(reader.GetOrdinal("description"))
                                     ? null
                                     : reader.GetString("description"),
                    Created:     reader.GetDateTime("created_date"),
                    Reminder:    reader.IsDBNull(reader.GetOrdinal("reminder_date"))
                                     ? null
                                     : reader.GetDateTime("reminder_date"),
                    IsCompleted: reader.GetBoolean("is_completed")
                ));
            }

            return results;
        }

        /// <summary>Marks a task as completed. Returns true if a row was updated.</summary>
        public bool MarkTaskCompleted(int id)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd   = conn.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET is_completed = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>Permanently deletes a task by ID. Returns true if deleted.</summary>
        public bool DeleteTask(int id)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd   = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ── Value record for task data transfer ───────────────────────────────────

    /// <summary>Immutable snapshot of a task row from the database.</summary>
    public record TaskRecord(
        int       Id,
        string    Title,
        string?   Description,
        DateTime  Created,
        DateTime? Reminder,
        bool      IsCompleted
    );
}
