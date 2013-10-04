namespace Autotob.Brick
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using MonoBrick;

    /// <summary>
	/// Abstract class for creating a Network connection
	/// </summary>
	public abstract class NetworkConnection<TBrickCommand,TBrickReply> : Connection<TBrickCommand,TBrickReply>
		where TBrickCommand : BrickCommand
		where TBrickReply : BrickReply, new ()
	{
		/// <summary>
		/// The network stream to use for communication.
		/// </summary>
		protected NetworkStream stream = null;
		
		/// <summary>
		/// Open the connection or wait for tunnel
		/// </summary>
		public override void Open(){
		
		}

		/// <summary>
		/// Close the connection
		/// </summary>
		public override void Close(){
		
		}

		/// <summary>
		/// Receive a reply
		/// </summary>
		public override TBrickReply Receive(){
			byte[] lengthBytes = new byte[2];
			byte[] data = null;
			int expectedlength = 0;
			try
			{
				this.stream.ReadAll(lengthBytes);
				expectedlength = (ushort)(0x0000 | lengthBytes[0] | (lengthBytes[1] << 2));
				if(expectedlength > 0){
					data = new byte[expectedlength];
					this.stream.ReadAll(data);
				}
				
			}
			catch(Exception e) {
				throw new ConnectionException(ConnectionError.ReadError, e);
			}
			if(expectedlength == 0){
				throw new ConnectionException(ConnectionError.NoReply);
			}
			var reply = new TBrickReply();
			reply.SetData(data);
			this.ReplyWasReceived(reply);
			return reply;
		}

		/// <summary>
		/// Send a command.
		/// </summary>
		/// <param name='command'>
		/// The command to send
		/// </param>
		public override void Send(TBrickCommand command){
			byte[] data = null;
			ushort length = (ushort) command.Length;
			data = new byte[length+2];
			data[0] = (byte) (length & 0x00ff);
			data[1] = (byte)((length&0xff00) >> 2);
			Array.Copy(command.Data,0,data,2,command.Length);
			this.CommandWasSend(command);
			try
			{
				this.stream.Write(data, 0, data.Length);
			}
			catch (Exception e) {
				throw new ConnectionException(ConnectionError.WriteError,e);
			}
		}	
	
	
	}
	
	/// <summary>
	/// Class for creating a tunnel connection
	/// </summary>
	public class TunnelConnection<TBrickCommand,TBrickReply> : NetworkConnection<TBrickCommand,TBrickReply>
		where TBrickCommand : BrickCommand
		where TBrickReply : BrickReply, new ()
	{
		private TcpClient tcpClient;
		private TcpListener tcpListener = null;
		private bool waitForTunnel;
		private string address;
		private ushort port;
		
		
		/// <summary>
		/// Initializes a tunnel connection where the connection waits for a tunnel to connect
		/// </summary>
		/// <param name='port'>
		/// The port to listen for incomming connections from a tunnel
		/// </param>
		public TunnelConnection(ushort port){
			this.port = port;
			this.isConnected = false;
			//IPAddress[] ipv4Addresses = Array.FindAll(Dns.GetHostEntry(string.Empty).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
			this.tcpListener = new TcpListener(IPAddress.Any , port);
			this.waitForTunnel = true;
		}
		
		/// <summary>
		/// Initializes a tunnel connection where the bricks connects to a tunnel
		/// </summary>
		/// <param name='ipAddress'>
		/// IP address.
		/// </param>
		/// <param name='port'>
		/// Port
		/// </param>
		public TunnelConnection(string ipAddress, ushort port){
			this.address = ipAddress;
			this.port = port;
			this.isConnected = false;
			this.waitForTunnel = false;
		}

		/// <summary>
		/// Open the connection or wait for tunnel
		/// </summary>
		public override void Open(){
			if(this.waitForTunnel){
				try
				{
					this.tcpListener.Start();
					this.tcpClient = this.tcpListener.AcceptTcpClient();//blocking call
					this.tcpClient.NoDelay = true;
					this.address = ((IPEndPoint)this.tcpClient.Client.RemoteEndPoint).Address.ToString();
					this.ConnectionWasOpened();
					this.isConnected = true;
					this.stream = this.tcpClient.GetStream();
					this.tcpClient.SendTimeout = 3000;
					this.tcpClient.ReceiveTimeout = 3000;
					this.tcpListener.Stop();
				}
				catch(Exception e) {
					throw new ConnectionException(ConnectionError.OpenError, e);
				}
				this.ConnectionWasOpened();
				this.isConnected = true;
			}
			else
			{
				try
				{
					this.tcpClient = new TcpClient(this.address, this.port);
					this.tcpClient.NoDelay = true;
					this.stream = this.tcpClient.GetStream();
					this.tcpClient.SendTimeout = 3000;
					this.tcpClient.ReceiveTimeout = 3000;
					//add something more here
				}
				catch(Exception e) {
					throw new ConnectionException(ConnectionError.OpenError, e);
				}
				this.isConnected = true;
				this.ConnectionWasOpened();
			}
		}

		/// <summary>
		/// Close the connection
		/// </summary>
		public override void Close(){
			if(this.waitForTunnel){
				try{this.tcpListener.Stop();}catch{}
				try{this.stream.Close();}catch{}
				try{this.tcpClient.Close();}catch{}
				this.ConnectionWasClosed();
				this.isConnected = false;
			}
			else{
				try
				{
					this.stream.Close();
					this.tcpClient.Close();
				}
				catch (Exception) {
					
				}
				this.isConnected = false;
				this.ConnectionWasClosed();
			}
		}
	}
	
	
	/// <summary>
	/// Network connection
	/// </summary>
	public class WiFiConnection<TBrickCommand,TBrickReply> : NetworkConnection<TBrickCommand,TBrickReply>
		where TBrickCommand : BrickCommand
		where TBrickReply : BrickReply, new ()

	{
		private class UdpInfo{
			public UdpInfo (string udpInfo)
			{
				var info = udpInfo.Split(new char[] {(char)0x0d, (char)0x0a});
				foreach(var s in info){
					if(s.Contains("Serial-Number")){
						this.SerialNumber = s.Substring(s.IndexOf(":") +1).ToUpper();
					}
					if(s.Contains("Port")){
						this.Port = int.Parse(s.Substring(s.IndexOf(":") +1));
					}
					if(s.Contains("Protocol")){
						this.Protocol = s.Substring(s.IndexOf(":") +1);
					}
					if(s.Contains("Name")){
						this.Name = s.Substring(s.IndexOf(":") +1);
					}
				}
			}
		
			public string SerialNumber{get; private set;}
			public int Port{get; private set;}
			public string Name{get; private set;}
			public string Protocol{get; private set;}
			public byte[] TcpUnlockData 
			{
				get{
					string serialString = "GET /target?sn=" + this.SerialNumber + "VMTP1.0";
					System.Text.StringBuilder sb = new System.Text.StringBuilder();
					foreach (var ch in serialString)
	   					sb.Append(ch);
	   				sb.Append((char)0x0d);
	   				sb.Append((char)0x0a);
	   				string protocolString = "Protocol:" + this.Protocol;
	   				foreach (var ch in protocolString)
	   					sb.Append(ch);
	   				sb.Append((char)0x0d);
	   				sb.Append((char)0x0a);
	   				sb.Append((char)0x0d);
	   				sb.Append((char)0x0a);
	   				return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
				}
			}
		}
		
		private TcpClient tcpClient;
		int timeOut = 0;
		
		/// <summary>
		/// Initializes a new instance of the Wifi connection 
		/// </summary>
		/// <param name="connectionTimeoutMs">Time out when trying to connect if set to zero wait forever</param>
		public WiFiConnection(int connectionTimeoutMs = 0){
			this.isConnected = false;
			this.timeOut = connectionTimeoutMs;
		}

		/// <summary>
		/// Open the connection to the EV3 over a WiFi connection - this will block
		/// </summary>
		public override void Open(){
			bool hasError = false;
			bool failedToLocateEV3 = true;
			int listenPort = 3015;
			int tcpIpPort = 5555;
			UdpClient listener = null;
			UdpClient sender = null;
	        try 
	        {
	            listener = new UdpClient(listenPort);
	        	IPEndPoint groupEP = new IPEndPoint(IPAddress.Any,listenPort);
				byte[] bytes = null;
                var resetEvent = new ManualResetEvent(false);
    			Thread t = new Thread(
    			new ThreadStart(
	                delegate()
	                {
	            		try{
	            			bytes = listener.Receive( ref groupEP);
	            			resetEvent.Set();
	            		}
	            		catch{
	            			
	            		}
	            		
				    }
	            ));
		        t.IsBackground = true;
				t.Priority = ThreadPriority.Normal;
		        t.Start();
				if(this.timeOut != 0){
					if(!resetEvent.WaitOne(this.timeOut))
						listener.Close();
				}
				else{
					resetEvent.WaitOne(); //wait forever
				}
				if(bytes != null){
					failedToLocateEV3 = false;
					UdpInfo udpInfo = new UdpInfo(System.Text.Encoding.ASCII.GetString(bytes,0,bytes.Length));
	                Thread.Sleep(100);
	                sender = new UdpClient();
	                sender.Send( new byte[]{0x00}, 1, groupEP);
	                Thread.Sleep(100);
	                this.tcpClient = new TcpClient(groupEP.Address.ToString(), tcpIpPort);
					this.tcpClient.NoDelay = true;
					this.stream = this.tcpClient.GetStream();
					this.tcpClient.SendTimeout = 3000;
					this.tcpClient.ReceiveTimeout = 3000;
					this.stream.Write(udpInfo.TcpUnlockData, 0, udpInfo.TcpUnlockData.Length);
					byte[] unlockReply = new byte[16];
					this.stream.ReadAll(unlockReply);
				}
				else{
					hasError = true;	
				}
				
	        } 
	        catch 
	        {
	        	hasError = true;    
	        }
	        finally
	        {
	            if(listener != null)
	            	listener.Close();
	            if(sender != null)
	            	sender.Close();
	            
	        }
	        if(hasError){
	        	if(failedToLocateEV3){
	        		throw new ConnectionException(ConnectionError.OpenError, new Exception("Failed to find EV3"));
	        	}
	        	else{
	        		throw new ConnectionException(ConnectionError.OpenError);
	        	}
	        	
	        }
		}

		/// <summary>
		/// Close the connection
		/// </summary>
		public override void Close(){
			try{this.stream.Close();}catch{}
			try{this.tcpClient.Close();}catch{}
			this.ConnectionWasClosed();
			this.isConnected = false;
		}
	}
}

