namespace Autotob.Brick.EV3
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Android.Bluetooth;

    using Autotob.Brick;

    /// <summary>
    /// Bluetooth connection for use on Android
    /// </summary>
    public class DroidBlueTooth<TBrickCommand, TBrickReply> : Connection<TBrickCommand, TBrickReply>
        where TBrickCommand : BrickCommand
        where TBrickReply : BrickReply, new()
    {
        private BluetoothSocket comPort = null;
        string port;

        /// <summary>
        /// Initializes a new instance of the Bluetooth class.
        /// </summary>
        /// <param name="comport">he serial port to use</param>
        public DroidBlueTooth(string comport)
        {
            this.port = comport;
            this.isConnected = false;
        }

        /// <summary>
        /// Send a command
        /// </summary>
        /// <param name='command'>
        /// The command to send
        /// </param>
        public override void Send(TBrickCommand command)
        {
            byte[] data = null;
            ushort length = (ushort)command.Length;
            data = new byte[length + 2];
            data[0] = (byte)(length & 0x00ff);
            data[1] = (byte)((length & 0xff00) >> 2);
            Array.Copy(command.Data, 0, data, 2, command.Length);
            this.CommandWasSend(command);
            try
            {
                Console.WriteLine("Begin sending command");
                this.comPort.OutputStream.BeginWrite(data, 0, data.Length, new AsyncCallback(delegate { }), State.Connected);
                Console.WriteLine("End Write");
                // comPort.OutputStream.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                throw new ConnectionException(ConnectionError.WriteError, e);
            }

        }

        /// <summary>
        /// Receive a reply
        /// </summary>
        public override TBrickReply Receive()
        {
            byte[] data = new byte[2];
            byte[] payload;
            int expectedlength = 0;
            int replyLength = 0;
            try
            {
                expectedlength = 2;
                replyLength = this.comPort.InputStream.Read(data, 0, 2);
                expectedlength = (ushort)(0x0000 | data[0] | (data[1] << 2));
                payload = new byte[expectedlength];
                replyLength = 0;
                replyLength = this.comPort.InputStream.Read(payload, 0, expectedlength);
            }
            catch (TimeoutException tEx)
            {
                if (replyLength == 0)
                {
                    throw new ConnectionException(ConnectionError.NoReply, tEx);
                }
                else if (replyLength != expectedlength)
                {
                    throw new MonoBrick.EV3.BrickException(MonoBrick.EV3.BrickError.WrongNumberOfBytes, tEx);
                }
                throw new ConnectionException(ConnectionError.ReadError, tEx);
            }
            catch (Exception e)
            {
                throw new ConnectionException(ConnectionError.ReadError, e);
            }
            TBrickReply reply = new TBrickReply();
            reply.SetData(payload);
            this.ReplyWasReceived(reply);
            return reply;
        }

        /// <summary>
        /// Open connection
        /// </summary>
        public override void Open()
        {
            try
            {

                BluetoothAdapter bth = BluetoothAdapter.DefaultAdapter;

                // If the adapter is null, then Bluetooth is not supported
                if (bth == null)
                {
                    throw new ConnectionException(ConnectionError.OpenError, new Exception("Bluetooth is not available"));
                }

                if (!bth.IsEnabled)
                {
                    bth.Enable();
                }

                ICollection<BluetoothDevice> bthD = bth.BondedDevices;

                Java.Util.UUID uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

                foreach (BluetoothDevice d in bthD)
                {
                    if (d.Name == this.port)
                    {
                        // success
                        this.comPort = d.CreateRfcommSocketToServiceRecord(uuid);
                    }
                }

                if (this.comPort == null)
                {
                    throw new Exception("Device not found");
                }

            }
            catch (Exception ex)
            {
                throw new ConnectionException(ConnectionError.OpenError, ex);
            }

            Console.Write("Connecting to device");
            this.comPort.Connect();
            this.isConnected = true;
            this.ConnectionWasOpened();
            Thread.Sleep(1000);
        }

        /// <summary>
        /// Close connection
        /// </summary>
        public override void Close()
        {
            try
            {
                this.comPort.Close();
            }
            catch (Exception)
            {
            }
            this.isConnected = false;
            this.ConnectionWasClosed();
        }
    }
}

