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

        private User Server;
        //private List<User> UserList;
        private Dictionary<string, string> UsernameIpPort;
        private int ListenPort;
        
        private bool isLogin;
        private TcpListener myListener;

        public Client()
        {
            
            InitializeComponent();
            //随机产生一个连接端口 [1w,6w)
            ListenPort = 10000 + new Random((int)DateTime.Now.Ticks).Next(50000);
            //UserList = new List<User>();
            UsernameIpPort = new Dictionary<string, string>();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            

            //开启服务器连接
            Thread ServerThread;
            ServerThread = new Thread(linktoserver);
            ServerThread.IsBackground = true;
            ServerThread.Start();
            
            //开启本地连接监听
            Thread Listenthread;
            Listenthread = new Thread(ConnectListen);
            Listenthread.IsBackground = true;
            Listenthread.Start();

            button2.Enabled = false;
            button3.Enabled = true;
        }
        private void ConnectListen()
        {
            IPAddress ip = IPAddress.Parse("127.0.0.1");//ip
            
            myListener = new TcpListener(ip, ListenPort);//创建TcpListener实例
            myListener.Start();//start
            textBox1.AppendText("开启监听连接\r\n" + "开启端口:" + ListenPort + "\r\n");

            while (true)
            {
                //监听新客户端连接
                TcpClient client = myListener.AcceptTcpClient();
                //对每一个新客户端，创建新线程处理
                Thread thread = new Thread(new ParameterizedThreadStart(ListenClient));
                thread.IsBackground = true;
                thread.Start(client);
            }
        }
        private void ListenClient(object obj)
        {
            TcpClient client = obj as TcpClient;
            var br = new BinaryReader(client.GetStream());
            textBox1.AppendText(br.ReadString() + "\r\n");
            client.Close();
        }
        private void linktoserver()
        {
            //通过服务器的ip和端口号，创建TcpClient实例
            String ip = textBox3.Text.Split(':')[0];
            int port = int.Parse(textBox3.Text.Split(':')[1]);

            Server = new User(new TcpClient(ip, port));
            //连接成功
            isLogin = true;
            Server.userName = textBox4.Text;
            Server.bw.Write(Server.userName);    //向服务器发送username
            Server.bw.Flush();                  //刷新输出缓存
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
                textBox1.AppendText("接收 " + username + " 用户ip:port " + IpPort);
            }


            while (isLogin)
            {
                try
                {
                    string ope = Server.br.ReadString();
                    if (ope.CompareTo("message")==0)
                    {
                        textBox1.AppendText(Server.br.ReadString() + "\r\n");
                    }
                    if (ope.CompareTo("new guy")==0)
                    {
                        //上线相关操作
                        //接收username
                        string username = Server.br.ReadString();
                        //添加 消息 和 listbox
                        textBox1.AppendText(username + "上线\r\n");
                        listBox1.Items.Add(username);
                        
                        //接收新用户ip port
                        UsernameIpPort[username] = Server.br.ReadString();

                        //输出
                        textBox1.AppendText("收到" + username + "  ip:port  " +
                            UsernameIpPort[username] + "\r\n");
                    }
                    if (ope.CompareTo("del guy")==0)
                    {
                        //离线相关操作
                        string username = Server.br.ReadString();
                        textBox1.AppendText(username + "离线\r\n");
                        listBox1.Items.Remove(username);
                    }
                }
                catch
                {
                    //接收失败
                    break;
                }
            }
            Mainclose();
        }

        private void Client_Load(object sender, EventArgs e)
        {
            Mainclose();
        }

        private void Mainclose()
        {
            isLogin = false;
            //listBox1 = new ListBox();
            if (Server != null)
            {
                Server.bw.Write("close");
                Server.bw.Flush();
                Server.Close();
                
            }
            if (!button2.Enabled)
            {
                button2.Enabled = true;
                button3.Enabled = false;
            }
        }
        private void Button3_Click(object sender, EventArgs e)
        {
            Mainclose();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            //得到选择用户名
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
