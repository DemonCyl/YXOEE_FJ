using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YXOEE_FJ.Entity;

namespace YXOEE_FJ.Common
{
    public class SQLHelper
    {

        private string connStr = null;
        private ConfigData configData;

        public SQLHelper(ConfigData data)
        {
            this.configData = data;

            this.connStr = new StringBuilder("server=" + configData.DataIpAddress +
            ";database=" + configData.DataBaseName + "; uid=" + configData.Uid + ";pwd=" + configData.Pwd + "").ToString();
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(connStr);
        }

    }
}
