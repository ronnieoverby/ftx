# ftx

![Screenshot](/docs/screenshot.png?raw=true)

## Why?
Moving files across the internet sucks. Especially if your moving a lot of files.

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
- Resume
