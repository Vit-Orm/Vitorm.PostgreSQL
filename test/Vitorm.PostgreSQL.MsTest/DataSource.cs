﻿using Vit.Core.Util.ConfigurationManager;

using Vitorm.Sql;

namespace Vitorm.MsTest
{
    [System.ComponentModel.DataAnnotations.Schema.Table("User")]
    public class User
    {
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.Column("userId")]
        public int id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("userName")]
        public string name { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("userBirth")]
        public DateTime? birth { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.Column("userFatherId")]
        public int? fatherId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.Column("userMotherId")]
        public int? motherId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.Column("userClassId")]
        public int? classId { get; set; }

        public static User NewUser(int id, bool forAdd = false) => new User { id = id, name = "testUser" + id };

        public static List<User> NewUsers(int startId, int count = 1, bool forAdd = false)
        {
            return Enumerable.Range(startId, count).Select(id => NewUser(id, forAdd)).ToList();
        }
    }

    public class UserClass
    {
        [System.ComponentModel.DataAnnotations.Key]
        public int id { get; set; }

        public string name { get; set; }

        public static List<UserClass> NewClasses(int startId, int count = 1)
        {
            return Enumerable.Range(startId, count).Select(id => new UserClass { id = id, name = "class" + id }).ToList();
        }
    }


    public class DataSource
    {
        public static void WaitForUpdate() { }

        readonly static string connectionString = Appsettings.json.GetStringByPath("Vitorm.PostgreSQL.connectionString");


        public static SqlDbContext CreateDbContextForWriting(bool autoInit = true) => CreateDbContext(autoInit);

        public static SqlDbContext CreateDbContext(bool autoInit = true)
        {
            var dbContext = new SqlDbContext();
            dbContext.UsePostgreSQL(connectionString);
            if (autoInit) InitDbContext(dbContext);
            return dbContext;
        }

        static void InitDbContext(SqlDbContext dbContext)
        {
            #region #1 init User
            {
                dbContext.TryDropTable<User>();
                dbContext.TryCreateTable<User>();

                var users = new List<User> {
                    new User { id=1,  name="u146", fatherId=4, motherId=6 },
                    new User { id=2,  name="u246", fatherId=4, motherId=6 },
                    new User { id=3,  name="u356", fatherId=5, motherId=6 },
                    new User { id=4,  name="u400" },
                    new User { id=5,  name="u500" },
                    new User { id=6,  name="u600" },
                };

                users.ForEach(user =>
                {
                    user.birth = DateTime.Parse("2021-01-01 00:00:00").AddHours(user.id);
                    user.classId = user.id % 2 + 1;
                });

                dbContext.AddRange(users);
            }
            #endregion

            #region #2 init Class
            {
                dbContext.TryDropTable<UserClass>();
                dbContext.TryCreateTable<UserClass>();
                dbContext.AddRange(UserClass.NewClasses(1, 6));
            }
            #endregion

            WaitForUpdate();
        }

    }
}
