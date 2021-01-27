using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YXOEE_FJ.Entity;

namespace YXOEE_FJ.DAL
{
    public class MainDAL
    {
        private ConfigData config;
        private ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainDAL(ConfigData data)
        {
            this.config = data;
        }

        // 存储OEE 数据


    }
}
