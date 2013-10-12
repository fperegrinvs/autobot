namespace Autotob.Brick.EV3
{
    using System;

    using Autotob.Brick;

    using MonoBrick;
    using MonoBrick.EV3;

    /// <summary>
    /// Class for creating a EV3 brick
    /// </summary>
    public class Brick<TSensor1, TSensor2, TSensor3, TSensor4, TData>
        where TSensor1 : Sensor, new()
        where TSensor2 : Sensor, new()
        where TSensor3 : Sensor, new()
        where TSensor4 : Sensor, new()
        where TData : new()
    {
        #region wrapper for connection, filesystem, sensor and motor
        private Connection<Command, Reply> connection = null;
        private TSensor1 sensor1;
        private TSensor2 sensor2;
        private TSensor3 sensor3;
        private TSensor4 sensor4;
        private TData data;
        private FilSystem fileSystem = new FilSystem();
        private Motor motorA = new Motor();
        private Motor motorB = new Motor();
        private Motor motorC = new Motor();
        private Motor motorD = new Motor();
        private Memory memory = new Memory();
        private MotorSync motorSync = new MotorSync();
        private Vehicle vehicle = new Vehicle(MotorPort.OutB, MotorPort.OutC);
        private Mailbox mailbox = new Mailbox();
        private void Init()
        {
            this.Sensor1 = new TSensor1();
            this.Sensor2 = new TSensor2();
            this.Sensor3 = new TSensor3();
            this.Sensor4 = new TSensor4();
            this.data = new TData();
            this.fileSystem.Connection = this.connection;
            this.motorA.Connection = this.connection;
            this.motorA.BitField = OutputBitfield.OutA;
            this.motorB.Connection = this.connection;
            this.motorB.BitField = OutputBitfield.OutB;
            this.motorC.Connection = this.connection;
            this.motorC.BitField = OutputBitfield.OutC;
            this.motorD.Connection = this.connection;
            this.motorD.BitField = OutputBitfield.OutD;
            this.motorSync.Connection = this.connection;
            this.motorSync.BitField = OutputBitfield.OutA | OutputBitfield.OutD;
            this.memory.Connection = this.connection;
            this.mailbox.Connection = this.connection;
            this.vehicle.Connection = this.connection;

        }

        /// <summary>
        /// Manipulate memory on the EV3
        /// </summary>
        /// <value>The memory.</value>
        /*public Memory Memory {
            get{return memory;}
        }
        */

        /// <summary>
        /// Message system used to write and read data to/from the brick
        /// </summary>
        /// <value>
        /// The message system
        /// </value>
        public Mailbox Mailbox
        {
            get { return this.mailbox; }
        }

        public TData Data
        {
            get
            {
                return this.data;
            }
        }

        /// <summary>
        /// Motor A
        /// </summary>
        /// <value>
        /// The motor connected to port A
        /// </value>
        public Motor MotorA
        {
            get { return this.motorA; }
        }

        /// <summary>
        /// Motor B
        /// </summary>
        /// <value>
        /// The motor connected to port B
        /// </value>
        public Motor MotorB
        {
            get { return this.motorB; }
        }

        /// <summary>
        /// Motor C
        /// </summary>
        /// <value>
        /// The motor connected to port C
        /// </value>
        public Motor MotorC
        {
            get { return this.motorC; }
        }

        /// <summary>
        /// Motor D
        /// </summary>
        /// <value>
        /// The motor connected to port D
        /// </value>
        public Motor MotorD
        {
            get { return this.motorD; }
        }

        /// <summary>
        /// Synchronise two motors
        /// </summary>
        /// <value>The motor sync.</value>
        public MotorSync MotorSync
        {
            get { return this.motorSync; }
        }

        /// <summary>
        /// Use the brick as a vehicle
        /// </summary>
        /// <value>
        /// The vehicle
        /// </value>
        public Vehicle Vehicle
        {
            get { return this.vehicle; }
        }

        /// <summary>
        /// Gets or sets the sensor connected to port 1
        /// </summary>
        /// <value>
        /// The sensor connected to port 1
        /// </value>
        public TSensor1 Sensor1
        {
            get { return this.sensor1; }
            set
            {
                this.sensor1 = value;
                this.sensor1.Port = SensorPort.In1;
                this.sensor1.Connection = this.connection;
            }
        }

        /// <summary>
        /// Gets or sets the sensor connected to port 2
        /// </summary>
        /// <value>
        /// The sensor connected to port 2
        /// </value>
        public TSensor2 Sensor2
        {
            get { return this.sensor2; }
            set
            {
                this.sensor2 = value;
                this.sensor2.Port = SensorPort.In2;
                this.sensor2.Connection = this.connection;
            }
        }

        /// <summary>
        /// Gets or sets the sensor connected to port 3
        /// </summary>
        /// <value>
        /// The sensor connected to port 3
        /// </value>
        public TSensor3 Sensor3
        {
            get { return this.sensor3; }
            set
            {
                this.sensor3 = value;
                this.sensor3.Port = SensorPort.In3;
                this.sensor3.Connection = this.connection;
            }
        }

        /// <summary>
        /// Gets or sets the sensor connected to port 4
        /// </summary>
        /// <value>
        /// The sensor connected to port 4
        /// </value>
        public TSensor4 Sensor4
        {
            get { return this.sensor4; }
            set
            {
                this.sensor4 = value;
                this.sensor4.Port = SensorPort.In4;
                this.sensor4.Connection = this.connection;
            }
        }

        /// <summary>
        /// The file system 
        /// </summary>
        /// <value>
        /// The file system
        /// </value>
        public FilSystem FileSystem
        {
            get { return this.fileSystem; }
        }

        /// <summary>
        /// Gets the connection that the brick uses
        /// </summary>
        /// <value>
        /// The connection
        /// </value>
        public Connection<Command, Reply> Connection
        {
            get { return this.connection; }
        }


        /// <summary>
        /// Initializes a new instance of the Brick class.
        /// </summary>
        /// <param name='connection'>
        /// Connection to use
        /// </param>
        public Brick(Connection<Command, Reply> connection)
        {
            this.connection = connection;
            this.Init();
        }

        /// <summary>
        /// Initializes a new instance of the Brick class with bluetooth, usb or WiFi connection
        /// </summary>
        /// <param name='connection'>
        /// Can either be a serial port name for bluetooth connection or "usb" for usb connection and finally "wiFi" for WiFi connection
        /// </param>
        public Brick(string connection)
        {

            switch (connection.ToLower())
            {
#if WINDOWS
                case "usb":
                    this.connection = new USB<Command,Reply>();
                break;
#endif
                //case "wifi":
                //    this.connection = new WiFiConnection<Command,Reply>(10000); //10 seconds timeout when connecting
                //break;
                //case "loopback":
                //    throw new NotImplementedException("Loopback connection has not been implemented for EV3");
                default:
#if !WINDOWS
                    this.connection = new DroidBlueTooth<Command, Reply>(connection);
#endif
                    break;
            }
            this.Init();
        }

        /// <summary>
        /// Initializes a new instance of the Brick class with a tunnel connection
        /// </summary>
        /// <param name='ipAddress'>
        /// The IP address to use
        /// </param>
        /// <param name='port'>
        /// The port number to use
        /// </param>
        public Brick(string ipAddress, ushort port)
        {
            this.connection = new TunnelConnection<Command, Reply>(ipAddress, port);
            this.Init();
        }

        #endregion

        #region brick functions

        /// <summary>
        /// Start a program on the brick
        /// </summary>
        /// <param name="file">File to start</param>
        public void StartProgram(BrickFile file)
        {
            this.StartProgram(file, false);
        }

        /// <summary>
        /// Start a program on the brick
        /// </summary>
        /// <param name="file">File to stat.</param>
        /// <param name="reply">If set to <c>true</c> reply from brick will be send</param>
        public void StartProgram(BrickFile file, bool reply)
        {
            this.StartProgram(file.FullName, reply);
        }

        /// <summary>
        /// Start a program on the brick
        /// </summary>
        /// <param name='name'>
        /// The name of the program to start
        /// </param>
        public void StartProgram(string name)
        {
            this.StartProgram(name, false);
        }

        /// <summary>
        /// Starts a program on the brick
        /// </summary>
        /// <param name='name'>
        /// The of the program to start
        /// </param>
        /// <param name='reply'>
        /// If set to <c>true</c> the brick will send a reply
        /// </param>
        public void StartProgram(string name, bool reply)
        {
            var command = new Command(0, 8, 400, reply);
            command.Append(ByteCodes.File);
            command.Append(FileSubCodes.LoadImage);
            command.Append((byte)ProgramSlots.User, ConstantParameterType.Value);
            command.Append(name, ConstantParameterType.Value);
            command.Append(0, VariableScope.Local);
            command.Append(4, VariableScope.Local);
            command.Append(ByteCodes.ProgramStart);
            command.Append((byte)ProgramSlots.User);
            command.Append(0, VariableScope.Local);
            command.Append(4, VariableScope.Local);
            command.Append(0, ParameterFormat.Short);
            this.connection.Send(command);
            System.Threading.Thread.Sleep(5000);
            if (reply)
            {
                var brickReply = this.connection.Receive();
                Error.CheckForError(brickReply, 400);
            }
        }

        /// <summary>
        /// Stops all running programs
        /// </summary>
        public void StopProgram()
        {
            this.StopProgram(false);
        }

        /// <summary>
        /// Stops all running programs
        /// </summary>
        /// <param name='reply'>
        /// If set to <c>true</c> reply the brick will send a reply
        /// </param>
        public void StopProgram(bool reply)
        {
            var command = new Command(0, 0, 401, reply);
            command.Append(ByteCodes.ProgramStop);
            command.Append((byte)ProgramSlots.User, ConstantParameterType.Value);
            this.connection.Send(command);
            if (reply)
            {
                var brickReply = this.connection.Receive();
                Error.CheckForError(brickReply, 401);
            }
        }

        /// <summary>
        /// Get the name of the program that is curently running
        /// </summary>
        /// <returns>
        /// The running program.
        /// </returns>
        public string GetRunningProgram()
        {
            return "";
        }

        /// <summary>
        /// Play a tone.
        /// </summary>
        /// <param name="volume">Volume.</param>
        /// <param name="frequency">Frequency of the tone</param>
        /// <param name="durationMs">Duration in ms.</param>
        public void PlayTone(byte volume, UInt16 frequency, UInt16 durationMs)
        {
            this.PlayTone(volume, frequency, durationMs, false);
        }

        /// <summary>
        /// Play a tone.
        /// </summary>
        /// <param name="volume">Volume.</param>
        /// <param name="frequency">Frequency of the tone</param>
        /// <param name="durationMs">Duration in ms.</param>
        /// <param name="reply">If set to <c>true</c> reply from brick will be send</param>
        public void PlayTone(byte volume, UInt16 frequency, UInt16 durationMs, bool reply)
        {
            var command = new Command(0, 0, 123, reply);
            command.Append(ByteCodes.Sound);
            command.Append(SoundSubCodes.Tone);
            command.Append(volume, ParameterFormat.Short);
            command.Append(frequency, ConstantParameterType.Value);
            command.Append(durationMs, ConstantParameterType.Value);
            this.connection.Send(command);
            if (reply)
            {
                var brickReply = this.connection.Receive();
                Error.CheckForError(brickReply, 123);
            }
        }

        /// <summary>
        /// Make the brick say beep
        /// </summary>
        /// <param name="volume">Volume of the beep</param>
        /// <param name="durationMs">Duration in ms.</param>
        public void Beep(byte volume, UInt16 durationMs)
        {
            this.Beep(volume, durationMs, false);
        }

        /// <summary>
        /// Make the brick say beep
        /// </summary>
        /// <param name="volume">Volume of the beep</param>
        /// <param name="durationMs">Duration in ms.</param>
        /// <param name="reply">If set to <c>true</c> reply from the brick will be send</param>
        public void Beep(byte volume, UInt16 durationMs, bool reply)
        {
            this.PlayTone(volume, 1000, durationMs, reply);
        }

        /// <summary>
        /// Play a sound file.
        /// </summary>
        /// <param name="name">Name the name of the file to play</param>
        /// <param name="volume">Volume.</param>
        /// <param name="repeat">If set to <c>true</c> the file will play in a loop</param>
        public void PlaySoundFile(string name, byte volume, bool repeat)
        {
            this.PlaySoundFile(name, volume, repeat, false);
        }

        /// <summary>
        /// Play a sound file.
        /// </summary>
        /// <param name="name">Name the name of the file to play</param>
        /// <param name="volume">Volume.</param>
        /// <param name="repeat">If set to <c>true</c> the file will play in a loop</param>
        /// <param name="reply">If set to <c>true</c> a reply from the brick will be send</param>
        public void PlaySoundFile(string name, byte volume, bool repeat, bool reply)
        {
            Command command = null;
            if (repeat)
            {
                command = new Command(0, 0, 200, reply);
                command.Append(ByteCodes.Sound);
                command.Append(SoundSubCodes.Repeat);
                command.Append(volume, ConstantParameterType.Value);
                command.Append(name, ConstantParameterType.Value);
                command.Append(ByteCodes.SoundReady);//should this be here?
            }
            else
            {
                command = new Command(0, 0, 200, reply);
                command.Append(ByteCodes.Sound);
                command.Append(SoundSubCodes.Play);
                command.Append(volume, ConstantParameterType.Value);
                command.Append(name, ConstantParameterType.Value);
                command.Append(ByteCodes.SoundReady);//should this be here?
            }
            this.connection.Send(command);
            if (reply)
            {
                var brickReply = this.connection.Receive();
                Error.CheckForError(brickReply, 200);
            }
        }

        /// <summary>
        /// Stops all sound playback.
        /// </summary>
        /// <param name="reply">If set to <c>true</c> reply from brick will be send</param>
        public void StopSoundPlayback(bool reply = false)
        {
            var command = new Command(0, 0, 123, reply);
            command.Append(ByteCodes.Sound);
            command.Append(SoundSubCodes.Break);
            this.connection.Send(command);
            if (reply)
            {
                var brickReply = this.connection.Receive();
                Error.CheckForError(brickReply, 123);
            }
        }

        /// <summary>
        /// Gets the sensor types of all four sensors
        /// </summary>
        /// <returns>The sensor types.</returns>
        public SensorType[] GetSensorTypes()
        {
            var command = new Command(5, 0, 200, true);
            command.Append(ByteCodes.InputDeviceList);
            command.Append((byte)4, ParameterFormat.Short);
            command.Append((byte)0, VariableScope.Global);
            command.Append((byte)4, VariableScope.Global);
            var reply = this.Connection.SendAndReceive(command);
            SensorType[] type = new SensorType[4];
            for (int i = 0; i < 4; i++)
            {
                if (Enum.IsDefined(typeof(SensorType), (int)reply[i + 3]))
                {
                    type[i] = (SensorType)reply[i + 3];
                }
                else
                {
                    type[i] = SensorType.Unknown;
                }
            }
            return type;
        }
        #endregion
    }
}

