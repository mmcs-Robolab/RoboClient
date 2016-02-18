using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Newtonsoft.Json.Linq;

namespace AsincClient
{
    public partial class Form1 : Form
    {
        //  Буфер для данных
        public static byte[] sendBuffer = new byte[1024];
        public static byte[] receiveBuffer = new byte[1024];

        public static ManualResetEvent ConnectDone = new ManualResetEvent(false);
        public static ManualResetEvent SendDone = new ManualResetEvent(false);
        public static ManualResetEvent ReceiveDone = new ManualResetEvent(false);

        public static IPHostEntry ipHost;
        public static IPAddress ipAddr;
        public static IPEndPoint endPoint;
        public static Socket sClient;

        //  Флажок установленного соединения
        public static bool connected = false;
        public static string Response;
        public static int BytesRead;

        public string pointName;
        public string userName;
        public int userID;
        public int selfID;
        private List<Device> deviceList;
        public Form1()
        {
            InitializeComponent();
        }

        public void ConnectionEstablished()
        {
            //connected = true;
            button2.Enabled = true;
            button1.Enabled = false;
            //  тут начать слушание сервера пока не отключились
            sClient.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, 0, new AsyncCallback(ReceiveCallback), 0);

            JObject userInfo = new JObject();
            userInfo.Add("userID", userID);
            userInfo.Add("selfID", selfID);
            userInfo.Add("name", pointName);
            if (deviceList != null)
                userInfo.Add("deviceList", JToken.FromObject(deviceList));
            else
                userInfo.Add("deviceList", "");

            sendBuffer = Encoding.UTF8.GetBytes("setUserInfo" + "#" + userInfo.ToString() + Environment.NewLine);
            sClient.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), sClient);

        }

        public void DataSend()
        {
            logText.AppendText("\nДанные отправлены\n");
            //  тут начать слушание сервера пока не отключились
        }

        // когда данные от сервера получены. ПЛОХАЯ РЕАЛИЗАЦИЯ С ГЛОБАЛЬНОЙ ПЕРЕМЕННОЙ, ПОТОМ ИЗМЕНИТЬ. 
        public void DataReceived()
        {
            //logText.Text += Response;
            string []cmdArr = Response.Split(' ');

            switch (cmdArr[0])
            {
                case "1":
                    processCommand(int.Parse(cmdArr[1]), cmdArr[2]);
                    break;
            }

        }

        // парсинг команды и действия робота
        private void processCommand(int deviceID, string command)
        {
            switch (command)
            {
                case "goForward":
                    logText.AppendText("\nРобот с id = " + deviceID + " едет прямо\n");
                    break;
                case "goLeft":
                    logText.AppendText("\nРобот с id = " + deviceID + " едет влево\n");
                    break;
                case "goRight":
                    logText.AppendText("\nРобот с id = " + deviceID + " едет вправо\n");
                    break;
                case "goBack":
                    logText.AppendText("\nРобот с id = " + deviceID + " едет назад\n");
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ipHost = Dns.Resolve(ipText.Text);//("192.168.0.174");
            ipAddr = ipHost.AddressList[0];
            endPoint = new IPEndPoint(ipAddr, System.Int32.Parse(portText.Text));
            pointName = pointNameText.Text;

            sClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sClient.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), sClient);
        }

        public /*static*/ void ConnectCallback(IAsyncResult ar)
        {
            //  Получаем сокет
            Socket sClient = (Socket)ar.AsyncState;
            sClient.EndConnect(ar);
            ConnectDone.Set();
            connected = true;
            //  Обновляем состояние формы и вообще
            this.Invoke((MethodInvoker)delegate { ConnectionEstablished(); });
        }

        public /*static*/ void SendCallback(IAsyncResult ar)
        {
            this.Invoke((MethodInvoker)delegate { DataSend(); });
        }
                    

        public /*static*/ void ReceiveCallback(IAsyncResult ar)
        {
            BytesRead = sClient.EndReceive(ar);
            if (BytesRead > 0)
            {
                Response = Encoding.UTF8.GetString(receiveBuffer, 0, BytesRead);
                this.Invoke((MethodInvoker)delegate { DataReceived(); });
                sClient.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, 0, new AsyncCallback(ReceiveCallback), 0);
            }
            else
            {
                ReceiveDone.Set();
            }
        }


        private void button2_Click_1(object sender, EventArgs e)
        {
            if (!connected)
            {
                button2.Enabled = false;
                return;
            }
            sendBuffer = Encoding.UTF8.GetBytes(nameText.Text + " : " + msgField.Text+Environment.NewLine);
            sClient.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), sClient);
            //button2.Select();
        }

        // получение списка подключенных устройств
        // заменить хардкод на реальный опрос состояния роботов
        private List<Device> getConnectedDevices()
        {
            Device dev1 = new Device();
            dev1.id = 0;
            dev1.name = "Robot1";

            List<Device> list = new List<Device>();
            list.Add(dev1);

            return list;
        }

        // авторизация на robolab, получение данных о пользователе
        private void authBtn_Click(object sender, EventArgs e)
        {
            string name = nameText.Text;
            string pass = passText.Text;

            string URLAuth = "http://192.168.0.125:3000/auth"; //"http://195.208.237.193:3000/auth" - это боевой сервер, потом заменить на него. 
            string postString = string.Format("login={0}&pass={1}", name, pass);

            const string contentType = "application/x-www-form-urlencoded";
            System.Net.ServicePointManager.Expect100Continue = false;

            CookieContainer cookies = new CookieContainer();
            HttpWebRequest webRequest = WebRequest.Create(URLAuth) as HttpWebRequest;
            webRequest.Method = "POST";
            webRequest.ContentType = contentType;
            webRequest.CookieContainer = cookies;
            webRequest.ContentLength = postString.Length;
            webRequest.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.9.0.1) Gecko/2008070208 Firefox/3.0.1";
            webRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            webRequest.Referer = "195.208.237.193:3000";

            StreamWriter requestWriter = new StreamWriter(webRequest.GetRequestStream());
            requestWriter.Write(postString);
            requestWriter.Close();

            try
            {
                using(WebResponse response = webRequest.GetResponse())
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://192.168.0.125:3000/auth/userInfo"); // и тут тоже
                    request.CookieContainer = cookies;
                    HttpWebResponse responseUserInfo = (HttpWebResponse)request.GetResponse();
                    if (responseUserInfo.StatusCode == HttpStatusCode.Forbidden)
                    {
                        logText.AppendText("\nНевозможно получить данные пользователя\n");
                    }
                    else if (responseUserInfo.StatusCode == HttpStatusCode.OK)
                    {
                        var encoding = Encoding.UTF8;
                        using (var reader = new System.IO.StreamReader(responseUserInfo.GetResponseStream(), encoding))
                        {
                            string responseText = reader.ReadToEnd();

                            JObject json = JObject.Parse(responseText);
                            userID = (int)json.GetValue("userId");
                            userName = (string)json.GetValue("username");

                            Random rand = new Random();
                            selfID = rand.Next(10, 99999);

                            deviceList = getConnectedDevices();
                        }

                        responseUserInfo.Close();
                        logText.AppendText("\nВы вошли как: " + userName + "\n");
                        serverPanel.Enabled = true;

                        authBtn.Enabled = false;
                        passText.Enabled = false;
                        passLabel.Enabled = false;
                        nameText.Enabled = false;
                        nameLabel.Enabled = false;
                    }


                }

            }
            catch(WebException exp)
            {
                using (WebResponse response = exp.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    switch((int)httpResponse.StatusCode)
                    {
                        case 401:
                            logText.AppendText("\nНеверный логин или пароль\n");
                            break;
                    }

                }
            }

        }

    }
}
