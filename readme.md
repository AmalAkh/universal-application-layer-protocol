# Universal Application Layer Protocol

Description of the protocol header and other details can be found here:
[Docs](docs/docs.pdf)

## Installation

1. Install .NET SDK for you system 
2. ```git clone https://github.com/AmalAkh/universal-application-layer-protocol```
3. ```cd universal-application-layer-protocol```
4. ```dotnet run . ```

## Features
- Dissector for Wireshark
- Selective Repeat ARQ
- Cross-platform
- Uses CRC16 for data integrity
- Support of Positive and Negative acknowledgements
- Supports both text and file messages

## Usage

Connection to the peer
```
connect 127.0.0.1 −p 5050
```
- -p: target port

Disconnection from the peer 
```
disconnect
```
Send simple text message 
```
send -fs 10 -err
```
- -fs: fragment size
- -err: simulate error 
Then input text to be sent

Send file:
```
sendfile -fs 10 -err path/to/myfile 
```





