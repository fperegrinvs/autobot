namespace Autobot.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Data from the distance reading sensor
    /// </summary>
    public class SenseData
    {
        /// <summary>
        /// Size of the binary data
        /// </summary>
        private const short BinarySize = 24;

        /// <summary>
        /// Angle position in the sense data
        /// </summary>
        private const short AngleOffset = 0;

        /// <summary>
        /// Distance position in the sense data
        /// </summary>
        private const short DistanceOffset = 4;

        /// <summary>
        /// PositionX position in the sense data
        /// </summary>
        private const short PositionXOffset = 8;

        /// <summary>
        /// PositionY offset in the sense data
        /// </summary>
        private const short PositionYOffset = 16;

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

        /// <summary>
        /// Convert binary data to a list of SenseData
        /// </summary>
        /// <param name="data">binary data</param>
        /// <returns>list of sense data</returns>
        public static List<SenseData> FromBytes(byte[] data)
        {
            var elements = data.Length / BinarySize;
            var result = new List<SenseData>(elements);

            for (var i = 0; i < elements; i++)
            {
                var startPosition = BinarySize * i;
                var item = new SenseData();
                item.Angle = BitConverter.ToSingle(data, startPosition + AngleOffset);
                item.Distance = BitConverter.ToSingle(data, startPosition + DistanceOffset);
                item.PositionX = BitConverter.ToSingle(data, startPosition + PositionXOffset);
                item.PositionY = BitConverter.ToSingle(data, startPosition + PositionYOffset);
                result.Add(item);
            }

            return result;
        }

        public byte[] ToBytes()
        {
            var result = new byte[24];
            
            var angleBytes = BitConverter.GetBytes(Angle);
            var distanceBytes = BitConverter.GetBytes(Distance);
            var positionXBytes = BitConverter.GetBytes(PositionX);
            var positionYBytes = BitConverter.GetBytes(PositionY);

            Buffer.BlockCopy(angleBytes, 0, result, AngleOffset, angleBytes.Length);
            Buffer.BlockCopy(distanceBytes, 0, result, DistanceOffset, distanceBytes.Length);
            Buffer.BlockCopy(positionXBytes, 0, result, PositionXOffset, positionXBytes.Length);
            Buffer.BlockCopy(positionYBytes, 0, result, PositionYOffset, positionYBytes.Length);

            return result;
        }
    }
}
