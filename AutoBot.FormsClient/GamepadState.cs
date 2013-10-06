namespace AutoBot.FormsClient
{
    using System;

    using SlimDX;
    using SlimDX.XInput;

    public class GamepadState
    {
        uint lastPacket;

        public GamepadState(UserIndex userIndex)
        {
            this.UserIndex = userIndex;
            this.Controller = new Controller(userIndex);
        }

        public readonly UserIndex UserIndex;
        public readonly Controller Controller;

        public DPadState DPad { get; private set; }
        public ThumbstickState LeftStick { get; private set; }
        public ThumbstickState RightStick { get; private set; }

        public bool A { get; private set; }
        public bool B { get; private set; }
        public bool X { get; private set; }
        public bool Y { get; private set; }

        public bool RightShoulder { get; private set; }
        public bool LeftShoulder { get; private set; }

        public bool Start { get; private set; }
        public bool Back { get; private set; }

        public float RightTrigger { get; private set; }
        public float LeftTrigger { get; private set; }

        public bool Connected
        {
            get { return this.Controller.IsConnected; }
        }

        public void Vibrate(float leftMotor, float rightMotor)
        {
            this.Controller.SetVibration(new Vibration
            {
                LeftMotorSpeed = (ushort)(MathHelper.Saturate(leftMotor) * ushort.MaxValue),
                RightMotorSpeed = (ushort)(MathHelper.Saturate(rightMotor) * ushort.MaxValue)
            });
        }

        public void Update()
        {
            // If not connected, nothing to update
            if (!this.Connected) return;

            // If same packet, nothing to update
            State state = this.Controller.GetState();
            if (this.lastPacket == state.PacketNumber) return;
            this.lastPacket = state.PacketNumber;

            var gamepadState = state.Gamepad;

            // Shoulders
            this.LeftShoulder = (gamepadState.Buttons & GamepadButtonFlags.LeftShoulder) != 0;
            this.RightShoulder = (gamepadState.Buttons & GamepadButtonFlags.RightShoulder) != 0;

            // Triggers
            this.LeftTrigger = gamepadState.LeftTrigger / (float)byte.MaxValue;
            this.RightTrigger = gamepadState.RightTrigger / (float)byte.MaxValue;

            // Buttons
            this.Start = (gamepadState.Buttons & GamepadButtonFlags.Start) != 0;
            this.Back = (gamepadState.Buttons & GamepadButtonFlags.Back) != 0;

            this.A = (gamepadState.Buttons & GamepadButtonFlags.A) != 0;
            this.B = (gamepadState.Buttons & GamepadButtonFlags.B) != 0;
            this.X = (gamepadState.Buttons & GamepadButtonFlags.X) != 0;
            this.Y = (gamepadState.Buttons & GamepadButtonFlags.Y) != 0;

            // D-Pad
            this.DPad = new DPadState((gamepadState.Buttons & GamepadButtonFlags.DPadUp) != 0,
                                 (gamepadState.Buttons & GamepadButtonFlags.DPadDown) != 0,
                                 (gamepadState.Buttons & GamepadButtonFlags.DPadLeft) != 0,
                                 (gamepadState.Buttons & GamepadButtonFlags.DPadRight) != 0);

            // Thumbsticks
            this.LeftStick = new ThumbstickState(
                Normalize(gamepadState.LeftThumbX, gamepadState.LeftThumbY, Gamepad.GamepadLeftThumbDeadZone),
                (gamepadState.Buttons & GamepadButtonFlags.LeftThumb) != 0);
            this.RightStick = new ThumbstickState(
                Normalize(gamepadState.RightThumbX, gamepadState.RightThumbY, Gamepad.GamepadRightThumbDeadZone),
                (gamepadState.Buttons & GamepadButtonFlags.RightThumb) != 0);
        }

        static Vector2 Normalize(short rawX, short rawY, short threshold)
        {
            var value = new Vector2(rawX, rawY);
            var magnitude = value.Length();
            var direction = value / (magnitude == 0 ? 1 : magnitude);

            var normalizedMagnitude = 0.0f;
            if (magnitude - threshold > 0)
                normalizedMagnitude = Math.Min((magnitude - threshold) / (short.MaxValue - threshold), 1);

            return direction * normalizedMagnitude;
        }

        public struct DPadState
        {
            public readonly bool Up, Down, Left, Right;

            public DPadState(bool up, bool down, bool left, bool right)
            {
                this.Up = up; this.Down = down; this.Left = left; this.Right = right;
            }
        }

        public struct ThumbstickState
        {
            public readonly Vector2 Position;
            public readonly bool Clicked;

            public ThumbstickState(Vector2 position, bool clicked)
            {
                this.Clicked = clicked;
                this.Position = position;
            }
        }
    }

    public static class MathHelper
    {
        public static float Saturate(float value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }
    }
}