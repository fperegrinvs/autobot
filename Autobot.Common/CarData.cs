using System;

namespace Autobot.Common
{
    /// <summary>
    /// The car
    /// </summary>
    public class CarData
    {
        /// <summary>
        /// Lock to avoid command override
        /// </summary>
        public object MoveLock = new object();

        /// <summary>
        /// Tells if the vehicle is moving
        /// </summary>
        public bool IsMoving { get; set; }

        /// <summary>
        /// Current direction
        /// </summary>
        public float Direction { get; set; }

        /// <summary>
        /// Posição na coordenada X
        /// </summary>
        public double PosX { get; set; }

        /// <summary>
        ///  Posição na coordenada Y
        /// </summary>
        public double PosY { get; set; }

        /// <summary>
        /// Distancia entre-eixos do carro
        /// </summary>
        public double Length = 16;

        /// <summary>
        /// Angulo da roda em relação ao eixo principal do carro
        /// </summary>
        public double WheelAngle = 0;

        /// <summary>
        /// compirmento do pneu (1 rotação)
        /// </summary>
        public double TyreLegth = Math.PI * 3.1;

        /// <summary>
        /// Serializes the data
        /// </summary>
        /// <returns>serialized data</returns>
        public byte[] SerializeData()
        {
            var bytes = new byte[28];
            var direction = BitConverter.GetBytes(Direction);
            var posX = BitConverter.GetBytes(PosX);
            var posY = BitConverter.GetBytes(PosY);
            var wheel = BitConverter.GetBytes(WheelAngle);

            Buffer.BlockCopy(direction, 0, bytes, 0, 4);
            Buffer.BlockCopy(posX, 0, bytes, 4, 8);
            Buffer.BlockCopy(posY, 0, bytes, 12, 8);
            Buffer.BlockCopy(wheel, 0, bytes, 20, 8);
            return bytes;
        }

        /// <summary>
        /// Reads car data from binary data
        /// </summary>
        /// <param name="binaryData">the binary data</param>
        /// <returns>A car data instance</returns>
        public static CarData DeserializeData(byte[] binaryData)
        {
            var result = new CarData();
            result.Direction = BitConverter.ToSingle(binaryData, 0);
            result.PosX = BitConverter.ToDouble(binaryData, 4);
            result.PosY = BitConverter.ToDouble(binaryData, 12);
            result.WheelAngle = BitConverter.ToDouble(binaryData, 20);
            return result;
        }
    }
}