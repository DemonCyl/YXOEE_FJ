using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YXOEE_FJ.Entity;

namespace YXOEE_FJ.DAL
{
    using Common;
    using Dapper;

    public class MainDAL
    {
        private ConfigData config;
        private ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainDAL(ConfigData data)
        {
            this.config = data;
        }

        //
        public OPCServerData GetOPCInfo()
        {
            string sql = @"select * from t_OpcServer where Status = 1";

            using (var conn = new SQLHelper(config).GetConnection())
            {
                return conn.QuerySingleOrDefault<OPCServerData>(sql);
            }
        }

        // 存储OEE 数据


    }
}
