namespace Autobot.Server
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    using Autobot.Common;

    using global::Android.Widget;

    public partial class MainActivity
    {
        private Socket serverSocket;
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
                Console.WriteLine("@ ListenForClient" + ex.Message);
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
                    case MessageType.Speed:
                        Bot.Speed(msgReceived.Parameter1);
                        break;
                    case MessageType.Turn:
                        Bot.Turn(msgReceived.Parameter1, msgReceived.Parameter2);
                        break;
                    case MessageType.CorrectPosition:
                        Bot.Data.PosX += msgReceived.Parameter1;
                        Bot.Data.PosY += msgReceived.Parameter2;
                        break;
                    case MessageType.Forward:
                        {
                            var distance = msgReceived.Parameter1;
                            var power = Convert.ToSByte(msgReceived.Parameter2);
                            if (distance == 0)
                            {
                                distance = 1;
                            }

                            if (power == 0)
                            {
                                power = 20;
                            }

                            Bot.Forward(distance, power);
                            msgToSend.Command = MessageType.Ack;
                            break;
                        }
                    case MessageType.Back:
                        {
                            var distance = msgReceived.Parameter1;
                            var power = Convert.ToSByte(msgReceived.Parameter2);
                            if (distance == 0)
                            {
                                distance = 1;
                            }

                            if (power == 0)
                            {
                                power = 20;
                            }

                            Bot.Back(distance, power);
                            msgToSend.Command = MessageType.Ack;
                            break;
                        }
                    case MessageType.Left:
                        {
                            var distance = msgReceived.Parameter1 > 0 ? msgReceived.Parameter1 / 100 : 1;
                            var power = msgReceived.Parameter2 != 0 ? msgReceived.Parameter2 : 80;
                            Bot.Left(distance, Convert.ToSByte(power));
                            msgToSend.Command = MessageType.Ack;
                            break;
                        }
                    case MessageType.Right:
                        {
                            var distance = msgReceived.Parameter1 > 0 ? msgReceived.Parameter1 / 100 : 1;
                            var power = msgReceived.Parameter2 != 0 ? msgReceived.Parameter2 : 80;
                            Bot.Right(distance, Convert.ToSByte(power));
                            msgToSend.Command = MessageType.Ack;
                            break;
                        }
                    case MessageType.Sense:
                        if (msgReceived.Parameter1 == 0)
                        {
                            msgReceived.Parameter1 = 360;
                        }

                        var result = Bot.Sense(msgReceived.Parameter1);
                        msgToSend.Data = new byte[result.Count * 24];
                        for (var i = 0; i < result.Count; i++)
                        {
                            var data = result[i].ToBytes();

                            Buffer.BlockCopy(data, 0, msgToSend.Data, 24 * i, 24);
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