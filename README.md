# ftx
A file server/client.

## Why?
For those times when you need to transfer a pile o' files across a network as fast as possible.

## Arguments

| Name  | Value(s)  | Descripton |
| :------------ |:---------------| :-----|
| mode      | Server \| Client  | Self-explanatory
| path      | c:\path\to\folder        | Path to folder that gets read from/written to.
| host      | some-host \| 1.2.3.4        |  
| port      | 12345        |  
| compression  | Fastest \| Optimal | Value is not needed on client side.
| password  | Uncr@ckable | Enables AES 256-bit encryption.


## Todo
- Resume on network failures
- Resume after app crashes
- Documented examples
