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

namespace Server
{
    public partial class Server : Form
    {

        private TcpListener myListener;
        private List<User> UserList;
        private Dictionary<string, string> UsernameIpPort;

        public Server()
        {
            InitializeComponent();
            UserList = new List<User>();
            UsernameIpPort = new Dictionary<string, string>();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            Thread myThread = new Thread(ConnectListen);
            myThread.IsBackground = true;
            myThread.Start();
            button2.Enabled = false;
        }

        private void ConnectListen()
        {
            IPAddress ip = IPAddress.Parse("127.0.0.1");//服务器端ip
            myListener = new TcpListener(ip,int.Parse(textBox3.Text));//创建TcpListener实例
            myListener.Start();//start
            textBox1.AppendText("服务端开启\r\n" + "开启端口:" + textBox3.Text + "\r\n");

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
            //创建新user对象
            User user = new User(client);
            
            //读取新用户id
            String receive = user.br.ReadString();

            //检测id是否被使用过
            foreach (var i in UserList)
                if (i.userName == receive)
                {
                    user.bw.Write("name conflict");
                    user.bw.Flush();
                    user.Close();
                    return;
                }
            user.bw.Write("ok");
            user.bw.Flush();


            //添加user进入userlist
            UserList.Add(user);
            user.userName = receive;
            textBox1.AppendText(user.userName + "连接成功\r\n");
            //listBox1添加用户
            listBox1.Items.Add(user.userName);


            
            //远程客户端ip与port
            string ip = ((IPEndPoint)user.client.Client.RemoteEndPoint)
                .Address.ToString();

            //接收新用户本地监听端口
            int Port = int.Parse(user.br.ReadString());
            
            //添加新用户ip和port
            UsernameIpPort[user.userName] = ip + ":" + Port;
            //输出ip和port
            textBox1.AppendText(user.userName + "  ip是" + ip + " 监听端口是" + Port + "\r\n");


            //将新用户ip和port转发给其他登录用户
            foreach(User other in UserList)
                if (other != user)
                {
                    other.bw.Write("new guy");
                    other.bw.Write(user.userName);
                    other.bw.Write(ip + ":" + Port);
                    other.bw.Flush();
                    textBox1.AppendText("向" + other.userName + " 发送 " + user.userName + " ip和port\r\n");
                }

            //向新用户发送已登录用户信息
            user.bw.Write(UserList.Count() - 1 + "");
            user.bw.Flush();
            foreach(var other in UsernameIpPort)
                if (other.Key != user.userName)
                {
                    user.bw.Write(other.Key);
                    user.bw.Write(other.Value);
                    user.bw.Flush();
                    textBox1.AppendText("向" + user.userName + " 发送 " + other.Key + " ip和port\r\n");
                }


            //循环监听客户端消息
            while (true)
            {
                try
                {
                    //读取客户端发来消息
                    string opr = user.br.ReadString();

                    if (opr.CompareTo("message") == 0)
                    {
                        //发送消息给服务器
                        textBox1.AppendText(user.userName + " : " + 
                            user.br.ReadString() + "\r\n");
                    }
                    if (opr.CompareTo("close") == 0)
                        //退出登录
                        break;

                }
                catch
                {
                    //消息接收异常，断开连接
                    break;
                }
            }

            //将退出消息告知给其他登录用户
            foreach (User other in UserList)
                if (other != user)
                {
                    other.bw.Write("del guy");
                    other.bw.Write(user.userName);
                    other.bw.Flush();
                }

            //从listbox删除对应用户
            listBox1.Items.Remove(user.userName);
            //UserList删除
            UserList.Remove(user);
            UsernameIpPort.Remove(user.userName);
            //输出
            textBox1.AppendText(user.userName + "退出登录\r\n");
            //断开连接
            user.Close();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            //得到选择用户名
            if (listBox1.SelectedItem == null)
            {
                MessageBox.Show("请选择接收人");
                return;
            }
            string username = listBox1.SelectedItem.ToString();

            string ip = UsernameIpPort[username].Split(':')[0];
            int port = int.Parse(UsernameIpPort[username].Split(':')[1]);
            TcpClient client = new TcpClient(ip, port);
            var bw = new BinaryWriter(client.GetStream());
            bw.Write("Server : " + textBox2.Text);
            bw.Flush();
            client.Close();
        }
    }
}