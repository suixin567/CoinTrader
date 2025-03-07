using Mysqlx.Crud;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinTrader.Common.Database
{
    [SugarTable("workflow")]
    public class Workflow
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)] // 主键&自增
        public int Id { get; set; }
        [SugarColumn(ColumnName = "created_at", IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // 币种
        [SugarColumn(ColumnName = "instrument")]
        public string Instrument { get; set; }

        // 导航属性：一个工作流有多个操作
        [SugarColumn(IsIgnore = true)]
        [Navigate(NavigateType.OneToMany, nameof(Operation.WorkflowId))]
        public List<Operation> Operations { get; set; }

        [SugarColumn(ColumnName = "status")]
        public int Status { get; set; }

        [SugarColumn(ColumnName = "stoped_at", IsNullable = true)]
        public DateTime? EndedAt { get; set; } = null;
    }

    [SugarTable("operation")]
    public class Operation
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)] // 主键&自增
        public int Id { get; set; }

        [SugarColumn(ColumnName = "created_at", IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 工作流外键
        public int WorkflowId { get; set; }

        // 导航属性：一个操作属于一个工作流（多对一）
        [SugarColumn(IsIgnore = true)]
        [Navigate(NavigateType.ManyToOne, nameof(WorkflowId))]
        public Workflow Workflow { get; set; }

        //// 订单Id
        //[SugarColumn(ColumnName = "orderId ")]
        //public long OrderId { get; set; }

        // 操作方向
        [SugarColumn(ColumnName = "side")]
        public int Side { get; set; }

        [SugarColumn(ColumnName = "status")]
        public int Status { get; set; }
    }


    public class MysqlHelper
    {
        SqlSugarClient db;
        public void init()
        {
            db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = "server=localhost;port=3306;database=okx;user=lianzaisss123654;password=lzsss666123654;",
                DbType = DbType.MySql, // 数据库类型
                IsAutoCloseConnection = true, // 自动关闭连接
                InitKeyType = InitKeyType.Attribute // 从实体类读取主键和自增列
            });
            db.CodeFirst.InitTables<Workflow>(); // 自动创建表（如果不存在）
            db.CodeFirst.InitTables<Operation>(); // 自动创建表（如果不存在）
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
        public SqlSugarClient getDB()
        {
            return db;
        }
    }
}
