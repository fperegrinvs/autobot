namespace Autobot.Server
{
    using System;
    using System.Collections.Generic;

    using Autobot.Common;

    using Autotob.Brick.EV3;

    using global::Android.App;
    using global::Android.Hardware;
    using global::Android.OS;
    using global::Android.Widget;

    using Sensor = Autotob.Brick.EV3.Sensor;

    [Activity(Label = "Brick.Android", MainLauncher = true, Icon = "@drawable/icon")]
    public partial class MainActivity : Activity, ISensorEventListener
    {
        /// <summary>
        /// Init event
        /// </summary>
        /// <param name="bundle"></param>
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            this.SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            var onButton = this.FindViewById<Button>(Resource.Id.OnButton);
            onButton.Click += delegate { this.On(); };

            var offButton = this.FindViewById<Button>(Resource.Id.OffButton);
            offButton.Click += delegate { this.Off(); };
        }

        /// <summary>
        /// Bot instance
        /// </summary>
        public Brick<IRSensor, Sensor, Sensor, Sensor, CarData> Bot { get; set; }

        /// <summary>
        /// Turning bot on
        /// </summary>
        public void On()
        {
            this.Bot = new Brick<IRSensor, Sensor, Sensor, Sensor, CarData>("EV3");
            try
            {
                // register the compass sensor
                this.RegisterSensor();

                // open the tcp server
                this.OpenTcp();

                // connect to lego
                this.Bot.Connection.Open();
            }
            catch (Exception e)
            {
                string msg = e.Message;
                if (e.InnerException != null)
                {
                    msg += e.InnerException.Message;
                }

                Toast.MakeText(this, msg, ToastLength.Long).Show();
            }
        }

        /// <summary>
        /// Registra um sensor
        /// </summary>
        public void RegisterSensor()
        {
            // Register sensor
            var sm = (SensorManager)this.GetSystemService(SensorService);

            IList<Android.Hardware.Sensor> mySensors = sm.GetSensorList(Android.Hardware.SensorType.Orientation);

            if (mySensors.Count > 0)
            {
                sm.RegisterListener(this, mySensors[0], SensorDelay.Normal);
                Toast.MakeText(this, "Start ORIENTATION Sensor", ToastLength.Long).Show();
            }
            else
            {
                Toast.MakeText(this, "No ORIENTATION Sensor", ToastLength.Long).Show();
                this.Finish();
            }
        }

        /// <summary>
        /// Turning bot off
        /// </summary>
        public void Off()
        {
            this.Bot.Connection.Close();
        }

        public void Test2()
        {
            var ev3 = new Brick<IRSensor, Sensor, Sensor, Sensor, CarData>("Bot");
            try
            {
                ev3.Connection.Open();
                //ev3.Sensor1.Mode = IRMode.Proximity;
                // var tc = ev3.MotorA.GetTachoCount();
                //ev3.Right();
                //Thread.Sleep(300);
                //ev3.Left();
                ev3.MotorC.On(80, 360, true);
                // var x = ev3.MotorB.GetTachoCount();
                // ev3.MotorB.ResetTacho();
                // var map = ev3.Sense();
            }
            catch (Exception e)
            {
                string msg = e.Message;
                if (e.InnerException != null)
                {
                    msg += e.InnerException.Message;
                }

                Toast.MakeText(this, msg, ToastLength.Long).Show();
            }
            finally
            {
                ev3.Connection.Close();
            }
        }

        /// <summary>
        /// Sensor accuracy changed
        /// </summary>
        /// <param name="sensor">sensor instance</param>
        /// <param name="accuracy">new accuracy</param>
        public void OnAccuracyChanged(Android.Hardware.Sensor sensor, SensorStatus accuracy)
        {
        }

        /// <summary>
        /// Sensor reading changed
        /// </summary>
        /// <param name="e">new reading</param>
        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type == Android.Hardware.SensorType.Orientation)
            {
                Bot.Data.Direction = e.Values[0];
            }
        }
    }
}

