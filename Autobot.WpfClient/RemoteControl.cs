using System;

namespace Autobot.WpfClient
{
    using System.Threading;

    using SlimDX.XInput;

    public class RemoteControl
    {
        public RemoteControl(BotClient client)
        {
            this.client = client;
            gamepad = new GamepadState(UserIndex.One);
            var thread = new Thread(this.UpdateState);
            thread.Start();
        }

        public void PowerOn()
        {
            isOn = true;
        }

        public void PowerOff()
        {
            isOn = false;
        }

        private bool isOn = false;

        private readonly BotClient client;

        private readonly GamepadState gamepad;

        private void UpdateState()
        {
            var rt = this.gamepad.RightTrigger;
            var lt = this.gamepad.LeftTrigger;
            var ls_x = this.gamepad.LeftStick.Position.X;
            while (isOn)
            {
                this.gamepad.Update();

                if (Math.Abs(this.gamepad.LeftStick.Position.X) < 0.1)
                {
                    // ReSharper disable CompareOfFloatsByEqualityOperator
                    if (this.gamepad.RightTrigger != rt)
                    {
                        rt = this.gamepad.RightTrigger;
                        client.UpdateSpeed(Convert.ToInt16(Math.Round(rt * 100, 0)));
                    }

                    if (this.gamepad.LeftTrigger != lt)
                    {
                        lt = this.gamepad.LeftTrigger;
                        client.UpdateSpeed(Convert.ToInt16(Math.Round(lt * -100, 0)));
                    }
                    // ReSharper restore CompareOfFloatsByEqualityOperator
                }
                else
                {
                    lt = this.gamepad.LeftTrigger;
                    rt = this.gamepad.RightTrigger;
                    ls_x = this.gamepad.LeftStick.Position.X;
                    var speed = rt > 0 ? rt : lt * -1;
                    client.UpdateDirection(Convert.ToInt16(Math.Round(ls_x * 100, 0)), Convert.ToInt16(Math.Round(speed * 100, 0)));
                }


                Thread.Sleep(50);
            }
        }
    }
}
