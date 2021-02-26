using Panuon.UI.Silver.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YXOEE_FJ.Entity
{
    public class InterFaceDataFJ
    {
        /// <summary>
        /// 变量ID
        /// </summary>
        [DisplayName("变量ID")]
        [IgnoreColumn]
        public string FTagID { get; set; }

        /// <summary>
        /// 变量名称
        /// </summary>
        [DisplayName("变量名称")]
        [ColumnWidth("300")]
        public string FDataName { get; set; }
        /// <summary>
        /// 值
        /// </summary>
        [DisplayName("数据")]
        [ColumnWidth("150")]
        public string Fvalue { get; set; }
        /// <summary>
        /// 通信质量
        /// </summary>
        [DisplayName("通信质量")]
        [ColumnWidth("100")]
        public string FQuanlity { get; set; }
        /// <summary>
        /// 更新时间
        /// </summary>
        [DisplayName("更新时间")]
        [ColumnWidth("*")]
        public string UpdateTime { get; set; }
    }
}
