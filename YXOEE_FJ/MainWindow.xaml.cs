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
        private DispatcherTimer ShowTimer;
        private DispatcherTimer ReconnTimer;
        private DispatcherTimer timer;
        private Array strItemIDs;
        private Array lClientHandles;
        private Array lserverhandles;
        private Array lErrors;
        private int TransactionID = 0;
        private int CancelID = 0;
        private int count = 0;
        private List<InterFaceDataFJ> varList = new List<InterFaceDataFJ>();
        private ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private bool firstSave = false;
        private bool stSave = false;
        private bool endSave = false;
        private DateTime trigger = DateTime.Now.Date;
        private OPCServerData serverData = new OPCServerData();
        private static BitmapImage IFalse = new BitmapImage(new Uri("/Static/01.png", UriKind.Relative));
        private static BitmapImage ITrue = new BitmapImage(new Uri("/Static/02.png", UriKind.Relative));

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                LoadJsonData();
                dal = new MainDAL(this.config);
                serverData = dal.GetOPCInfo();
                if (serverData == null)
                    throw new Exception("无OPC服务器配置！");
                varList = dal.GetFJ();
                if (!varList.Any())
                {
                    throw new Exception("无TAG资料配置！");
                }
                count = varList.Count;

                #region 时间定时器
                ShowTimer = new System.Windows.Threading.DispatcherTimer();
                ShowTimer.Tick += new EventHandler(ShowTimer1);
                ShowTimer.Interval = new TimeSpan(0, 0, 0, 1);
                ShowTimer.Start();
                #endregion

                DataList.ItemsSource = null;
                OPCImage.Source = IFalse;
                DataList.ItemsSource = varList;

                OpcServer = new OPCServer();

                if (Init())
                {
                    ReadData();
                }
                else
                {
                    if (MessageBoxX.Show("连接OPC服务错误！", "错误提示") == MessageBoxResult.OK)
                    {
                        this.Close();
                    }
                }

                #region 时间定时器
                ReconnTimer = new System.Windows.Threading.DispatcherTimer();
                ReconnTimer.Tick += new EventHandler(Reconn);
                ReconnTimer.Interval = new TimeSpan(0, 0, 0, 5);
                ReconnTimer.Start();
                #endregion
            }
            catch (Exception ex)
            {
                log.Error("1." + ex.Message);
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
            //OpcGroup.DataChange += AsyncReadData;

            // add items
            string[] tmpIDs = new string[count + 1];
            string[] tmpNames = new string[count + 1];
            int[] tmpCHandles = new int[count + 1];
            for (int i = 1; i<=count; i++)
            {
                tmpCHandles[i] = i;
                tmpIDs[i] = varList[i-1].FTagID;
                tmpNames[i] = varList[i-1].FDataName;
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
                log.Error("2." + e.Message);
            }
        }

        /// <summary>
        /// 断开OPC连接
        /// </summary>
        public void DisConnected()
        {
            if (timer != null && timer.IsEnabled)
            {
                timer.Stop();
                timer = null;
            }

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
                        //OpcGroup.DataChange -= AsyncReadData;
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

                OpcServer.Connect(serverData.OpcServerName, serverData.OpcIp); //连接OPC Server
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
                log.Error("3." + ex.Message);
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

            if (ShowTimer != null && ShowTimer.IsEnabled)
                ShowTimer.Stop();
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
                log.Error("4." + ex.Message);
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
        private void AsyncReadData(int TransactionID, int NumItems, ref Array ClientHandles, ref Array ItemValues, ref Array Qualities, ref Array TimeStamps, ref Array Errors) //, ref Array Errors
        {
            // 数据解析
            for (int i = 0; i < NumItems; i++)
            {
                try
                {
                    object value = ItemValues.GetValue(i + 1);
                    int clientHandle = Convert.ToInt32(ClientHandles.GetValue(i + 1));

                    for (int j = 0; j < this.varList.Count; j++)
                    {
                        if (j + 1 == clientHandle)
                        {
                            this.varList[j].Fvalue = value.ToString();
                            this.varList[j].FQuanlity = Qualities.GetValue(i + 1).ToString();
                            this.varList[j].UpdateTime = TimeStamps.GetValue(i + 1).ToString();

                            dal.UpdateData(varList[j]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("5." + ex.Message);
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

        private void ReadData()
        {
            timer = new DispatcherTimer();
            timer.Tick += (s, ee) =>
            {

                if (OpcServer != null)
                {
                    try
                    {
                        if (OpcServer.ServerState == (int)OPCServerState.OPCRunning)
                        {
                            OpcGroup.AsyncRead(count, lserverhandles, out lErrors, TransactionID, out CancelID);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("6." + ex.Message);
                        log.Error("6." + OpcServer.ServerState);
                    }
                }

            };
            timer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            timer.Start();
        }

        #region 取消
        //private void conn_Click(object sender, RoutedEventArgs e)
        //{
        //    if (Init())
        //    {

        //        timer = new DispatcherTimer();
        //        timer.Tick += (s, ee) =>
        //        {

        //            if (OpcServer != null)
        //            {
        //                try
        //                {
        //                    OpcGroup.AsyncRead(count, lserverhandles, out lErrors, TransactionID, out CancelID);
        //                }
        //                catch (Exception ex)
        //                {
        //                }
        //            }

        //        };
        //        timer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        //        timer.Start();

        //        connBtn.Visibility = Visibility.Hidden;
        //        disBtn.Visibility = Visibility.Visible;
        //        refreshBtn.Visibility = Visibility.Hidden;
        //    }
        //    else
        //    {
        //        if (MessageBoxX.Show("连接OPC服务错误！", "错误提示") == MessageBoxResult.OK)
        //        {
        //            this.Close();
        //        }
        //    }
        //}

        //private void disconn_Click(object sender, RoutedEventArgs e)
        //{
        //    DisConnected();

        //    connBtn.Visibility = Visibility.Visible;
        //    disBtn.Visibility = Visibility.Hidden;
        //    refreshBtn.Visibility = Visibility.Visible;
        //} 
        #endregion

        public void ShowTimer1(object sender, EventArgs e)
        {
            this.TM.Text = " ";
            //获得年月日 
            this.TM.Text += DateTime.Now.ToString("yyyy年MM月dd日");   //yyyy年MM月dd日 
            this.TM.Text += "  ";
            //获得时分秒 
            this.TM.Text += DateTime.Now.ToString("HH:mm:ss");
            this.TM.Text += "  ";
            this.TM.Text += DateTime.Now.ToString("dddd", new System.Globalization.CultureInfo("zh-cn"));
            this.TM.Text += "  ";
        }

        private void ReConn_Click(object sender, RoutedEventArgs e)
        {
            DisConnected();

            if (Init())
            {
                ReadData();
                log.Info("重新连接成功!");
            }
            else
            {
                MessageBoxX.Show("连接OPC服务错误！", "错误提示");
            }
        }

        public void Reconn(object sender, EventArgs e)
        {
            if (OpcServer.ServerState != (int)OPCServerState.OPCRunning)
            {

                DisConnected();

                if (Init())
                {
                    ReadData();
                    log.Info("重新连接成功!");
                }
            }
        }
    }

}
