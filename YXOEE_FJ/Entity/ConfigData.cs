using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YXOEE_FJ.Entity
{
    public class ConfigData
    {
        #region 数据库配置
        public string DataIpAddress { get; set; }

        public string DataBaseName { get; set; }

        public string Uid { get; set; }

        public string Pwd { get; set; }
        #endregion

        #region OPC服务器IP
        public string OpcIp { get; set; }
        #endregion
    }
}
