using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.SqlTypes;
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

            // 2.1) Ensure tables exist
            var createSchemaSql = @"
                CREATE TABLE IF NOT EXISTS `owners` (
                    `ID`   INT AUTO_INCREMENT PRIMARY KEY,
                    `Name` VARCHAR(255) NOT NULL
                );

                CREATE TABLE IF NOT EXISTS `cats` (
                    `ID`        INT AUTO_INCREMENT PRIMARY KEY,
                    `Name`      VARCHAR(255) NOT NULL,
                    `Is_Cute`   BOOLEAN NOT NULL,
                    `Color`     VARCHAR(255) NOT NULL,
                    `Owner_ID`  INT NULL,
                    FOREIGN KEY (`Owner_ID`) REFERENCES `owners`(`ID`)
                        ON DELETE SET NULL
                        ON UPDATE CASCADE
                );
            ";
            using (var cmd = new MySqlCommand(createSchemaSql, connection))
            {
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("Tables 'owners' and 'cats' ensured.");
            }

            // At this point the scheme setup was successfull. It's we can start manipulate the DB.

            // 3) Add some test data to the database.
            // We only insert sample owners and cats if BOTH tables are completely empty.
            using (var cmd = new MySqlCommand(@"
                SELECT (SELECT COUNT(*) FROM `owners`) + (SELECT COUNT(*) FROM `cats`);
            ", connection))
            {
                // 3.1) Check if both tables contain zero rows
                var totalRows = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                if (totalRows == 0)
                {
                    Console.WriteLine("No existing rows found in owners or cats. Inserting sample data...");

                    // 3.2) Insert sample owners
                    var insertOwnerSql = @"
                        INSERT INTO `owners` (`Name`)
                        VALUES (@name);
                    ";
                    using var insertOwnerCmd = new MySqlCommand(insertOwnerSql, connection);
                    insertOwnerCmd.Parameters.Add("@name", MySqlDbType.VarChar);

                    async Task<long> InsertOwner(string name)
                    {
                        insertOwnerCmd.Parameters["@name"].Value = name;

                        await insertOwnerCmd.ExecuteNonQueryAsync();
                        return insertOwnerCmd.LastInsertedId;
                    }

                    var ownerJohnId = await InsertOwner("John");
                    var ownerSarahId = await InsertOwner("Sarah");

                    Console.WriteLine("Sample owners inserted.");

                    // 3.3) Insert sample cats assigned to owners
                    var insertCatSql = @"
                        INSERT INTO `cats` (`Name`, `Is_Cute`, `Color`, `Owner_ID`)
                        VALUES (@name, @cute, @color, @owner);
                    ";
                    using var insertCatCmd = new MySqlCommand(insertCatSql, connection);
                    insertCatCmd.Parameters.Add("@name", MySqlDbType.VarChar);
                    insertCatCmd.Parameters.Add("@cute", MySqlDbType.Byte);
                    insertCatCmd.Parameters.Add("@color", MySqlDbType.VarChar);
                    insertCatCmd.Parameters.Add("@owner", MySqlDbType.Int32);

                    async Task<long> InsertCat(string name, bool cute, string color, long? ownerId = null)
                    {
                        insertCatCmd.Parameters["@name"].Value = name;
                        insertCatCmd.Parameters["@cute"].Value = cute;
                        insertCatCmd.Parameters["@color"].Value = color;
                        insertCatCmd.Parameters["@owner"].Value = ownerId;

                        await insertCatCmd.ExecuteNonQueryAsync();
                        return insertCatCmd.LastInsertedId;
                    }

                    // John owns 2 cats
                    await InsertCat("Pimpi", true, "Brown", ownerJohnId);
                    await InsertCat("Shadow", false, "Black", ownerJohnId);
                    // Sarah owns 1 cat
                    await InsertCat("Luna", true, "White", ownerSarahId);
                    // unowned cat
                    await InsertCat("Lala", true, "Grey");

                    Console.WriteLine("Sample cats inserted.");
                }
                else
                {
                    Console.WriteLine("Database already contains data. Skipping sample insert.");
                }
            }

            Console.WriteLine("\n---\n");

            // 4) SELECT data
            // Basically every query can be processed like this.
            // The only difference is the query sting and the reader.Get<Type>(n).
            // Changes according to the column type in the query.
            using (var cmd = new MySqlCommand(@"
                SELECT *
                FROM `cats`
                LEFT JOIN `owners`
                    ON `cats`.`Owner_ID` = `owners`.`ID`;
            ", connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                Console.WriteLine("_________________________________________________________________________________");
                Console.WriteLine("|C_Id\t|C_Name\t|C_IsCute\t|C_Color\t|C_OwnerId\t|O_Id\t|O_Name\t|");
                Console.WriteLine("+-------+-------+---------------+---------------+---------------+-------+-------+");
                while (await reader.ReadAsync())
                {
                    var cat_id = reader.GetInt32(0);
                    var cat_name = reader.GetString(1);
                    var cat_isCute = reader.GetBoolean(2);
                    var cat_color = reader.GetString(3);

                    if (!reader.IsDBNull(4)) // Check if Foreign Key is not NULL
                    {
                        var cat_ownerId = reader.GetInt32(4);
                        var owner_id = reader.GetInt32(5);
                        var owner_name = reader.GetString(6);

                        Console.WriteLine($"|{cat_id}\t|{cat_name}\t|{cat_isCute}\t\t|{cat_color}\t\t|{cat_ownerId}\t\t|{owner_id}\t|{owner_name}\t|");
                    }
                    else
                    {
                        Console.WriteLine($"|{cat_id}\t|{cat_name}\t|{cat_isCute}\t\t|{cat_color}\t\t|NULL (no owner data available)\t|");
                    }
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

