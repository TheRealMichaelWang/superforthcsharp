# SuperForth-CSharp
This project aims to provide seamless interoperability between native-SuperForth, and C# a signifigant reduction in performance or flexebility. 

## Key Features:
* Interoperability with Microsoft's .NET libraries.
  * Other projects like [the Iron Languages](https://github.com/IronLanguages/ironpython3) reimplement their respective languages in C# for .NET interoperability, often at a high performance cost. 
  * Because SuperForth is written in C#, the only real performance costs are marshalling/interoperation between SuperForth's C backend and it's C# frontend wrapper. Therfore SuperForth scripting in C# is much more performant than many other alternatives implemented directly in C#.
* Security, and Code Trust
  * SuperForth CSharp is designed with the zero-trust principle in mind: It assumes all SuperForth binaries are potentially malicious.
    * Extra bounds checking with memory access/writing.
    * Extra runtime type-checking to ensure that the binary doesn't crash the instance fatally
      * That means no C-like segfaults or other C errors.
  * SuperForth's FFI can restrict and control what functinalities to give to the script.
* Interoperability with SuperForth
  * Interact with superforth code via SuperForth's FFI
    * Possible use cases include performant plugin scripting in one's C# app, game, or server.
  * You can read more about superforth's many powerful features [here](https://github.com/TheRealMichaelWang/superforth#features).
    * SuperForth supports parametric polymorphism with both records and functions.
    * SuperForth is a functional programming language, designed around the first-class anonymous function.
