namespace Autobot.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Autobot.Common;

    using Autotob.Brick.EV3;

    public static class TruckExtensions
    {
        static void WaitForMotorToStop(this Motor motor, int? tacho = null)
        {
            try
            {
                var waited = false;

                // pausa inicial para o comando come�ar a ser processado
                if (!tacho.HasValue)
                {
                    Thread.Sleep(50);
                }

                while (true)
                {
                    // verifica se motor est� ativo
                    if (motor.IsRunning())
                    {
                        Thread.Sleep(20);
                    }
                    else if (tacho.HasValue)
                    {
                        var count = motor.GetTachoCount();
                        var diff = Math.Abs(count - tacho.Value);

                        if (diff >= 2)
                        {
                            if (!waited)
                            {
                                Thread.Sleep(50);
                                waited = true;
                            }
                            else
                            {
                                // comando se perdeu, criar um novo
                                if (count < tacho.Value)
                                {
                                    motor.On(10, Convert.ToUInt32(tacho.Value - count), true);
                                }
                                else
                                {
                                    motor.On(-10, Convert.ToUInt32(count - tacho.Value), true);
                                }

                                Thread.Sleep(20);
                                waited = false;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static void Speed<TData>(
            this Brick<IRSensor, Sensor, Sensor, Sensor, TData> ev3, int speed) where TData : new()
        {
            ev3.Vehicle.Forward(Convert.ToSByte(speed));
        }

        public static void Turn<TData>(this Brick<IRSensor, Sensor, Sensor, Sensor, TData> ev3, int turn, int speed) where TData : new()
        {
            ev3.Vehicle.TurnRightForward(Convert.ToSByte(speed), Convert.ToSByte(turn));
        }

        /// <summary>
        /// Andando pra frente
        /// </summary>
        /// <param name="ev3"></param>
        /// <param name="distance"></param>
        /// <param name="power"></param>
        public static void Forward(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            // angulo original
            var originalDirection = ev3.Data.Direction;

            // reseta o tacometro
            ev3.MotorB.ResetTacho();

            // movimenta o ve�culo para frente
            ev3.Vehicle.Forward(power, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);

            // aguarda motor parar
            WaitForMotorToStop(ev3.MotorB);

            // angulo final do ve�culo
            var finalDirection = ev3.Data.Direction;

            // verifica se o ve�culo andou torto, se for este o caso, � necess�rio entender o motivo
            if (Math.Abs(originalDirection - finalDirection) > 5)
            {
                throw new Exception("Movimento n�o mapeado");
            }


            ev3.Data.Direction = finalDirection;

            var angle = finalDirection / 180.0 * Math.PI;

            // giro efetivo do motor
            var tacho = ev3.MotorB.GetTachoCount();

            var total = tacho * ev3.Data.TyreLegth;

            // se o ve�culo est� de r�, considera o angulo inverso
            if (total < 0)
            {
                total = Math.Abs(total);
                angle += Math.PI;
            }

            var deltaX = total * Math.Cos(angle);
            var deltaY = total * Math.Sin(angle);

            ev3.Data.PosX += deltaX;
            ev3.Data.PosY += deltaY;
        }

        public static void Back(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            power = Convert.ToSByte(power * -1);
            Forward(ev3, distance, power);
        }

        public static void Left(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            var originalDirection = ev3.Data.Direction;
            ev3.Vehicle.TurnLeftForward(80, 100, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);
            WaitForMotorToStop(ev3.MotorB);
            var finalDirection = ev3.Data.Direction;

            var originalDirectionRadians = originalDirection / 180.0 * Math.PI;
            var finalDiretionRadians = finalDirection / 180.0 * Math.PI;
            var half_w = ev3.Data.Length / 2.0;

            var dX = half_w * (Math.Sin(finalDiretionRadians) - Math.Sin(originalDirectionRadians));
            var dy = half_w * (Math.Cos(originalDirectionRadians) - Math.Cos(finalDiretionRadians));

            ev3.Data.Direction = finalDirection;
            ev3.Data.PosX += dX;
            ev3.Data.PosY += dy;
        }

        public static void Right(this Brick<IRSensor, Sensor, Sensor, Sensor, CarData> ev3, double distance = 1, sbyte power = 80)
        {
            var originalDirection = ev3.Data.Direction;
            ev3.Vehicle.TurnRightForward(80, 100, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);
            WaitForMotorToStop(ev3.MotorB);
            var finalDirection = ev3.Data.Direction;

            var originalDirectionRadians = originalDirection / 180.0 * Math.PI;
            var finalDiretionRadians = finalDirection / 180.0 * Math.PI;
            var half_w = ev3.Data.Length / 2.0;

            var dX = half_w * (Math.Sin(originalDirectionRadians) - Math.Sin(finalDiretionRadians));
            var dy = half_w * (Math.Cos(finalDiretionRadians) - Math.Cos(originalDirectionRadians));

            ev3.Data.Direction = finalDirection;
            ev3.Data.PosX += dX;
            ev3.Data.PosY += dy;
        }

        public static List<int> Sense<TData>(this Brick<IRSensor, Sensor, Sensor, Sensor, TData> ev3) where TData : new()
        {
            const uint Angle = 15u;
            const int Size = 360 / (int)Angle;
            var map = new List<int>(Size);

            ev3.MotorA.ResetTacho();
            ev3.MotorA.On(-10, 180, true);
            ev3.MotorA.WaitForMotorToStop(-180);

            for (var i = 0; i < Size; i++)
            {
                map.Add(ev3.Sensor1.Read());
                ev3.MotorA.On(10, Angle, true);
                ev3.MotorA.WaitForMotorToStop(Convert.ToInt32(-180 + ((i + 1) * Angle)));
            }

            ev3.MotorA.On(-10, 180, true);
            ev3.MotorA.WaitForMotorToStop(0);

            return map;
        }
    }
}
