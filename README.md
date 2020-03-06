# ftx

![Screenshot](/docs/screenshot.png?raw=true)

## Why?
Existing file transfer tools/protocols impose significant overhead that can dramatically increase the total time taken to move many files. This tool aims to have as little overhead as possible. I built this tool to move many, many millions of small image files from one hosting provider to another -- in a timely fashion -- without having to involve UPS/FedEx.

## How?
Less features -> less work -> less time! The tool is intended for copying a directory structure to a new location. Synchronization and incremental updates are not features of this tool. Once the transfer session is established, an uninteruppted, unidirectional stream sends the files to the client. The transfer protocol is completely static. Once the transfer begins, the only protocol-level metadata transferred is the next files length. If the bulk of data being transferred responds well to compression, gzip can be enabled. Multiple client connections are not supported.

## Security?
AES encryption can be enabled. Key exchange is facilitated by ECDH. You are on your own for integrity. The server endpoint is designed to be ephemeral; you set it up and you yourself connect the client to it. If you need additional security, there are many proxies available that can perform TLS termination/offloading (NGINX/STUNNEL for example).

## Arguments

| Name           | Value(s)                 | Descripton            
| :------------- |:-------------------------| :---------------------
| `mode`         | `Server` \| `Client`     | Self-explanatory
| `path`         | `c:\path\to\folder`      | Path to folder that gets read from/written to.
| `host`         | `some-host` \| `1.2.3.4` |  
| `port`         | `12345`                  |  Server will choose a random port if not specified.
| `compression`  | `Fastest` \| `Optimal`   | When using compression, the level is only needed on the server side.
| `encrypt`      |                          | Enables traffic encryption.
| `overwrite`    | `true` \| `false`        | Client side setting that prevents files from being overwritten. Defaults to false.

## Examples

### Basic

`ftx -mode server -path c:\source -port 12345`

`ftx -mode client -path d:\destination -host myserver -port 12345`

### Compression

`ftx -mode server -path c:\source -port 12345 -compression optimal`

`ftx -mode client -path d:\destination -host myserver -port 12345 -compression`

### Encryption

`ftx -mode server -path c:\source -port 12345 -encrypt`

`ftx -mode client -path d:\destination -host myserver -port 12345 -encrypt`

### Listen only on specific interface w/ randomly selected port

`ftx -mode server -path c:\source -host 1.2.3.4`

## Todo
- Seek out a security review
- Implement on .NET Core
- Use newer c# language features
- Resume support
- Consider:
    - pre-allocating target files
    - security implications of transfer direction
