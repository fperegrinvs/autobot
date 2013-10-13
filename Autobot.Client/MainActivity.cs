namespace Autobot.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;

    using Android.App;
    using Android.Net.Wifi;
    using Android.OS;
    using Android.Widget;

    using Autobot.Common;

    using Java.Net;

    using Message = Autobot.Common.Message;
    using ProtocolType = System.Net.Sockets.ProtocolType;
    using Socket = System.Net.Sockets.Socket;

    [Activity(Label = "Brick.Client", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        public Socket ClientSocket { get; set; }
        public event EventHandler<StringEventArgs> MessageEvent;

        public byte[] Data { get; set; }

        protected void SendAlert(string text)
        {
            if (MessageEvent != null)
            {
                MessageEvent(this, new StringEventArgs { Message = text });
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            this.SetContentView(Resource.Layout.Main);

            var ipText = this.FindViewById<EditText>(Resource.Id.IpText);
            var ip = GetMyIp();
            ipText.Text = ip.Replace(ip.Split('.').Last(), "");

            // Get our button from the layout resource,
            // and attach an event to it
            var button = this.FindViewById<Button>(Resource.Id.SearchButton);

            button.Click += delegate
            {
                var list = this.FindViewById<Spinner>(Resource.Id.MachineList);
                var machines = ScanSubNet(ip);
                var dataAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, machines);
                dataAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
                list.Adapter = dataAdapter;
            };

            var connect = this.FindViewById<Button>(Resource.Id.ConnectButton);
            connect.Click += delegate
            {
                Connect(ipText.Text.Trim());
            };

            var fwd = this.FindViewById<Button>(Resource.Id.ForwardButton);
            fwd.Click += delegate
            {
                var message = new Message() { Command = MessageType.Forward, Parameter1 = 1 };
                this.SendMessage(message);
            };

            var left = this.FindViewById<Button>(Resource.Id.LeftButton);
            left.Click += delegate
            {
                var message = new Message() { Command = MessageType.Left };
                this.SendMessage(message);
            };

            var right = this.FindViewById<Button>(Resource.Id.RightButton);
            right.Click += delegate
            {
                var message = new Message() { Command = MessageType.Right };
                this.SendMessage(message);
            };

            MessageEvent += (sender, args) => this.RunOnUiThread(() => Toast.MakeText(this, args.Message, ToastLength.Long).Show());

            var spinner = this.FindViewById<Spinner>(Resource.Id.MachineList);
            spinner.ItemSelected += delegate { ipText.Text = spinner.SelectedItem.ToString().Split('-').Last().Trim(); };

        }

        public string GetMyIp()
        {
            var wim = (WifiManager)GetSystemService(WifiService);
            var ip = wim.ConnectionInfo.IpAddress;
            int first = (ip >> 24) & 0xFF;
            int second = (ip >> 16) & 0xFF;
            int third = (ip >> 8) & 0xFF;
            int fourth = ip & 0xFF;
            return string.Format("{3}.{2}.{1}.{0}", first, second, third, fourth);
        }


        /// <summary>
        /// Scan a subnet for connected computers
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        private List<String> ScanSubNet(String ip)
        {
            var subnet = ip.Replace(ip.Split('.').Last(), "");
            StrictMode.SetThreadPolicy(StrictMode.ThreadPolicy.Lax);

            var hosts = new List<String>();

            for (int i = 1; i < 255; i++)
            {
                var testIp = subnet + i;

                if (testIp == ip)
                {
                    continue;
                }

                InetAddress testAddress = InetAddress.GetByName(testIp);

                if (!string.IsNullOrEmpty(testAddress.HostName) && testAddress.HostName != testAddress.HostAddress) //achable(50)))
                {
                    hosts.Add(string.Format("{0} - {1}", testAddress.HostName, testAddress.HostAddress));
                }
            }

            return hosts;
        }

        public void UpdateCarData(CarData data)
        {

        }

        public void Connect(string address)
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse(address);
                ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                //Server is listening on port 1000
                IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, 5429);

                //Connect to the server
                ClientSocket.BeginConnect(ipEndPoint, this.OnConnect, null);
            }
            catch (System.FormatException)
            {
                this.SendAlert("Invalid Address");
            }

        }

        private void SendMessage(Message message)
        {
            var bytes = message.ToByte();
            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
        }

        private void OnConnect(IAsyncResult ar)
        {
            var message = new Common.Message();
            message.Command = MessageType.Hello;
            ClientSocket.EndConnect(ar);
            var bytes = message.ToByte();
            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                ClientSocket.EndSend(ar);
                Data = new byte[1024];
                //Start listening to the data asynchronously
                ClientSocket.BeginReceive(Data,
                                           0,
                                           Data.Length,
                                           SocketFlags.None,
                                           this.OnReceive,
                                           null);
            }
            catch (Exception ex)
            {
                this.SendAlert(ex.Message);
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                ClientSocket.EndReceive(ar);

                var msgReceived = new Message(Data);

                //Accordingly process the message received
                switch (msgReceived.Command)
                {
                    case MessageType.Hello:
                        this.SendAlert("Connected");
                        this.RunOnUiThread(() =>
                        {
                            var switcher = this.FindViewById<ViewSwitcher>(Resource.Id.Switcher);
                            switcher.ShowNext();
                        });
                        break;
                    case MessageType.Info:
                        CarData data = CarData.DeserializeData(msgReceived.Data);
                        this.UpdateCarData(data);
                        break;
                    case MessageType.Ack:
                        break;

                }

                Data = new byte[1024];

                ClientSocket.BeginReceive(Data,
                                          0,
                                          Data.Length,
                                          SocketFlags.None,
                                          this.OnReceive,
                                          null);

            }
            catch (ObjectDisposedException)
            { }
            catch (Exception ex)
            {
                if (MessageEvent != null)
                {
                    MessageEvent(this, new StringEventArgs { Message = ex.Message });
                }
            }

        }
    }
}

