# ftx
A file server/client.

## Why?
For those times when you need to transfer a pile o' files across a network easily and quickly.

## Arguments

| Name  | Value(s)  | Descripton |
| :------------ |:---------------| :-----|
| mode      | `Server` \| `Client`  | Self-explanatory
| path      | `c:\path\to\folder`        | Path to folder that gets read from/written to.
| host      | `some-host` \| `1.2.3.4`        |  
| port      | `12345`        |  Server will choose a random port if not specified.
| compression  | `Fastest` \| `Optimal` | When using compression, the level is only needed on the server side.
| password  | `Uncr@ckable` | Enables traffic encryption.
| overwrite  | `true` \| `false` | Client side setting that prevents files from being overwritten. Defaults to false.

## Examples

### Basic

`ftx -mode server -path c:\source -port 12345`

`ftx -mode client -path d:\destination -host myserver -port 12345`



## Todo
- Resume
- Documented examples
