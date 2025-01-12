﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Vitorm.MsTest.CommonTest
{

    [TestClass]
    public class Transaction_Test
    {

        [TestMethod]
        public void Test_Transaction()
        {
            #region Transaction
            {
                using var dbContext = DataSource.CreateDbContext();
                var userSet = dbContext.DbSet<User>();

                Assert.AreEqual("u400", userSet.Get(4).name);

                dbContext.Update(new User { id = 4, name = "u4001" });
                Assert.AreEqual("u4001", userSet.Get(4).name);

                using (var tran = dbContext.BeginTransaction())
                {
                    dbContext.Update(new User { id = 4, name = "u4002" });
                    Assert.AreEqual("u4002", userSet.Get(4).name);
                }
                Assert.AreEqual("u4001", userSet.Get(4).name);

                using (var tran = dbContext.BeginTransaction())
                {
                    dbContext.Update(new User { id = 4, name = "u4002" });
                    Assert.AreEqual("u4002", userSet.Get(4).name);
                    tran.Rollback();
                }
                Assert.AreEqual("u4001", userSet.Get(4).name);

                using (var tran = dbContext.BeginTransaction())
                {
                    dbContext.Update(new User { id = 4, name = "u4003" });
                    Assert.AreEqual("u4003", userSet.Get(4).name);
                    tran.Commit();
                }
                Assert.AreEqual("u4003", userSet.Get(4).name);

            }
            #endregion
        }


        [TestMethod]
        public void Test_Dispose()
        {
            {
                using var dbContext = DataSource.CreateDbContext();
                var userSet = dbContext.DbSet<User>();

                var tran2 = dbContext.BeginTransaction();
                {
                    dbContext.Update(new User { id = 4, name = "u4002" });
                    Assert.AreEqual("u4002", userSet.Get(4).name);
                    tran2.Commit();
                }

                Assert.AreEqual("u4002", userSet.Get(4).name);

                var tran3 = dbContext.BeginTransaction();
                {
                    dbContext.Update(new User { id = 4, name = "u4003" });
                    Assert.AreEqual("u4003", userSet.Get(4).name);
                }
                Assert.AreEqual("u4003", userSet.Get(4).name);
            }

            {
                using var dbContext = DataSource.CreateDbContext(autoInit: false);
                var userSet = dbContext.DbSet<User>();

                Assert.AreEqual("u4002", userSet.Get(4).name);
            }

        }






    }
}
