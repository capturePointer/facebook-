﻿using System;
using System.Collections.Generic;
using System.Linq;
using Xzy.Sockets;
using Xzy.EmbeddedApp.Utils;
using Xzy.EmbeddedApp.WinForm.Socket.Model;
using Wx.Qunkong360.Wpf.Implementation;
using Xzy.EmbeddedApp.WinForm.Tasks;
using System.Collections.Concurrent;
using Wx.Qunkong360.Wpf.Utils;
using Wx.Qunkong360.Wpf.Events;

namespace Xzy.EmbeddedApp.WinForm.Socket
{
    public class SocketServer
    {
        /// <summary>
        /// 存储连接池对象
        /// </summary>
        public static ConcurrentDictionary<string, TcpSocketSession> AllConnectionKey = new ConcurrentDictionary<string, TcpSocketSession>();

        private static TcpSocketServer _server;

        /// <summary>
        /// 开启socket服务
        /// </summary>
        public static void Init()
        {
            StartServer();

            //添加心跳定时器
            var timer = new System.Timers.Timer();
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Interval = int.Parse(System.Configuration.ConfigurationManager.AppSettings["HeartbeatTime"].ToString()) * 60 * 1000;//
        }
        private static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {           
            //遍历连接池，发送心跳信息
            if (!AllConnectionKey.IsNull() && AllConnectionKey.Count() > 0)
            {
                foreach (var item in AllConnectionKey)
                {
                    if (!item.Value.IsNull())
                    {
                        SendHeartbeat(item);
                    }
                }
            }
        }

        /// <summary>
        /// 发送心跳包
        /// </summary>
        /// <param name="collection">发送的客户端信息</param>
        private static void SendHeartbeat(KeyValuePair<string, TcpSocketSession> collection)
        {
            try
            {
                string jsonMsg2 = "{" + $"\"action\":{(int)SocketCase.Heartbeat},\"tasknum\":0,\"context\":" + "{" + "\"tasktype\":0, \"txtmsg\":\"\", \"list\":[]" + "}}";

                SocketUtils.SendJson(collection.Value, jsonMsg2);
                SocketUtils.Send<String>(collection.Value, "\r\n");
                Console.WriteLine("发送了一个心跳包;key:" + collection.Key + " : " + jsonMsg2);
            }
            catch (Exception e)
            {
                //异常，表示客户端断线，移除客户端信息
                TcpSocketSession tcpSocketSession;
                AllConnectionKey.TryRemove(collection.Key, out tcpSocketSession);
            }
        }

        /// <summary>
        /// 发送任务指令
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="typeid"></param>
        /// <param name = "context" ></ param >
        public static int SendTaskInstruct(int key, int typeid, int tasknum, string context)
        {
            int nflag = -1;
            try
            {
                if (!AllConnectionKey.IsNull() && AllConnectionKey.Count() > 0)
                {
                    foreach (var item in AllConnectionKey)
                    {
                        if (!item.Value.IsNull() && item.Value.MobileIndex == key)
                        {
                            string jsonMsg2 = "{" + $"\"action\":{(int)SocketCase.ServerMsg},\"tasknum\":{tasknum},\"context\":{context}" + "}";
                            SocketUtils.SendJson(item.Value, jsonMsg2);
                            //发送结束包
                            SocketUtils.Send<String>(item.Value, "\r\n");
                            nflag = 1;
                            Console.WriteLine("发送了一个指令包;key:" + item.Key);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                //异常，表示客户端断线，移除客户端信息
                //AllConnectionKey.Remove(collection.Key);
            }
            return nflag;
        }


        /// <summary>
        /// 获得服务对象
        /// </summary>
        /// <returns></returns>
        public static TcpSocketServer GetServer()
        {
            return _server;
        }

        /// <summary>
        /// 开启服务端监听
        /// </summary>
        private static void StartServer()
        {
            var config = new TcpSocketServerConfiguration();

            #region 配置信息

            //config.UseSsl = true;
            //config.SslServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.pfx", "Cowboy");
            //config.SslPolicyErrorsBypassed = false;

            //config.FrameBuilder = new FixedLengthFrameBuilder(20000);
            //config.FrameBuilder = new RawBufferFrameBuilder();
            //config.FrameBuilder = new LineBasedFrameBuilder();
            //config.FrameBuilder = new LengthPrefixedFrameBuilder();
            //config.FrameBuilder = new LengthFieldBasedFrameBuilder();

            #endregion 配置信息

            _server = new TcpSocketServer(int.Parse(System.Configuration.ConfigurationManager.AppSettings["SocketServerHost"].ToString()), config);
            _server.ClientConnected += server_ClientConnected;
            _server.ClientDisconnected += server_ClientDisconnected;
            _server.ClientDataReceived += server_ClientDataReceived;            
            _server.Listen();
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public static void StopServer()
        {
            try
            {
                foreach (var session in AllConnectionKey)
                {
                    _server.CloseSession(session.Value.SessionKey);
                }
                _server.Shutdown();
            }
            catch (Exception ex)
            {
                LogUtils.Error($"{ex}");
            }
        }

        /// <summary>
        /// 连接成功后消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void server_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            //加入到连接池中
            Console.WriteLine("连接成功;key:"+e.Session.SessionKey);        
            
            AllConnectionKey.TryAdd(e.Session.SessionKey,e.Session);

            LogUtils.Information("连接成功;key:" + e.Session.SessionKey);
            //writeLogs("连接成功;key:"+ e.Session.SessionKey);
        }

        //static void writeLogs(string result)
        // {
        //     try
        //     {
        //         foreach (var win in System.Windows.Forms.Application.OpenForms)
        //         {
        //             if (win is MainForm)
        //             {
        //                 MainForm mainForm = win as MainForm;

        //                 mainForm.BeginInvoke(new Action(() =>
        //                 {
        //                     mainForm.txt_socketlogs.Text += result + "\r\n";

        //                 }));
        //             }
        //         }
        //     }
        //     catch(Exception ex)
        //     {

        //     }
        // }

        /// <summary>
        /// 关闭连接消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void server_ClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {
            try
            {
                TcpSocketSession tcpSocketSession;
                AllConnectionKey.TryRemove(AllConnectionKey.Where(p => p.Value.SessionKey == e.Session.SessionKey).First().Key, out tcpSocketSession);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 接收客户端发送的消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void server_ClientDataReceived(object sender, TcpClientDataReceivedEventArgs e)
        {            
            SockMsgJson actMsg = JsonBufHelper.BytesToObject<SockMsgJson>(e.Data, e.DataOffset, e.DataLength);

            if (actMsg == null)
            {
                return;
            }

            TasksSchedule tasksch = new TasksSchedule();
            Console.WriteLine("收到模拟器消息……");
            Console.WriteLine(actMsg.action + ":"+ actMsg.tasknum + ":" + actMsg.context);
            switch (actMsg.action) {
                case SocketCase.Init:
                    string imei = actMsg.context;
                    int mobileIndex = VmManager.Instance.VmModels.Values.FirstOrDefault(vm => vm.Imei == imei).Index;
                    AllConnectionKey.FirstOrDefault(p => p.Value.SessionKey == e.Session.SessionKey).Value.MobileIndex= mobileIndex;
                    LogUtils.Information(string.Format("模拟器{0}绑定成功，IMEI{1}:", mobileIndex, imei));
                    //writeLogs(string.Format("模拟器{0}绑定成功，IMEI{1}:", mobileIndex, imei));

                    EventAggregatorManager.Instance.EventAggregator.GetEvent<NewSocketConnectedEvent>().Publish(mobileIndex);

                    break;
                case SocketCase.ClientTask: //更新任务状态
                    int tasknum = actMsg.tasknum;
                    string status = actMsg.context; //任务状态
                    if(status != "")
                    {                        
                        tasksch.UpdateTaskResval(tasknum, status);
                    }
                    break;
                case SocketCase.ClientPhone:    //更新手机号码状态
                    int phone = actMsg.tasknum;
                    int respone = Int32.Parse(actMsg.context); //响应状态
                    tasksch.UpdatePhoneStatus(phone, respone);
                    break;
                case SocketCase.ClientInitPhone:

                    string[] datas = actMsg.context.Split(new char[] { '|' });
                    string phoneImei = datas[0];
                    string phoneNum = datas[1];

                    var vmModel = VmManager.Instance.VmModels.Values.FirstOrDefault(vm => vm.Imei == phoneImei);
                    if (vmModel != null)
                    {
                        vmModel.PhoneNumber = phoneNum;
                    }

                    break;

                case SocketCase.SimulationTask:

                    SimulationTaskManager.Invoke(actMsg.tasknum, bool.Parse(actMsg.context));


                    break;
            }
        }
    }
}
