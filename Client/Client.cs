using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class Client : Form
    {

        private User Server;                                    //服务器
        private Dictionary<string, string> UsernameIpPort;      //key:<id>  value:<ip:port>
        private int ListenPort;                                 //本地监听连入端口
        
        private bool isLogin;                                   //是否连入服务器
        private TcpListener myListener;                         //本地监听"Serversocket"

        public Client()
        {
            InitializeComponent();
            //随机产生一个连接端口 [1w,6w)
            ListenPort = 10000 + new Random((int)DateTime.Now.Ticks).Next(50000);
            UsernameIpPort = new Dictionary<string, string>();
        }

        private void Button2_Click(object sender, EventArgs e)
        {

            button2.Enabled = false;
            button3.Enabled = true;
            isLogin = true;

            //开启本地连接监听
            Thread Listenthread;
            Listenthread = new Thread(ConnectListen);
            Listenthread.IsBackground = true;
            Listenthread.Start();


            //开启服务器连接
            Thread ServerThread;
            ServerThread = new Thread(linktoserver);
            ServerThread.IsBackground = true;
            ServerThread.Start();

        }
        private void ConnectListen()
        {
            /**
             * 本地监听，一直循环，
             * 如果有人连入，开启新线程ListenClient，接收数据退出。
             * 如果myListener断开，则触发异常，break
             * 只需要让isLogin和myListener.Stop()同时修改
             * while循环条件就可以改为true
             */
            IPAddress ip = IPAddress.Parse("127.0.0.1");//ip
            
            myListener = new TcpListener(ip, ListenPort);//创建TcpListener实例
            myListener.Start();//start
            textBox1.AppendText("开启监听连接\r\n" + "开启端口:" + ListenPort + "\r\n");

            while (isLogin)
            {
                //监听新客户端连接
                try {
                    TcpClient client = myListener.AcceptTcpClient();
                    //对每一个新客户端，创建新线程处理
                    Thread thread = new Thread(new ParameterizedThreadStart(ListenClient));
                    thread.IsBackground = true;
                    thread.Start(client);
                }
                catch (Exception e)
                {
                    break;
                }
                
            }
            
        }
        private void ListenClient(object obj)
        {
            /**
             * 接收一条消息并退出
             * 有一点浪费，可以考虑把tcp连接保存下来，
             * 如果以后发现之前连过，直接通信即可
             * 一个dictionary即可
             */
            TcpClient client = obj as TcpClient;
            var br = new BinaryReader(client.GetStream());
            textBox1.AppendText(br.ReadString() + "\r\n");
            client.Close();
        }
        private void linktoserver()
        {
            /**
             * 与服务器通信的主连接
             * 登录需要考虑的事情
             * 1.服务器是否打开
             * 2.向服务器发送本人信息
             *   包括id、ip、port（用于其他人连入）
             *   判断id是否被使用
             * 3.接收已登录的用户信息，比如
             *   包含id、ip、port（用于连入他人）
             * 4.本地listbox、UsernameIpPort加载
             * 
             * 进入循环一直监听服务器消息直到isLogin = false
             */


            //通过服务器的ip和端口号，创建TcpClient实例
            String ip = textBox3.Text.Split(':')[0];
            int port = int.Parse(textBox3.Text.Split(':')[1]);

            try
            {
                Server = new User(new TcpClient(ip, port));
            } catch (Exception e)
            {
                Mainclose();
                MessageBox.Show("服务器未打开");
                return;
            }

            
            Server.userName = textBox4.Text;
            Server.bw.Write(Server.userName);    //向服务器发送username
            Server.bw.Flush();                  //刷新输出缓存

            if (Server.br.ReadString() == "name conflict")
            {
                MessageBox.Show("id已被使用，请更换id");
                Mainclose();
                return;
            }

            //连接成功
            textBox1.AppendText("与服务器连接成功\r\n");
            listBox1.Items.Add("Server");


            //向服务器发送本地监听端口
            Server.bw.Write(ListenPort+"");    //向服务器发送ListenPort
            Server.bw.Flush();                  //刷新输出缓存

            //向服务器接收已登录用户信息
            int number = int.Parse(Server.br.ReadString());

            //输出
            textBox1.AppendText("待接收 " + number + " 用户信息\r\n");
            while (number-- > 0)
            {
                string username = Server.br.ReadString();
                string IpPort = Server.br.ReadString();
                UsernameIpPort[username] = IpPort;
                listBox1.Items.Add(username);
                textBox1.AppendText("接收 " + username + " 用户ip:port " + IpPort + "\r\n");
            }


            while (isLogin)
            {
                try
                {
                    //接收操作码，跳转到不同方法
                    string ope = Server.br.ReadString();
                    if (ope.CompareTo("message") == 0)
                        newMessage();
                    if (ope.CompareTo("new guy")==0)
                        addGuy();
                    if (ope.CompareTo("del guy")==0)
                        deleteGuy();
                }
                catch
                {
                    //接收失败
                    MessageBox.Show("与服务器断开连接");
                    break;
                }
            }
            
            //关闭所有
            Mainclose();
            
        }
        private void deleteGuy()
        {
            /**
             * 下线相关操作
             * 1. 接收username
             * 2. textbox输出离线消息
             * 3. listbox删除
             * 4. UsernameIpPort删除
             */
            string username = Server.br.ReadString();
            textBox1.AppendText(username + "离线\r\n");
            listBox1.Items.Remove(username);
            UsernameIpPort.Remove(username);
        }
        private void addGuy()
        {
            /**
             * 上线相关操作
             * 1. 接收username
             * 2. 添加listbox 
             * 3. 添加UsernameIpPort(接收新用户ip port)
             * 4. textbox输出
             */
            string username = Server.br.ReadString();
            textBox1.AppendText(username + "上线\r\n");
            listBox1.Items.Add(username);
            UsernameIpPort[username] = Server.br.ReadString();
            textBox1.AppendText("收到" + username + "  ip:port  " +
                UsernameIpPort[username] + "\r\n");
        }

        private void newMessage()
        {
            /**
             * 接收新消息，直接输出
             */
            textBox1.AppendText(Server.br.ReadString() + "\r\n");
        }

        private void Mainclose()
        {
            /**
             * 关闭所有
             * 1. isLogin = false
             * 2. myListener关闭
             * 3. textbox清空
             * 4. listbox清空
             * 5. 向Server发送close操作码
             * 6. 登录、注销按钮切换
             * 
             */

            isLogin = false;
            myListener.Stop();
            textBox1.Text = "";
            textBox2.Text = "";
            listBox1.Items.Clear();
            try
            {
                Server.bw.Write("close");
                Server.bw.Flush();
                Server.Close();
                Server = null;
            }
            catch (Exception e) { }
            button2.Enabled = true;
            button3.Enabled = false;
        }
        private void Button3_Click(object sender, EventArgs e)
        {
            Mainclose();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            /**
             * 发送消息
             * 1. 判断是否登录
             * 2. 判断是否选择用户
             * 3. 如果是向服务器发送消息
             *      直接发送
             * 4. 如果是向客户端发送消息，
             *      先取出ip、port，再建立tcp连接，最后发送
             *      记得关闭tcp连接
             */


            //未登录
            if (!isLogin)
            {
                MessageBox.Show("请登录");
                return;
            }
            //得到选择用户名
            if (listBox1.SelectedItem == null) {
                MessageBox.Show("请选择接收人");
                return;
            }
            string username = listBox1.SelectedItem.ToString();
            
            if (username == "Server")
            {
                //向Server发送消息
                Server.bw.Write("message");
                Server.bw.Write(textBox2.Text);
                Server.bw.Flush();
            } else
            {
                //得到ip与port
                string ip = UsernameIpPort[username].Split(':')[0];
                int port = int.Parse(UsernameIpPort[username].Split(':')[1]);
                
                //新建连接
                TcpClient client = new TcpClient(ip, port);
                //输出并关闭连接
                var bw = new BinaryWriter(client.GetStream());
                bw.Write(textBox4.Text + " : " + textBox2.Text);
                bw.Flush();
                client.Close();
            }
        }
    }
}
