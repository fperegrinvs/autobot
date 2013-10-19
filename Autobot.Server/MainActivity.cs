namespace Autobot.Server
{
    using System;
    using System.Globalization;
    using System.Linq;

    using Autobot.Common;

    using Autotob.Brick.EV3;

    using global::Android.App;
    using global::Android.Hardware;
    using global::Android.OS;
    using global::Android.Widget;

    using Sensor = Autotob.Brick.EV3.Sensor;
    using SensorType = Android.Hardware.SensorType;

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

            compassText = this.FindViewById<TextView>(Resource.Id.CompassTxt);
        }


        private TextView compassText;

        /// <summary>
        /// Bot instance
        /// </summary>
        public Tank Bot { get; set; }

        /// <summary>
        /// Turning bot on
        /// </summary>
        public void On()
        {
            this.Bot = new Tank("EV3");
            try
            {
                // register the compass sensor
                this.RegisterSensor();

                // open the tcp server
                this.OpenTcp();

                // connect to lego
                this.Bot.Connection.Open();

				// sensor 1
				this.Bot.Sensor1.Mode = UltrasonicMode.Centimeter;

                this.Bot.MotorA.ResetTacho();
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

            var accelerometer = sm.GetDefaultSensor(SensorType.Accelerometer);
            var magnetometer = sm.GetDefaultSensor(SensorType.MagneticField);

            sm.RegisterListener(this, accelerometer, SensorDelay.Ui);
            sm.RegisterListener(this, magnetometer, SensorDelay.Ui);
        }

        /// <summary>
        /// Turning bot off
        /// </summary>
        public void Off()
        {
            this.Bot.Connection.Close();
        }

        /// <summary>
        /// Sensor accuracy changed
        /// </summary>
        /// <param name="sensor">sensor instance</param>
        /// <param name="accuracy">new accuracy</param>
        public void OnAccuracyChanged(Android.Hardware.Sensor sensor, SensorStatus accuracy)
        {
        }

        private float[] mGravity;

        private float[] mGeomagnetic;

        /// <summary>
        /// Sensor reading changed
        /// </summary>
        /// <param name="e">new reading</param>
        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type == SensorType.Accelerometer)
            {
                mGravity = e.Values.ToArray();
            }

            if (e.Sensor.Type == SensorType.MagneticField)
            {
                mGeomagnetic = e.Values.ToArray();
            }

            if (mGravity != null && mGeomagnetic != null)
            {
                var r = new float[9];
                var I = new float[9];
                bool success = SensorManager.GetRotationMatrix(r, I, mGravity, mGeomagnetic);
                if (success)
                {
                    var orientation = new float[3];
                    SensorManager.GetOrientation(r, orientation);
                    var degrees = Java.Lang.Math.ToDegrees(orientation[0]);

                    // degrees = [-180, 180] => [0, 360]
                    // 0 is north
                    if (degrees < 0)
                    {
                        degrees = 360 + degrees;
                    }

					Bot.Data.Direction = (float)degrees; // orientation contains: azimut, pitch and roll
					// compassText.Text = degrees.ToString(CultureInfo.InvariantCulture);
                }
            }
        }
    }
}

