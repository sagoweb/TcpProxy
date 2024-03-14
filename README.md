
# TcpProxy

This is a simple TCP proxy implemented in C#. It listens for incoming connections on a specified port, 
and forwards the traffic to a specified outbound address and port.

## Features

- Listens for incoming connections on a specified port.
- Forwards traffic to a specified outbound address and port.
- Prints out the content of the messages being forwarded, both from the inbound and outbound streams, in hex, ASCII and decimal representations

## Usage

To start the proxy, run the program with two arguments: the inbound port and the outbound address and port in the format `address:port`.
This will start the proxy, listening for incoming connections on the specified port and forwarding the traffic to the specified outbound address and port.
For example, to start the proxy listening on port 8080 and forwarding traffic to `192.168.1.100:8081`, run:
TcpProxy.exe 8080 192.168.1.100:8081

## Note

This is a simple implementation and does not handle some edge cases.
