using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/*
 *	10.04.2021 By Almog Brand 
 *	
 *	Protocol:
 *	Client to Serverr commands:		<$><command><*><args><\n>
 *	Server to Client response:		sent as a string, client waits for the entier message to arrive
 *		
 *		$       - signal start of command 
 *		command - given a special Commans enum, is actually an index according of its position in enum (easy for future extra commands that will be nneded) 
 *		args    - i used a string, because if needed, it is easy to convert to other types 
 *		\n      - signal end of commans 
 *	
 *	The protocl is not exposed to the end user. The user just needs to do is set a server, a client, and sent an SE.Commands command.
 *	The implementation uses the protocol in order to make sure all commands arriving safely (becasue UDP doesn't provide it by itself).
 *	Even if only a partial command arrives to the server, it will "append" it, and invoke only a full valid command. 
 *	Any unknow commands that answer the protocol will returne "Unknow command".
 *	
 *	The Solar Edge (SE) src files are written in a Library project and while building the project a DLL file is exported to /project/bin folder 
 *	Any application asks to use this library must add a reference to the se.dll file 
 *	Classes in the se.dll - 
 *	
 *		* SE.Server			 - Server side implementation 
 *		* SE.Client			 - Client side implementation
 *		* SE.Commands		 - Enum that holds commands names 
 *		* SE.CommandExecuter - a class in the server side, implements the commands parsing and invoking of functions, also holds the functions implementation. 
 *							   that way the server-client can be used with future different protocols, user will only need giving the server a referense of a different CommandExecuter 
 *							   Also - client side return a dynamic type, so it will be a bit easier to update it if needed to any desire type. 
 *
 *  I've used
 *	Server and Client are in different classes so if needed, can be splitted into different src files, same for dll files, so user will add only the needed dll. 
 *
 *  I have also added an option for the server to use broadcasting (default = false, also in the test i've added i don't use it). 
 *  that way no need to define the address in order to connect, but the server will boradcast and the client will listen to the boradcast signal
 *  than the client will be able to listen the boradcast (doing so, he be able to get server's address) and what is being boradcast is the port, now the client stops listen the boradcst anc connects with the server at address:port.
 *  This option might not work when using a network switch. 
 * 
 *  For testing i built a windows forms app that uses the dll files (i'll send you the project with a referense to the SE project instead of dll files so it won't minde the path to the dll's).
 *  The use of the dll's is visible in the Test project.
 *  Also - you can play with the app directly.
 *  
 *  Functions implementation - you didnt mentiones a lot, so i only made sure you can change a value only after using init. the rest simply return "successfuly.."

 */

namespace SE
{
	public enum Commands
	{
		On,
		Off,
		Init,
		Change,
		GetStatus
	}

	public class Client
	{
		private System.Net.Sockets.UdpClient _clientUdp;
		private static string _respond = null;
		private (string IP, int Port) FindBroadcaster()
		{
			var client = new System.Net.Sockets.UdpClient() { EnableBroadcast = true };
			var requestData = Encoding.ASCII.GetBytes(".");
			var serverIP = new IPEndPoint(IPAddress.Any, 0);
			client.Send(requestData, requestData.Length, new IPEndPoint(IPAddress.Broadcast, 8889));
			Thread.Sleep(100);
			var result = ("", 0);
			if (client.Available > 0)
			{
				var serverResponseData = client.Receive(ref serverIP);
				var serverResponse = Encoding.ASCII.GetString(serverResponseData);
				result = (serverIP.Address.ToString(), int.Parse(serverResponse));
				client.Close();
			}
			client.Close();
			return result;
		}
		public Client(string hostname = null, int port = 0)
		{
			if (hostname == null)
			{
				var br = FindBroadcaster();
				Trace.WriteLine($"Found Server {br.IP} {br.Port}");
				hostname = br.IP;
				port = br.Port;
			}
			_clientUdp = new UdpClient();
			_clientUdp.Connect(hostname, port);
			Task.Run(async () =>
			{
				while (true)
					try
					{
						var result = await _clientUdp.ReceiveAsync();
						_respond = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length);
					}
					catch
					{ }
			});
		}
		public bool IsConnected()
		{
			return Send("~").Result == "~";
		}
		private (bool Done, dynamic Result) Respond(int timeOut = 300)
		{
			var sw = Stopwatch.StartNew();
			while (string.IsNullOrEmpty(_respond) && sw.ElapsedMilliseconds < timeOut)
			{ }
			var res = _respond;
			_respond = null;
			/*double d;
			bool b;
			if (double.TryParse(res, out d)) return (_respond != null, d);
			if (bool.TryParse(res, out b)) return (_respond != null, b);*/
			return (res != null, res);
		}

		public (bool Done, dynamic Result) Send(Commands command, string args = "")
		{
			var data = "$" + command.ToString() + "*" + args + "\n";
			var datagram = Encoding.ASCII.GetBytes(data);
			_clientUdp.Send(datagram, datagram.Length);
			return Respond();
		}

		public (bool Done, dynamic Result) Send(string data)
		{
			var datagram = Encoding.ASCII.GetBytes(data);
			_clientUdp.Send(datagram, datagram.Length);
			return Respond();
		}
	}
	public class Server
	{
		private System.Net.Sockets.UdpClient _serverUdp;
		private bool _break = false;
		public Server(int port, CommandExecuter commandExecuter, bool broadcast = false)
		{
			_serverUdp = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Any, port));
			Task.Run(async () =>
			{
				while (!_break)
				{
					var result = await _serverUdp.ReceiveAsync();
					var msg = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length);
					byte[] data;
					if (msg == "~")
					{
						data = Encoding.ASCII.GetBytes(msg);
						_serverUdp.Send(data, data.Length, result.RemoteEndPoint);
						continue;
					}

					//Reply back to client
					data = Encoding.ASCII.GetBytes(commandExecuter.ParseCommand(msg));
					_serverUdp.Send(data, data.Length, result.RemoteEndPoint);
					
				}
			});
			if (broadcast)
			{
				Task.Run(() =>
				{
					var responseData = Encoding.ASCII.GetBytes(port.ToString());
					while (!_break)
					{
						var server = new System.Net.Sockets.UdpClient(8889);
						var clientEp = new IPEndPoint(IPAddress.Any, 0);
						server.Receive(ref clientEp);
						server.Send(responseData, responseData.Length, clientEp);
						server.Close();
					}
				});
			}
		}
		public void Stop()
		{
			_break = true;
		}
	}


	public class CommandExecuter
	{
		private string _serverRecivedData;
		public string ParseCommand(string command)
		{
			/////////////////////////////////////////////
			//    valid command <$><cmd><*><arg><\n>   //
			/////////////////////////////////////////////

			// extract a valid command from unknow sequence of data server received from client
			_serverRecivedData += command;
			var startCmd = _serverRecivedData.IndexOf('$');
			var endCmd = _serverRecivedData.IndexOf('\n');
			if (startCmd == -1 || endCmd == -1) return "";
			var cmdAndArgs = _serverRecivedData.Substring(startCmd + 1, endCmd - startCmd - 1);
			_serverRecivedData = _serverRecivedData.Replace('$' + cmdAndArgs + '\n', "");
			var argsStart = cmdAndArgs.IndexOf('*');
			var cmd = cmdAndArgs.Substring(0, argsStart);
			var args = "";
			if (argsStart + 1 <= cmdAndArgs.Length - 1) args = cmdAndArgs.Substring(argsStart + 1, cmdAndArgs.Length - 1 - argsStart);

			// invoke command
			string answer = InvokeCommand(cmd, args);
			return answer;
		}

		public string InvokeCommand(string cmd, string args)
		{
			string answer;

			switch (Enum.Parse(typeof(Commands), cmd))
			{
				case Commands.On:
					answer = On();
					break;

				case Commands.Off:
					answer = Off();
					break;

				case Commands.Init:
					answer = Init(args);
					break;

				case Commands.Change:
					answer = Change(args);
					break;

				case Commands.GetStatus:
					answer = GetStatus();
					break;

				default:
					answer = "Unknown command";
					break;
			}

			return answer;
		}

		///////////////////
		//   Functions   //
		///////////////////

		private string _argument;
		private bool _isInitiated = false;

		public string On()
		{
			return "Power successfuly turned On";
		}

		public string Off()
		{
			return "Power successfuly turned Off";
		}

		public string Init(string arg)
		{
            if (!_isInitiated)
            {
				_argument = arg;
				_isInitiated = true;
				return "Initiated to " + arg;
            }
            else
            {
				return "Already initiated";
            }
			
		}

		public string Change(string arg)
		{
			if (_isInitiated)
			{
				_argument = arg;
				return "Changed to " + arg;
			}

			return "Need to Init first";
		}

		public string GetStatus()
		{
			return "Value is " + _argument;
		}
	}
}
