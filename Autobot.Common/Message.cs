namespace Autobot.Common
{
    using System;
    using System.Collections.Generic;

    //The data structure by which the server and the client interact with 
    //each other
    public class Message
    {
        /// <summary>
        /// Comand argument
        /// </summary>
        public int Parameter1 { get; set; }

        /// <summary>
        /// Comand argument
        /// </summary>
        public int Parameter2 { get; set; }

        /// <summary>
        /// Command type
        /// </summary>
        public MessageType Command { get; set; }

        /// <summary>
        /// Aditional data
        /// </summary>
        public byte[] Data { get; set; }

        //Default constructor
        public Message()
        {
            this.Command = MessageType.Null;
            this.Parameter1 = 0;
            this.Parameter2 = 0;
            this.Data = new byte[0];
        }

        //Converts the bytes into an object of type Data
        public Message(byte[] data)
        {
            //The first four bytes are for the Command
            this.Command = (MessageType)BitConverter.ToInt32(data, 0);

            //The next four are the command parameter
            this.Parameter1 = BitConverter.ToInt32(data, 4);

            this.Parameter2 = BitConverter.ToInt32(data, 8);

            //The remaining bytes are the aditional data
            var len = data.Length - 12;
            this.Data = new byte[len];
            Buffer.BlockCopy(data, 12, this.Data, 0, len);
        }

        //Converts the Data structure into an array of bytes
        public byte[] ToByte()
        {
            List<byte> result = new List<byte>(12 + this.Data.Length);

            //First four are for the Command
            result.AddRange(BitConverter.GetBytes((int)this.Command));

            //Add the parameter
            result.AddRange(BitConverter.GetBytes(this.Parameter1));

            result.AddRange(BitConverter.GetBytes(this.Parameter2));

            // data
            result.AddRange(this.Data);

            return result.ToArray();
        }
    }
}