using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Autobot.Brick.Tests
{
    using System.Diagnostics;

    using Autobot.Server;

    using Autotob.Brick.EV3;

    [TestClass]
    public class BrickTests
    {
        public class TestData
        {

        }

        [TestMethod]
        public void Right()
        {
            var bot = new Brick<IRSensor, Sensor, Sensor, Sensor, TestData>("usb");
            // connect to lego
            bot.Connection.Open();

            var sw = new Stopwatch();
            sw.Start();
            bot.Right();
            sw.Stop();
            var a = sw.ElapsedMilliseconds;
            bot.Connection.Close();
        }

        [TestMethod]
        public void SenseTest()
        {
            var bot = new Brick<IRSensor, Sensor, Sensor, Sensor, TestData>("usb");
            // connect to lego
            bot.Connection.Open();

            var sw = new Stopwatch();
            sw.Start();
            bot.Sense();
            sw.Stop();
            var a = sw.ElapsedMilliseconds;
            bot.Connection.Close();
        }
    }
}
