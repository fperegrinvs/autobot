namespace Autobot.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Android.Locations;

    using Autobot.Common;

    using Autotob.Brick.EV3;

    public static class TruckExtensions
    {
        public static void Center(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3)
        {
            var pos = ev3.MotorA.GetTachoCount();
            if (pos == 0)
            {
                return;
            }

            if (pos < 0)
            {
                ev3.MotorA.On(10, 100, true);
            }
            else if (pos > 0)
            {
                ev3.MotorA.On(-10, 100, true);
            }

            ev3.MotorA.ResetTacho();
        }

        static void WaitForMotorToStop(this Motor motor)
        {
            while (motor.IsRunning()) { Thread.Sleep(50); }
        }

        public static void Speed(
            this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, int speed)
        {
            //if (speed > 0)
            //{
            ev3.Vehicle.Forward(Convert.ToSByte(speed));
            //}
            //else
            //{
            //    ev3.Vehicle.Backward(Convert.ToSByte(speed * -1));
            //}
        }

        public static void Turn(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, int turn, int speed)
        {
            ev3.Vehicle.TurnRightForward(Convert.ToSByte(speed), Convert.ToSByte(turn));

            //if (speed > 0)
            //{
            //    if (turn < 0)
            //    {
            //    }
            //}
            //byte power = 30;
            //var current = ev3.MotorA.GetTachoCount();
            //var desired = Convert.ToInt32((turn * 0.8));

            //ev3.MotorA.MoveTo(power, desired, true);
        }

        /// <summary>
        /// Andando pra frente
        /// </summary>
        /// <param name="ev3"></param>
        /// <param name="distance"></param>
        /// <param name="power"></param>
        public static void Forward(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            ev3.Vehicle.Forward(power, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);

            var originalAngle = ev3.Data.Direction / 180 * Math.PI;

            // atualizando posição
            var finalAngle = ev3.Data.Direction / 180 * Math.PI;

            if (distance < 0)
            {
                finalAngle = (finalAngle + Math.PI) % (Math.PI * 2);
                distance = Math.Abs(distance);
            }

            var beta = finalAngle - originalAngle;
            var r = distance * ev3.Data.TyreLegth;
            var angle = Math.Atan(beta * ev3.Data.Length / r);

            ev3.Data.WheelAngle = angle / Math.PI * 180;

            // aproximadamente linha reta
            if (Math.Abs(beta) < 0.01)
            {
                ev3.Data.PosX += r * Math.Cos(originalAngle);
                ev3.Data.PosY += r * Math.Sin(originalAngle);
            }
            else
            {
                // bicycle model
                var cx = ev3.Data.PosX - Math.Sin(originalAngle) * r;
                var cy = ev3.Data.PosY + Math.Cos(originalAngle) * r;
                ev3.Data.PosX = cx + Math.Sin(finalAngle);
                ev3.Data.PosY = cy - Math.Cos(finalAngle);
            }
        }

        public static void Back(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            power = Convert.ToSByte(power * -1);
            Forward(ev3, distance, power);
        }

        public static void Left(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            ev3.Vehicle.TurnLeftForward(80, 100, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);
        }

        public static void Right(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            ev3.Vehicle.TurnRightForward(80, 100, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);
        }

        public static List<int> Sense(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3)
        {
            const uint Angle = 15u;
            const int Size = 360 / (int)Angle;
            var map = new List<int>(Size);

            ev3.MotorA.On(-10, 180, true);
            ev3.MotorA.WaitForMotorToStop();

            for (var i = 0; i < Size; i++)
            {
                map.Add(ev3.Sensor1.Read());
                ev3.MotorA.On(10, Angle, true);
                ev3.MotorA.WaitForMotorToStop();
            }

            ev3.MotorA.On(-10, 180, true);
            ev3.MotorA.WaitForMotorToStop();

            return map;
        }
    }
}
