namespace Autobot.WpfClient
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Windows.Documents;

    using Autobot.Common;

    public class BotClient
    {
        /// <summary>
        /// Connection to the autobot 
        /// </summary>
        public Socket ClientSocket { get; set; }

        /// <summary>
        /// Menssage data
        /// </summary>
        public byte[] Data { get; set; }


        /// <summary>
        /// Last sese reading
        /// </summary>
        public List<SenseData> SenseData { get; set; }
 
        /// <summary>
        /// Connect to the bot server
        /// </summary>
        /// <param name="ipStr">ip address as string</param>
        public void Connect(string ipStr)
        {
            IPAddress ipAddress = IPAddress.Parse(ipStr);
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Server is listening on port 1000
            var ipEndPoint = new IPEndPoint(ipAddress, 5429);

            //Connect to the server
            ClientSocket.BeginConnect(ipEndPoint, this.OnConnect, null);
        }

        /// <summary>
        /// Send message to the server
        /// </summary>
        /// <param name="message">message to be sent</param>
        private void SendMessage(Message message)
        {
            var bytes = message.ToByte();
            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
        }

        /// <summary>
        /// Connect event
        /// </summary>
        /// <param name="ar">async result</param>
        private void OnConnect(IAsyncResult ar)
        {
            var message = new Message();
            message.Command = MessageType.RemoteControl;
            ClientSocket.EndConnect(ar);
            var bytes = message.ToByte();
            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
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
                    // CarData data = CarData.DeserializeData(msgReceived.Data);
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

        /// <summary>
        /// Send event
        /// </summary>
        /// <param name="ar">async result</param>
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

        public void UpdateSpeed(short speed)
        {
            var message = new Message() { Command = MessageType.Speed, Parameter1 = speed };
            this.SendMessage(message);
        }

        public void UpdateDirection(short direction, short speed)
        {
            var message = new Message { Command = MessageType.Turn, Parameter1 = direction, Parameter2 = speed };
            this.SendMessage(message);
        }

        public void Forward(int rotations = 1, short speed = 80)
        {
            var message = new Message { Command = MessageType.Forward, Parameter1 = rotations, Parameter2 = speed };
            this.SendMessage(message);
        }

        public void Back(int rotations = 1, short speed = 80)
        {
            var message = new Message { Command = MessageType.Back, Parameter1 = rotations, Parameter2 = speed };
            this.SendMessage(message);
        }

        public void Left(int rotations = 1, short speed = 80)
        {
            var message = new Message { Command = MessageType.Left, Parameter1 = rotations, Parameter2 = speed };
            this.SendMessage(message);
        }

        public void UpdateSenseData(int angle = 360)
        {
            var message = new Message { Command = MessageType.Forward, Parameter1 = angle };
            this.SendMessage(message);
        }

        public void Right(int rotations = 1, short speed = 80)
        {
            var message = new Message { Command = MessageType.Forward, Parameter1 = rotations, Parameter2 = speed };
            this.SendMessage(message);
        }
    }
}
