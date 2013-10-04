namespace Autobot.Server
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    using Autobot.Common;

    using global::Android.Widget;

    public partial class MainActivity
    {
        private Socket serverSocket = null;
        private byte[] byteData = new byte[1024];

        public void OpenTcp()
        {
            try
            {
                //We are using TCP sockets
                this.serverSocket = new Socket(AddressFamily.InterNetwork,
                                           SocketType.Stream,
                                           ProtocolType.Tcp);

                //Assign the any IP of the machine and listen on port number 5429
                var ipEndPoint = new IPEndPoint(IPAddress.Any, 5429);

                //Bind and listen on the given address
                this.serverSocket.Bind(ipEndPoint);
                this.serverSocket.Listen(4);

                //Accept the incoming clients
                this.serverSocket.BeginAccept(this.OnAccept, null);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("@ ListenForClient" + ex.Message);
                Toast.MakeText(this, "ListenForClient:" + ex.Message, ToastLength.Long).Show();
            }
        }

        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = this.serverSocket.EndAccept(ar);

                //Start listening for more clients
                this.serverSocket.BeginAccept(this.OnAccept, null);

                //Once the client connects then start 
                //receiving the commands from her
                clientSocket.BeginReceive(this.byteData, 0,
                    this.byteData.Length, SocketFlags.None,
                    this.OnReceive, clientSocket);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "SGSserverTCP:" + ex.Message, ToastLength.Long).Show();
            }
        }


        /// <summary>
        /// Receive message
        /// </summary>
        /// <param name="ar"></param>
        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                var clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndReceive(ar);

                var msgReceived = new Message(this.byteData);

                //We will send this object in response the users request
                var msgToSend = new Message();

                //If the message is to login, logout, or simple text message
                //then when send to others the type of the message remains the same
                msgToSend.Command = msgReceived.Command;

                // process the message and get the response
                switch (msgReceived.Command)
                {
                    case MessageType.Forward:
                        Bot.Forward(msgReceived.Parameter1);
                        msgToSend.Command = MessageType.Ack;
                        break;
                    case MessageType.Back:
                        Bot.Back(msgReceived.Parameter1);
                        msgToSend.Command = MessageType.Ack;
                        break;
                    case MessageType.Center:
                        Bot.Center();
                        msgToSend.Command = MessageType.Ack;
                        break;
                    case MessageType.Left:
                        Bot.Left();
                        msgToSend.Command = MessageType.Ack;
                        break;
                    case MessageType.Right:
                        Bot.Right();
                        msgToSend.Command = MessageType.Ack;
                        break;
                    case MessageType.Sense:
                        var result = Bot.Sense();
                        msgToSend.Data = new byte[result.Count * 4];
                        for (var i = 0; i < result.Count; i++)
                        {
                            var senseData = BitConverter.GetBytes(result[i]);
                            Buffer.BlockCopy(senseData, 0, msgToSend.Data, 4 * i, 4);
                        }
                        break;
                    case MessageType.Info:
                        msgToSend.Data = Bot.Data.SerializeData();
                        break;
                }

                var message = msgToSend.ToByte();

                //Send the name of the users in the chat room
                clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, this.OnSend, clientSocket);

				
				//Once the client connects then start 
				//receiving the commands from her
				clientSocket.BeginReceive(this.byteData, 0,
				                          this.byteData.Length, SocketFlags.None,
				                          this.OnReceive, clientSocket);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Error Receiving Message:" + ex.Message, ToastLength.Long).Show();
            }
        }

        /// <summary>
        /// Send the response message
        /// </summary>
        /// <param name="ar">result</param>
        public void OnSend(IAsyncResult ar)
        {
            try
            {
                var client = (Socket)ar.AsyncState;
                client.EndSend(ar);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Error Sending Message:" + ex.Message, ToastLength.Long).Show();
            }
        }
    }
}