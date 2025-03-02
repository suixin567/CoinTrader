using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinTrader.Common.Database
{
    [SugarTable("users")] // 对应数据库表
    public class User
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)] // 主键&自增
        public int Id { get; set; }

        [SugarColumn(ColumnName = "username")]
        public string Name { get; set; }

        public int Age { get; set; }
    }

    public class MysqlHelper
    {
        public void init()
        {
            var db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = "server=localhost;port=3306;database=okx;user=lianzaisss123654;password=lzsss666123654;",
                DbType = DbType.MySql, // 数据库类型
                IsAutoCloseConnection = true, // 自动关闭连接
                InitKeyType = InitKeyType.Attribute // 从实体类读取主键和自增列
            });

            db.CodeFirst.InitTables<User>(); // 自动创建表（如果不存在）
            db.Insertable(new User { Name = "张三", Age = 25 }).ExecuteCommand();
        }

        private static MysqlHelper _instance = null;
        public static MysqlHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MysqlHelper();

                return _instance;
            }
        }
    }
}
