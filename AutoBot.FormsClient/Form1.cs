using System;
using System.Windows.Forms;

namespace AutoBot.FormsClient
{
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using Autobot.Common;

    using MjpegProcessor;

    using SlimDX.XInput;

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            MjpegDecoder mjpeg = new MjpegDecoder();
            mjpeg.FrameReady += mjpeg_FrameReady;
            mjpeg.Error += mjpeg_Error;
            mjpeg.ParseStream(new Uri("http://192.168.1.12:8080/videofeed"));
        }

        private void mjpeg_FrameReady(object sender, FrameReadyEventArgs e)
        {
            pictureBox1.Image = e.Bitmap;
        }

        void mjpeg_Error(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.Message);
        }

        GamepadState GamePad { get; set; }

        public Socket ClientSocket
        {
            get;
            set;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            GamePad = new GamepadState(UserIndex.One);
            var thread = new Thread(this.UpdateState);
            thread.Start();

            IPAddress ipAddress = IPAddress.Parse(IpTxb.Text);
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Server is listening on port 1000
            var ipEndPoint = new IPEndPoint(ipAddress, 5429);

            //Connect to the server
            ClientSocket.BeginConnect(ipEndPoint, this.OnConnect, null);

            //this.btnStart_Click(this, e);
        }

        public byte[] Data { get; set; }

        private void OnConnect(IAsyncResult ar)
        {
            var message = new Autobot.Common.Message();
            message.Command = MessageType.RemoteControl;
            ClientSocket.EndConnect(ar);
            var bytes = message.ToByte();
            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
        }

        private void OnSend(IAsyncResult ar)
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

        private void OnReceive(IAsyncResult ar)
        {
            ClientSocket.EndReceive(ar);

            var msgReceived = new Message(Data);

            //Accordingly process the message received
            switch (msgReceived.Command)
            {
                case MessageType.Hello:
                    //this.SendAlert("Connected");
                    //this.RunOnUiThread(() =>
                    //{
                    //    var switcher = this.FindViewById<ViewSwitcher>(Resource.Id.Switcher);
                    //    switcher.ShowNext();
                    //});
                    break;
                case MessageType.Info:
                    CarData data = CarData.DeserializeData(msgReceived.Data);
                    //this.UpdateCarData(data);
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

        private void UpdateState()
        {
            var rt = GamePad.RightTrigger;
            var lt = GamePad.LeftTrigger;
            var ls_x = GamePad.LeftStick.Position.X;
            while (true)
            {
                GamePad.Update();

                if (GamePad.LeftStick.Position.X == 0)
                {
                    if (GamePad.RightTrigger != rt)
                    {
                        rt = GamePad.RightTrigger;
                        this.UpdateSpeed(Convert.ToInt16(Math.Round(rt * 100, 0)));
                    }

                    if (GamePad.LeftTrigger != lt)
                    {
                        lt = GamePad.LeftTrigger;
                        this.UpdateSpeed(Convert.ToInt16(Math.Round(lt * -100, 0)));
                    }
                }
                else
                {
                    lt = GamePad.LeftTrigger;
                    rt = GamePad.RightTrigger;
                    ls_x = GamePad.LeftStick.Position.X;
                    var speed = rt > 0 ? rt : lt * -1;
                    this.UpdateDirection(Convert.ToInt16(Math.Round(ls_x * 100, 0)), Convert.ToInt16(Math.Round(speed * 100, 0)));
                }


                Thread.Sleep(50);
            }
        }

        private void SendMessage(Message message)
        {
            var bytes = message.ToByte();
            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
        }

        private void UpdateSpeed(short speed)
        {
            var message = new Message() { Command = MessageType.Speed, Parameter1 = speed };
            this.SendMessage(message);
        }

        public void UpdateDirection(short direction, short speed)
        {
            var message = new Message() { Command = MessageType.Turn, Parameter1 = direction, Parameter2 = speed};
            this.SendMessage(message);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            GamePad.Update();
        }

        private void FwdButton_Click(object sender, EventArgs e)
        {
            var message = new Message() { Command = MessageType.Forward };
            this.SendMessage(message);
        }

        private void leftBtn_Click(object sender, EventArgs e)
        {
            var message = new Message() { Command = MessageType.Left };
            this.SendMessage(message);
        }

        private void rightBtn_Click(object sender, EventArgs e)
        {
            var message = new Message() { Command = MessageType.Right };
            this.SendMessage(message);
        }

        private void backBtn_Click(object sender, EventArgs e)
        {
            var message = new Message() { Command = MessageType.Back };
            this.SendMessage(message);

        }

        private void senseBtn_Click(object sender, EventArgs e)
        {
            var message = new Message() { Command = MessageType.Sense };
            this.SendMessage(message);
        }

        private void infoBtn_Click(object sender, EventArgs e)
        {
            var message = new Message() { Command = MessageType.Info };
            this.SendMessage(message);
        }
    }
}
