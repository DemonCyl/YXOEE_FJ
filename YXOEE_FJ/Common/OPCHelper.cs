using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OPCAutomation;

namespace YXOEE_FJ.OPC
{
    public class OPCHelper
    {
        object syncLock = new object();

        private OPCServer OpcServer;
        private OPCGroup OpcGroup, OpcWriteGroup;
        private OPCGroups OpcGroups;
        private OPCItems OpcItems;
        private OPCItem OpcItem;
        private bool OPCState = false;

        /// <summary>
        /// OPC连接状态
        /// </summary>
        bool IsConnected = false;
        /// <summary>
        /// 客户端句柄
        /// </summary>
        int ItmHandleClient = 0;
        /// <summary>
        /// 服务端句柄
        /// </summary>
        int ItmHandleServer = 0;
        string IpAddr;
        //public string[] OpcValues = null; 
        int index;

        public void Dispose()
        {
            DisConnected();
        }

        public OPCHelper(string opcAddr)
        {
            this.IpAddr = opcAddr;
            bool b = ConnectToServer();
            if (b == false)
                return;
            bool b2 = CreateGroups();
            if (b2 == false)
                return;
        }

        string ServerName = "";
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
                OpcServer = new OPCServer();
                //获取IP地址上最后一个 OPC Server 的名字

                object serverList = OpcServer.GetOPCServers(IpAddr);
                if (serverList == null)
                {
                    OPCState = false;
                    return false;
                }

                foreach (string turn in (Array)serverList)
                {
                    ServerName = turn;
                }

                OpcServer.Connect(ServerName, IpAddr); //连接OPC Server
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
                return false;
            }
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
                isCreate = false;
            }
            return isCreate;
        }

        /// <summary>
        /// OPC取数
        /// </summary>
        /// <param name="opcTags"></param>
        /// <returns></returns>
        public string[] GetOpcValues(List<string> opcTags)
        {
            bool b = ConnectToServer();
            if (b == false)
                return null;
            CreateGroups();
            index = 0;
            int badValue = 0;
            string[] OpcValue = new string[opcTags.Count];

            string strTemp = ""; //临时使用
            foreach (string str in opcTags)
            {
                string strs = GetOpcValueOne(str);
                //如果测点加不进去,数量超过10个,说明接口机又取不到数了 
                if (strs.Equals("BAD"))
                {
                    badValue += 1;
                    if (badValue > 10)
                    {
                        OPCState = false;
                        return null;
                    }
                }
                OpcValue[index] = strs;
                strTemp += str + "，值：" + strs + "\r\n";
                index++;
            }
            return OpcValue;
        }

        /// <summary>
        /// 同步取数/一个一个读取
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string GetOpcValueOne(string str)
        {
            Hashtable ReadHS = new Hashtable();
            int iItemIndex = 1;

            OPCItem item;

            int IndexItem = 0;
            try
            {
                try
                {
                    if (ReadHS.ContainsKey(str))
                        item = OpcGroup.OPCItems.GetOPCItem(Convert.ToInt32(ReadHS[str]));
                    else
                    {
                        iItemIndex += 1;
                        item = OpcGroup.OPCItems.AddItem(str, iItemIndex);
                        ReadHS.Add(str, item.ServerHandle);
                    }
                }
                catch
                {
                    iItemIndex += 1;
                    try
                    {
                        item = OpcGroup.OPCItems.AddItem(str, iItemIndex);
                    }
                    catch
                    {
                        return "BAD";
                    }
                }

                Object value;
                Object quality;
                Object timestamp;
                //直接从设备上取数
                item.Read((short)OPCDataSource.OPCDevice, out value, out quality, out timestamp);

                return value.ToString();
            }
            catch (Exception ex)
            {
                return "";
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
                        //删组 
                        if (OpcGroup != null)
                            OpcServer.OPCGroups.Remove(OpcGroup.Name);
                        if (OpcWriteGroup != null)
                            OpcServer.OPCGroups.Remove(OpcWriteGroup.Name);
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
            IsConnected = false;
        }
    }

}

