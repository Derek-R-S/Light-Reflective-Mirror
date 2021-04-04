# Light Reflective Mirror

## What
Light Reflective Mirror is a transport for Mirror Networking which relays network traffic through your own servers. This allows you to have clients host game servers and not worry about NAT/Port Forwarding, etc. There are still features I plan on adding but it still is completely stable in its current state.

## Features
* WebGL Support, WebGL can host servers!
* Built in server list!
* Relay password to stop other games from stealing your precious relay!
* Relay supports connecting users without them needing to port forward!
* NAT Punchtrough
* Direct Connecting

## Plans

For the future I plan on adding features such as:
* Multi Relay server setup for load balancing (It will split players up between multiple relay servers to make sure one single relay server isnt doing all the heavy lifting)

## How does it work?

I took a bit of a unique approach to this version and instead of using one fixed net library for the game to communicate with the standalone relay server, I instead made it use any of mirrors transports! This allows you to make it work with websockets, Ignorance(ENET), LiteNetLib, and all the others!

## Known Issues/Flaws

Disconnects from the relay will not auto reconnect **yet**. So a dedicated host is extremely recommended! Or implement your own logic to auto reconnect.

## Tutorials

(I recommend these over the text format)

### How to setup LRM on an ubuntu server
https://www.youtube.com/watch?v=0SpKIs0Beuo

### How to setup LRM in unity, along with basic usage
https://www.youtube.com/watch?v=Wi0rp2b8KmM

## Usage

Now for the juicy part, using it. Like I mentioned in the 'What' section, this is a prototype so if theres problems, please report them to me. Also PRs are also always welcomed! :)

First things first, you will need:
* Mirror, Install that from Asset Store.
* Download the latest release of Light Reflective Mirror Unity Package and put that in your project also. Download from: [Releases](https://github.com/Derek-R-S/Light-Reflective-Mirror/releases).
* Download JSON.Net from the asset store. (It's free)

#### Client Setup
Running a client is fairly straight forward, attach the LightReflectiveMirrorTransport script to your NetworkManager and set it as the transport. Put in the IP/Port of your relay server, assign LightReflectiveMirror as the Transport on the NetworkManager. Then attach the SimpleWebTransport script and assign that in the 'ClientToServerTransport' in the Light Reflective Mirror inspector. When you start a server, you can simply get the URI from the transport and use that to connect. If you wish to connect without the URI, the LightReflectiveMirror component has a public "Server ID" field which is what clients would set as the address to connect to. 

If your relay server has a password, enter it in the relayPassword field or else you wont be able to connect. By default the relays have the password as "Secret Auth Key".

##### Server List

Light Reflective Mirror has a built in room/server list if you would like to use it. To use it you need to set all the values in the 'Server Data' tab in the transport. Also if you would like to make the server show on the list, make sure "Is Public Server" is checked. Once you create a server, you can update those variables from the "UpdateRoomInfo" function on the LightReflectiveMirrorTransport script.

To request the server list you need a reference to the LightReflectiveMirrorTransport from your script and call 'RequestServerList()'. This will invoke a request to the server to update our server list. Once the response is recieved the field 'relayServerList' will be populated and you can get all the servers from there.
 
#### Server Setup
Download the latest Server release from: [Releases](https://github.com/Derek-R-S/Light-Reflective-Mirror/releases)
Make sure you have .NET Core 5.0
And all you need to do is run LRM.exe on windows, or "dotnet LRM.dll" on linux!

#### Server Config
In the config.json file there are a few fields.

TransportDLL - This is the name of the dll of the compiled transport dll.

TransportClass - The class name of the transport inside the DLL, Including namespaces!
By default, there are 5 compiled transports in the MultiCompiled dll.
To switch between them you have the following options:
* Mirror.LiteNetLibTransport
* Mirror.TelepathyTransport
* kcp2k.KcpTransport
* Mirror.SimpleWebTransport
* Mirror.MultiplexTransport

AuthenticationKey - This is the key the clients need to have on their inspector. It cannot be blank.

UpdateLoopTime - The time in miliseconds between calling 'Update' on the transport

UpdateHeartbeatInterval - the amounts of update calls before sending a heartbeat. By default its 100, which if updateLoopTime is 10, means every (10 * 100 = 1000ms) it will send out a heartbeat.


## Example

Maqsoom's Server List Example - https://github.com/maqsoom/LightReflectiveMirror-Example

## Credits

Cooper - Assisted with development and made some wonderful features! He's also active in the discord to help answer questions and help with issues.

Maqsoom & JesusLuvsYooh - Both really active testers and have been testing it since I pitched the idea. They tested almost all versions of DRM and I am sure they will test the crap out of LRM!

All Mirror Transport Creators! - They made all the transports that this thing relies on! Especially the Simple Web Transport by default!

## License
[MIT](https://choosealicense.com/licenses/mit/)
