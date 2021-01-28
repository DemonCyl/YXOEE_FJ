using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using log4net;
using Newtonsoft.Json;
using OPCAutomation;
using YXOEE_FJ.DAL;
using YXOEE_FJ.Entity;
using Panuon.UI.Silver;
using System.ComponentModel;
using Panuon.UI.Silver.Core;

namespace YXOEE_FJ
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        object syncLock = new object();
        private OPCServer OpcServer;
        private OPCGroup OpcGroup;
        private OPCGroups OpcGroups;
        private OPCItems OpcItems;
        private bool OPCState = false;
        private bool IsConnected = false;
        private MainDAL dal;
        private ConfigData config;
        private DispatcherTimer timer;
        private Array strItemIDs;
        private Array lClientHandles;
        private Array lserverhandles;
        private Array lErrors;
        private int TransactionID = 0;
        private int CancelID = 0;
        private static int count = 4;
        private List<OPCVarList> varList = new List<OPCVarList>();
        private ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private bool firstSave = false;
        private bool stSave = false;
        private bool endSave = false;
        private DateTime trigger = DateTime.Now.Date;
        private string strHostName = null;
        private static BitmapImage IFalse = new BitmapImage(new Uri("/Static/01.png", UriKind.Relative));
        private static BitmapImage ITrue = new BitmapImage(new Uri("/Static/02.png", UriKind.Relative));

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                LoadJsonData();
                dal = new MainDAL(this.config);
                DataList.ItemsSource = null;
                OPCImage.Source = IFalse;
                DataList.ItemsSource = varList;

            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
        }

        /// <summary>
        /// OPC 连接初始化
        /// </summary>
        /// <returns></returns>
        private bool Init()
        {
            bool b = ConnectToServer();
            if (b == false)
                return false;
            bool b2 = CreateGroups();
            if (b2 == false)
                return false;

            OPCImage.Source = ITrue;
            OpcGroup.AsyncReadComplete += AsyncReadData;

            // add items
            string[] tmpIDs = new string[count + 1];
            string[] tmpNames = new string[count + 1];
            #region Tag
            tmpIDs[1] = "D425._modifyCountES";
            tmpIDs[2] = "D425._modifyCountRT";
            tmpIDs[3] = "D425.q_D425_X142_10_spare";
            tmpIDs[4] = "D425.q_D425_X142_10_spare";
            //tmpIDs[5] = "";
            //tmpIDs[6] = "";
            //tmpIDs[7] = "";
            //tmpIDs[8] = "";
            //tmpIDs[9] = "";
            //tmpIDs[10] = "";
            //tmpIDs[11] = "";
            //tmpIDs[12] = "";
            //tmpIDs[13] = "";
            //tmpIDs[14] = "";
            //tmpIDs[15] = "";

            tmpNames[1] = "";
            tmpNames[2] = "";
            tmpNames[3] = "";
            tmpNames[4] = "";
            //tmpNames[5] = "";
            //tmpNames[6] = "";
            //tmpNames[7] = "";
            //tmpNames[8] = "";
            //tmpNames[9] = "";
            //tmpNames[10] = "";
            //tmpNames[11] = "";
            //tmpNames[12] = "";
            //tmpNames[13] = "";
            //tmpNames[14] = "";
            //tmpNames[15] = "";
            #endregion

            varList.Clear();
            int[] tmpCHandles = new int[count + 1];
            for (int i = 1; i <= count; i++)
            {
                tmpCHandles[i] = i;
                varList.Add(new OPCVarList()
                {
                    OPCItemID = tmpIDs[i],
                    OPCItemName = tmpNames[i]
                });
            }

            DataList.ItemsSource = varList;
            DataList.Items.Refresh();

            strItemIDs = (Array)tmpIDs;
            lClientHandles = (Array)tmpCHandles;
            OpcItems.AddItems(count, strItemIDs, lClientHandles, out lserverhandles, out lErrors);

            return true;
        }

        /// <summary>
        /// 本地配置文件读取
        /// </summary>
        private void LoadJsonData()
        {
            try
            {
                string path = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                string file = path + $"config.json";

                using (var sr = File.OpenText(file))
                {
                    string JsonStr = sr.ReadToEnd();
                    config = JsonConvert.DeserializeObject<ConfigData>(JsonStr);
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
        }

        /// <summary>
        /// 断开OPC连接
        /// </summary>
        public void DisConnected()
        {
            if (!IsConnected)
            {
                return;
            }

            //加锁 
            lock (syncLock)
            {
                if (OpcServer != null)
                {
                    try
                    {
                        OpcGroup.AsyncReadComplete -= AsyncReadData;
                        //删组 
                        if (OpcGroup != null)
                            OpcServer.OPCGroups.Remove(OpcGroup.Name);
                    }
                    catch
                    { GC.Collect(); }

                    try
                    {
                        OpcServer.Disconnect();
                    }
                    catch
                    {
                        try
                        {
                            GC.Collect();
                            OpcServer.Disconnect();
                        }
                        catch
                        {
                            GC.Collect();
                        }
                    }
                }
            }

            OpcGroup = null;
            IsConnected = false;
            OPCImage.Source = IFalse;
        }

        /// <summary>
        /// 连接OPC Server
        /// </summary>
        /// <returns></returns>
        public bool ConnectToServer()
        {
            string ServerName = "";

            try
            {
                if (OpcServer != null)
                {
                    try
                    {
                        if (OpcServer.ServerState == (int)OPCServerState.OPCRunning)
                        {
                            OPCState = true;
                            return true;
                        }
                    }
                    catch
                    {
                        OPCState = false;
                    }
                }


                bool isConn = false;
                //OpcServer = new OPCServer();
                //string strHostIP;
                //获取IP地址上最后一个 OPC Server 的名字
                //获取本地计算机IP,计算机名称
                //IPHostEntry IPHost = Dns.GetHostEntry(Environment.MachineName);
                //IPHost.HostName.ToString();
                //if (IPHost.AddressList.Length > 0)
                //{
                //    strHostIP = IPHost.AddressList[0].ToString();
                //}
                //else
                //{
                //    return false;
                //}
                ////通过IP来获取计算机名称，可用在局域网内
                //IPHostEntry ipHostEntry = Dns.GetHostEntry(strHostIP);
                //var strHostName = ipHostEntry.HostName.ToString();


                //object serverList = OpcServer.GetOPCServers(strHostName);
                //if (serverList == null)
                //{
                //    OPCState = false;
                //    return false;
                //}

                //foreach (string turn in (Array)serverList)
                //{
                //    ServerName = turn;
                //log.Info(turn);
                //}
                ////ServerName = "";
                //log.Info(strHostName);

                ServerName = this.opcserverlist.Text;
                OpcServer.Connect(ServerName, strHostName); //连接OPC Server
                if (OpcServer.ServerState == (int)OPCServerState.OPCRunning)
                {
                    isConn = true;
                    IsConnected = true;
                    OPCState = true;
                }
                return isConn;
            }
            catch (Exception ex)
            {
                OPCState = false;
                log.Error(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 窗体关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DisConnected();

            if (timer != null && timer.IsEnabled)
                timer.Stop();
        }

        /// <summary>
        /// 创建组
        /// </summary>
        /// <returns></returns>
        private bool CreateGroups()
        {
            if (OpcGroup != null)
                return true;

            bool isCreate = false;
            try
            {
                OpcGroups = OpcServer.OPCGroups;
                OpcGroup = OpcGroups.Add("OEEGROUP" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                //设置组属性
                OpcServer.OPCGroups.DefaultGroupIsActive = true;
                OpcServer.OPCGroups.DefaultGroupDeadband = 0;
                OpcGroup.IsActive = true;
                OpcGroup.IsSubscribed = true;
                OpcGroup.UpdateRate = 250;

                OpcItems = OpcGroup.OPCItems;

                isCreate = true;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                isCreate = false;
            }
            return isCreate;
        }

        /// <summary>
        /// 异步读取回调方法
        /// </summary>
        /// <param name="TransactionID"></param>
        /// <param name="NumItems"></param>
        /// <param name="ClientHandles"></param>
        /// <param name="ItemValues"></param>
        /// <param name="Qualities"></param>
        /// <param name="TimeStamps"></param>
        /// <param name="Errors"></param>
        private void AsyncReadData(int TransactionID, int NumItems, ref Array ClientHandles, ref Array ItemValues, ref Array Qualities, ref Array TimeStamps, ref Array Errors)
        {
            // 数据解析
            for (int i = 0; i < NumItems; i++)
            {
                object value = ItemValues.GetValue(i + 1);
                int clientHandle = Convert.ToInt32(ClientHandles.GetValue(i + 1));

                for (int j = 0; j < this.varList.Count; j++)
                {
                    if (j + 1 == clientHandle)
                    {
                        this.varList[j].OPCValue = value.ToString();
                        this.varList[j].Quanlity = Qualities.GetValue(i + 1).ToString();
                        this.varList[j].UpdateTime = TimeStamps.GetValue(i + 1).ToString();
                    }
                }
            }

            DataList.ItemsSource = varList;
            DataList.Items.Refresh();
            

            // 时间点存储数据
            if (!firstSave)
            {

                firstSave = true;
            }

            DateTime now = DateTime.Now;
            if (now.Date > trigger.Date)
            {
                stSave = false;
                endSave = false;
                trigger.AddDays(1);
            }

            if (!stSave)
            {
                // 早上 00:05:00
                var mTime = trigger;
                mTime = mTime.AddMinutes(5);
                if (now.Day == mTime.Day && now.Hour == mTime.Hour && now.Minute == mTime.Minute)
                {

                    stSave = true;
                }
            }

            if (!endSave)
            {
                // 晚上 23:55:00
                var eTime = trigger;
                eTime = eTime.AddHours(23);
                eTime = eTime.AddMinutes(55);
                if (now.Day == eTime.Day && now.Hour == eTime.Hour && now.Minute == eTime.Minute)
                {

                    endSave = true;
                }
            }

        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            clientlist.Items.Clear();
            opcserverlist.Items.Clear();

            //获取客户端名称和IP
            GetLocaIp();
            //获取OPC服务器
            GetLocalOPCServer();
            //初始化客户端名和OPC服务器名的选择
            this.clientlist.SelectedIndex = 0;
            this.opcserverlist.SelectedIndex = 0;

            
        }

        private void conn_Click(object sender, RoutedEventArgs e)
        {
            if (Init())
            {

                timer = new DispatcherTimer();
                timer.Tick += (s, ee) =>
                {

                    if (OpcServer != null)
                    {
                        try
                        {
                            OpcGroup.AsyncRead(count, lserverhandles, out lErrors, TransactionID, out CancelID);
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                };
                timer.Interval = new TimeSpan(0, 0, 0, 5);
                timer.Start();

                connBtn.Visibility = Visibility.Hidden;
                disBtn.Visibility = Visibility.Visible;
                refreshBtn.Visibility = Visibility.Hidden;
            }
            else
            {
                if (MessageBoxX.Show("连接OPC服务错误！", "错误提示") == MessageBoxResult.OK)
                {
                    this.Close();
                }
            }
        }

        private void disconn_Click(object sender, RoutedEventArgs e)
        {
            DisConnected();

            connBtn.Visibility = Visibility.Visible;
            disBtn.Visibility = Visibility.Hidden;
            refreshBtn.Visibility = Visibility.Visible;
        }

        private void GetLocaIp()
        {
            //获取本地计算机名
            strHostName = Dns.GetHostName();
            this.clientlist.Items.Add(strHostName);

            //获取本机IP地址
            try
            {
                IPHostEntry ipHostEntry = Dns.GetHostEntry(strHostName);
                for (int i = 0; i < ipHostEntry.AddressList.Length; i++)
                {
                    //从IP地址列表中筛选出IPv4类型的IP地址
                    //AddressFamily.InterNetwork表示此IP为IPv4,
                    //AddressFamily.InterNetworkV6表示此地址为IPv6类型
                    if (ipHostEntry.AddressList[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        this.clientlist.Items.Add(ipHostEntry.AddressList[i].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取本地IP出错:" + ex.Message);
            }

            //添加空字符串作为客户端名实现与OPC服务器的连接
            if (!this.clientlist.Items.Contains(""))
            {
                this.clientlist.Items.Add("");
            }
        }

        private void GetLocalOPCServer()
        {
            try
            {
                OpcServer = new OPCServer();
                object opcServerList = OpcServer.GetOPCServers(strHostName);
                foreach (string server in (Array)opcServerList)
                {
                    this.opcserverlist.Items.Add(server);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取OPC服务器出错：" + ex.Message);
            }
        }
    }

    public class OPCVarList
    {
        /// <summary>
        /// 变量ID
        /// </summary>
        [DisplayName("变量ID")]
        [IgnoreColumn]
        public string OPCItemID { get; set; }

        /// <summary>
        /// 变量名称
        /// </summary>
        [DisplayName("变量名称")]
        [ColumnWidth("350")]
        public string OPCItemName { get; set; }
        /// <summary>
        /// 值
        /// </summary>
        [DisplayName("变量值")]
        [ColumnWidth("200")]
        public string OPCValue { get; set; }
        /// <summary>
        /// 通信质量
        /// </summary>
        [DisplayName("通信质量")]
        [ColumnWidth("60")]
        public string Quanlity { get; set; }
        /// <summary>
        /// 更新时间
        /// </summary>
        [DisplayName("更新时间")]
        [ColumnWidth("*")]
        public string UpdateTime { get; set; }
    }
}
