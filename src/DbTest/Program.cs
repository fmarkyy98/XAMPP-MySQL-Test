using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;

namespace DbTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await Solution1();
            // await Solution2();
        }

        #region MySql.Data based solution

        static async Task Solution1()
        {
            var server = "localhost";
            var user = "root";
            var password = "";
            var database = "my_test_db";

            // 1) First connect to SQL server WITHOUT database selected
            var rootConnectionString = $"Server={server};Uid={user};Pwd={password};";
            using (var rootConnection = new MySqlConnection(rootConnectionString))
            {
                await rootConnection.OpenAsync();

                // 1.1) Ensure database exists
                using (var cmd = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{database}`;", rootConnection))
                {
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"Database '{database}' ensured.");
                }
            }

            // 2) Now connect WITH the database specified
            var connectionString = $"Server={server};Database={database};Uid={user};Pwd={password};";
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine($"Connected to database '{database}'.");

            // 2.1) Ensure table exists
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS `cats` (
                    `ID`      INT AUTO_INCREMENT PRIMARY KEY,
                    `Name`    VARCHAR(255) NOT NULL,
                    `Is_Cute` BOOLEAN NOT NULL,
                    `Color`   VARCHAR(255) NOT NULL
                );
            ";

            using (var cmd = new MySqlCommand(createTableSql, connection))
            {
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("Table 'cats' ensured.");
            }

            // At this point the scheme setup was successfull. It's we can start manipulate the DB.


            // 3) Add some test data to the database.
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM `cats`;", connection))
            {
                // 3.1) Check if table already contains data
                var existingRows = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                if (existingRows == 0)
                {
                    Console.WriteLine("No existing rows found. Inserting sample cats...");

                    // 3.2) Insert a few test records using parameterized queries
                    var insertSql = @"
                        INSERT INTO `cats` (`Name`, `Is_Cute`, `Color`)
                        VALUES (@name, @cute, @color);
                    ";

                    using var insertCmd = new MySqlCommand(insertSql, connection);

                    insertCmd.Parameters.Add("@name", MySqlDbType.VarChar);
                    insertCmd.Parameters.Add("@cute", MySqlDbType.Byte);
                    insertCmd.Parameters.Add("@color", MySqlDbType.VarChar);

                    // Helper to avoid repeating parameter code
                    async Task InsertCat(string name, bool cute, string color)
                    {
                        insertCmd.Parameters["@name"].Value = name;
                        insertCmd.Parameters["@cute"].Value = cute ? (byte)1 : (byte)0;
                        insertCmd.Parameters["@color"].Value = color;
                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    await InsertCat("Pimpi", true, "Brown");
                    await InsertCat("Luna", true, "White");
                    await InsertCat("Shadow", false, "Black");

                    Console.WriteLine("Sample data inserted.");
                }
                else
                {
                    Console.WriteLine("Table already contains data. Skipping sample insert.");
                }
            }


            // 4) SELECT data
            using (var cmd = new MySqlCommand("SELECT * FROM `cats`;", connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var name = reader.GetString(1);
                    var isCute = reader.GetBoolean(2);
                    var color = reader.GetString(3);

                    Console.WriteLine($"{id}|{name}|{isCute}|{color}");
                }
            }
        }


        #endregion

        #region EntityFramework based solution

        public class Cat
        {
            [Column("ID")]
            public int Id { get; set; }

            [Column("Name")]
            public string Name { get; set; }

            [Column("Is_Cute")]
            public bool IsCute { get; set; }

            [Column("Color")]
            public string Color { get; set; }
        }


        public class MyDbContext : DbContext
        {
            public DbSet<Cat> Cats { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder options)
            {
                options.UseMySql(
                    "Server=localhost;Database=my_test_db;Uid=root;Pwd=;",
                    new MySqlServerVersion(new Version(8, 0, 0))  // MySQL major version
                );
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Cat>().ToTable("cats");
            }
        }

        static async Task Solution2()
        {
            using var db = new MyDbContext();

            var cats = await db.Cats.ToListAsync();

            foreach (var cat in cats)
            {
                Console.WriteLine($"{cat.Id}|{cat.Name}|{cat.IsCute}|{cat.Color}");
            }
        }

        #endregion
    }
}
