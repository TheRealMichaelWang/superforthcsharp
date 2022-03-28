# SuperForth-CSharp
This project aims to provide seamless interoperability between native-SuperForth, and C# a signifigant reduction in performance or flexebility. 

## Key Features:
* Interoperability with Microsoft's .NET libraries.
  * Other projects like [the Iron Languages](https://github.com/IronLanguages/ironpython3) reimplement their respective languages in C# for .NET interoperability, often at a high performance cost. 
  * Because SuperForth is written in C#, the only real performance costs are marshalling/interoperation between SuperForth's C backend and it's C# frontend wrapper. Therfore SuperForth scripting in C# is much more performant than many other alternatives implemented directly in C#.
* Interoperability with SuperForth
  * Interact with superforth code via SuperForth's FFI
    * Possible use cases include performant plugin scripting in one's C# app, game, or server.
