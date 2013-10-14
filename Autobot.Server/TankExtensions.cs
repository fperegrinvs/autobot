namespace Autobot.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Autobot.Common;

    using Autotob.Brick.EV3;

    public class Tank : Brick<IRSensor, Sensor, Sensor, Sensor, CarData>
    {
        public Tank(string connection) : base(connection)
        {
            
        }
    }

    public static class TankExtensions
    {
        static void WaitForMotorToStop(this Motor motor, int? tacho = null)
        {
            try
            {
                var waited = false;

                // pausa inicial para o comando começar a ser processado
                if (!tacho.HasValue)
                {
                    Thread.Sleep(50);
                }

                while (true)
                {
                    // verifica se motor está ativo
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

        public static void Speed(this Tank ev3, int speed)
        {
            ev3.Vehicle.Forward(Convert.ToSByte(speed));
        }

        public static void Turn(this Tank ev3, int turn, int speed)
        {
            ev3.Vehicle.TurnRightForward(Convert.ToSByte(speed), Convert.ToSByte(turn));
        }

        /// <summary>
        /// Andando pra frente
        /// </summary>
        /// <param name="ev3"></param>
        /// <param name="distance"></param>
        /// <param name="power"></param>
        public static void Forward(this Tank ev3, double distance = 1, sbyte power = 80)
        {
            lock (ev3.Data.MoveLock)
            {
                // angulo original
                var originalDirection = ev3.Data.Direction;

                // reseta o tacometro
                ev3.MotorB.ResetTacho();

                // movimenta o veículo para frente
                ev3.Vehicle.Forward(power, Convert.ToUInt16(Math.Round(360 * distance, 0)), false, true);

                // aguarda motor parar
                WaitForMotorToStop(ev3.MotorB);

                // angulo final do veículo
                var finalDirection = ev3.Data.Direction;

                // verifica se o veículo andou torto, se for este o caso, é necessário entender o motivo
                if (Math.Abs(originalDirection - finalDirection) > 5)
                {
                    throw new Exception("Movimento não mapeado");
                }

                var delta = ev3.GetMovement();

                ev3.Data.PosX += delta.Item1;
                ev3.Data.PosY += delta.Item2;

                // reseta o tacometro
                ev3.MotorB.ResetTacho();
            }
        }


        public static Tuple<double, double> GetMovement(this Tank ev3)
        {
            var angle = ev3.Data.Direction / 180.0 * Math.PI;

            // giro efetivo do motor
            var tacho = ev3.MotorB.GetTachoCount();

            var total = tacho * ev3.Data.TyreLegth;

            // se o veículo está de ré, considera o angulo inverso
            if (total < 0)
            {
                total = Math.Abs(total);
                angle += Math.PI;
            }

            var deltaX = total * Math.Cos(angle);
            var deltaY = total * Math.Sin(angle);

            return new Tuple<double, double>(deltaX, deltaY);
        }

        public static void Back(this Tank ev3, double distance = 1, sbyte power = 80)
        {
            power = Convert.ToSByte(power * -1);
            Forward(ev3, distance, power);
        }

        public static void Left(this Tank ev3, double distance = 1, sbyte power = 80)
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

        public static void Right(this Tank ev3, double distance = 1, sbyte power = 80)
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

        public static List<SenseData> Sense(this Tank ev3, int angle = 360)
        {
            const uint Angle = 15u;
            int size = angle / (int)Angle;
            var map = new List<SenseData>(size);

            var half_angle = Convert.ToUInt16(angle / 2);
            var negative_half = half_angle * -1;

            ev3.MotorA.ResetTacho();
            ev3.MotorA.On(-10,  half_angle, true);
            ev3.MotorA.WaitForMotorToStop(negative_half);

            for (var i = 0; i < size; i++)
            {
                var sense = new SenseData();
                sense.Distance = ev3.Sensor1.Read();
                sense.Angle = (ev3.Data.Direction + ev3.MotorA.GetTachoCount()) % 360;

                var movement = ev3.GetMovement();
                sense.PositionX = ev3.Data.PosX + movement.Item1;
                sense.PositionY = ev3.Data.PosY + movement.Item2;

                map.Add(sense);
                ev3.MotorA.On(10, Angle, true);
                ev3.MotorA.WaitForMotorToStop(Convert.ToInt32(negative_half + ((i + 1) * Angle)));
            }

            ev3.MotorA.On(-10, half_angle, true);
            ev3.MotorA.WaitForMotorToStop(0);

            return map;
        }
    }
}
