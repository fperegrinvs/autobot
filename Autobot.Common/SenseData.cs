namespace Autobot.Common
{
    using System;

    /// <summary>
    /// Data from the distance reading sensor
    /// </summary>
    public class SenseData
    {
        /// <summary>
        /// Direction angle
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// Measured distance
        /// </summary>
        public float Distance { get; set; }

        /// <summary>
        /// Position in the X Axis
        /// </summary>
        public double PositionX { get; set; }

        /// <summary>
        /// Position in the Y Axis
        /// </summary>
        public double PositionY { get; set; }

        public byte[] ToBytes()
        {
            var result = new byte[24];
            
            var angleBytes = BitConverter.GetBytes(Angle);
            var distanceBytes = BitConverter.GetBytes(Distance);
            var positionXBytes = BitConverter.GetBytes(PositionX);
            var positionYBytes = BitConverter.GetBytes(PositionY);

            Buffer.BlockCopy(angleBytes, 0, result, 0, 4);
            Buffer.BlockCopy(distanceBytes, 0, result, 4, 4);
            Buffer.BlockCopy(positionXBytes, 0, result, 8, 4);
            Buffer.BlockCopy(positionYBytes, 0, result, 16, 4);

            return result;
        }
    }
}
