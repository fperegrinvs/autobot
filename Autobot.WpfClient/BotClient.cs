namespace Autobot.WpfClient
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Net;
    using System.Net.Sockets;

    using Autobot.Common;

    public class BotClient
    {
        /// <summary>
        /// Connection to the autobot 
        /// </summary>
        public Socket ClientSocket { get; set; }

        /// <summary>
        /// Last sese reading
        /// </summary>
        public List<SenseData> SenseData { get; set; }

        /// <summary>
        /// one connection per client
        /// </summary>
        private readonly object connectLock = new object();

        /// <summary>
        /// Connect to the bot server
        /// </summary>
        /// <param name="ipStr">ip address as string</param>
        public Socket Connect(string ipStr)
        {
            IPAddress ipAddress = IPAddress.Parse(ipStr);
            
            if (ClientSocket == null)
            {
                ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            
            //Server is listening on port 1000
            var ipEndPoint = new IPEndPoint(ipAddress, 5429);


            lock (connectLock)
            {
                if (!ClientSocket.Connected)
                {
                    //Connect to the server
                    ClientSocket.Connect(ipEndPoint);
                }
            }

            return ClientSocket;
        }

        /// <summary>
        /// Send message to the server
        /// </summary>
        /// <param name="message">message to be sent</param>
        private void SendMessage(Message message)
        {
            var bytes = message.ToByte();

            if (ClientSocket == null || !ClientSocket.Connected)
            {
                this.Connect(ConfigurationManager.AppSettings["ServerAddress"]);

                if (ClientSocket == null || !ClientSocket.Connected)
                {
                    throw new Exception();
                }
            }

            ClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, this.OnSend, null);
        }


        private void OnReceive(IAsyncResult ar)
        {
            ClientSocket.EndReceive(ar);

            var msgReceived = new Message(((Message)ar.AsyncState).Data);

            //Accordingly process the message received
            switch (msgReceived.Command)
            {
                case MessageType.Sense:
                    {
                        SenseData = Common.SenseData.FromBytes(msgReceived.Data);
                        break;
                    }
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
        }

        /// <summary>
        /// Send event
        /// </summary>
        /// <param name="ar">async result</param>
        private void OnSend(IAsyncResult ar)
        {
            ClientSocket.EndSend(ar);

            var msg = new Message { Data = new byte[1024] };

            //Start listening to the data asynchronously
            ClientSocket.BeginReceive(msg.Data,
                                       0,
                                        msg.Data.Length,
                                       SocketFlags.None,
                                       this.OnReceive,
                                       msg);
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
            var message = new Message { Command = MessageType.Sense, Parameter1 = angle };
            this.SendMessage(message);
        }

        public void Right(int rotations = 1, short speed = 80)
        {
            var message = new Message { Command = MessageType.Forward, Parameter1 = rotations, Parameter2 = speed };
            this.SendMessage(message);
        }
    }
}
