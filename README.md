# UDP server client

Protocol:
 Client to Serverr commands:		<$><command><*><args><\n>
 Server to Client response:		sent as a string, client waits for the entier message to arrive
 	
 * $       - signal start of command 
 * command - given a special Commans enum, is actually an index according of its position in enum (easy for future extra commands that will be nneded) 
 * args    - i used a string, because if needed, it is easy to convert to other types 
 * \n      - signal end of commans 
 
 The protocl is not exposed to the end user. The user just needs to do is set a server, a client, and sent an SE.Commands command.
 The implementation uses the protocol in order to make sure all commands arriving safely (becasue UDP doesn't provide it by itself).
 Even if only a partial command arrives to the server, it will "append" it, and invoke only a full valid command. 
 Any unknow commands that answer the protocol will returne "Unknow command".
 
 The SE src files are written in a Library project and while building the project a DLL file is exported to /project/bin folder 
 Any application asks to use this library must add a reference to the se.dll file 
 Classes in the se.dll - 
 
 * SE.Server			 - Server side implementation 
 * SE.Client			 - Client side implementation
 * SE.Commands		 - Enum that holds commands names 
 * SE.CommandExecuter - a class in the server side, implements the commands parsing and invoking of functions, also holds the functions implementation. 
 	                      that way the server-client can be used with future different protocols, 
                        user will only need giving the server a referense of a different CommandExecuter 
 					              Also - client side return a dynamic type, so it will be a bit easier to update it if needed to any desire type. 
 
  I've used Server and Client are in different classes so if needed, can be splitted into different src files, same for dll files, so user will add only the needed dll. 
 
  I have also added an option for the server to use broadcasting (default = false, also in the test i've added i don't use it). 
  that way no need to define the address in order to connect, but the server will boradcast and the client will listen to the boradcast signal
  than the client will be able to listen the boradcast (doing so, he be able to get server's address) and what is being boradcast is the port, now the client stops listen the     boradcst anc connects with the server at address:port.
  This option might not work when using a network switch. 
  
   For testing i built a windows forms app that uses the dll files (i'll send you the project with a referense to the SE project instead of dll files so it won't minde the path to the dll's).
   The use of the dll's is visible in the Test project.
   Also - you can play with the app directly.
   
   Functions implementation - you didnt mentiones a lot, so i only made sure you can change a value only after using init. the rest simply return "successfuly.."
