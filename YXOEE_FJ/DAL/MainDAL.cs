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

        public List<InterFaceDataFJ> GetFJ()
        {
            string sql = @"select * from t_InterfaceDataFJ ";

            using (var conn = new SQLHelper(config).GetConnection())
            {
                return conn.Query<InterFaceDataFJ>(sql).ToList();
            }
        }
        // 存储OEE 数据
        public void UpdateData(InterFaceDataFJ model)
        {
            string sql = @"update t_InterfaceDataFJ set Fvalue = @Fvalue,FQuanlity = @FQuanlity,FNewBillTime = GETDATE() where FTagID = @FTagID";

            using (var conn = new SQLHelper(config).GetConnection())
            {
                conn.Execute(sql, model);
            }
        }

    }
}
