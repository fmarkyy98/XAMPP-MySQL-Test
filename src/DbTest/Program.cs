using System;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DbTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await Solution1();
            await Solution2();
        }

        #region MySql.Data based solution

        static async Task Solution1()
        {
            var server = "localhost";
            var database = "my_test_db";
            var user = "root";
            var password = "";

            var connection = new MySqlConnection($"Server={server};Database={database};Uid={user};Pwd={password};");
            await connection.OpenAsync();
            Console.WriteLine("Connected!");

            using var cmd = new MySqlCommand("SELECT * FROM `cats`;", connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var isCute = reader.GetBoolean(2);
                var color = reader.GetString(3);

                Console.WriteLine($"{id}|{name}|{isCute}|{color}");
            }
        }

        #endregion

        #region EntityFramework based solution

        public class Cat
        {
            [Column("Cat_Id")]
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
