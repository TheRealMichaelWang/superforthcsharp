using System;
using System.Runtime.InteropServices;

namespace SuperForth
{
    public enum Error
    {
        None,
        Memory,
        Internal,
        UnexpectedToken,
        Readonly,
        TypeNotAllowed,
        Undeclared,
        Redeclaration,
        UnexpectedType,
        UnexpectedArgumentSize,
        CannotReturn,
        CannotContinue,
        CannotBreak,
        IndexOutOfRange,
        DivideByZero,
        StackOverflow,
        ReadUninitializedMemory,
        UnreturnedFunction,
        Abort,
        Foreign,
        CannotOpenFile
    }
    
    public sealed class SuperForthException : Exception
    {
        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr get_err_msg(Error error);

        public readonly Error Error;

        public SuperForthException(Error error) : base("A superforth error occured: " + Marshal.PtrToStringAnsi(get_err_msg(error)))
        {
            this.Error = error;
        }

        public SuperForthException(Error error, ulong runtimeInstructionAddr) : base("A superforth runtime error occured (IP:"+runtimeInstructionAddr+"): " + Marshal.PtrToStringAnsi(get_err_msg(error)))
        {
            this.Error = error;
        }
    }

    public sealed class SuperForthInstance : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MachineHeapAllocation
        {
            public enum GCTraceMode
            {
                None,
                All,
                Some
            }

            public MachineRegister this[int i]
            {
                get
                {
                    if (i < 0 || i > Limit)
                        throw new SuperForthException(Error.IndexOutOfRange);
                    if (Marshal.ReadInt32(IntPtr.Add(this.InitStat, i * sizeof(int))) == 0)
                        throw new SuperForthException(Error.ReadUninitializedMemory);
                    return Marshal.PtrToStructure<MachineRegister>(IntPtr.Add(this.Registers, i * Marshal.SizeOf<MachineRegister>()));
                }
                set
                {
                    if (i < 0 || i > Limit)
                        throw new SuperForthException(Error.IndexOutOfRange);
                    Marshal.WriteInt32(IntPtr.Add(this.InitStat, i * sizeof(int)), 1);
                    Marshal.StructureToPtr(value, IntPtr.Add(this.Registers, i * Marshal.SizeOf<MachineRegister>()), true);
                }
            }

            private readonly IntPtr Registers;
            private readonly IntPtr InitStat;
            private readonly IntPtr TraceStat;

            readonly ushort Limit;
            private readonly int GCFlag, RegisteredWithTable, PreFreed;

            private readonly GCTraceMode TraceMode;

            public void ConfigureCustomGCTraceSchema(params bool[] traceProperty)
            {
                if (this.TraceMode != GCTraceMode.Some)
                    throw new InvalidOperationException("Invalid gc trace mode: Must be gc_trace_some to configure custom trace mode.");
                if (traceProperty.Length != this.Limit)
                    throw new ArgumentException("Unexpected trace property count.");
                
                for(int i = 0; i < traceProperty.Length; i++)
                {
                    IntPtr dest = IntPtr.Add(this.TraceStat, i * sizeof(int));
                    if (traceProperty[i])
                        Marshal.WriteInt32(dest, 1);
                    else
                        Marshal.WriteInt32(dest, 0);
                }
            }

            public string GetString()
            {
                char[] buffer = new char[Limit];
                for (int i = 0; i < Limit; i++)
                    buffer[i] = this[i].CharInt;
                return new string(buffer);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct MachineRegister
        {
            [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr machine_alloc(ref Machine machine, ushort req_size, MachineHeapAllocation.GCTraceMode traceMode);

            [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
            private static extern void machine_heap_supertrace(ref Machine machine, IntPtr heapAllocationPtr);

            [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
            private static extern void machine_heap_detrace(ref Machine machine, IntPtr heapAllocationPtr);

            public static MachineRegister FromChar(char c)
            {
                MachineRegister machineRegister = new MachineRegister();
                machineRegister.CharInt = c;
                return machineRegister;
            }

            public static MachineRegister FromInt(int i)
            {
                MachineRegister machineRegister = new MachineRegister();
                machineRegister.LongInt = i;
                return machineRegister;
            }

            public static MachineRegister FromFloat(double d)
            {
                MachineRegister machineRegister = new MachineRegister();
                machineRegister.FloatInt = d;
                return machineRegister;
            }

            public static MachineRegister FromBool(bool b)
            {
                MachineRegister machineRegister = new MachineRegister();
                machineRegister.BoolFlag = b;
                return machineRegister;
            }

            public static MachineRegister NewHeapAlloc(ref Machine machine, int size, MachineHeapAllocation.GCTraceMode traceMode)
            {
                if (size < 0)
                    throw new ArgumentException("Size of heap allocation must be >= 0.");
                MachineRegister machineRegister = new MachineRegister();
                machineRegister.heapAllocationPtr = machine_alloc(ref machine, (ushort)size, traceMode);
                return machineRegister;
            }

            public static MachineRegister NewHeapAlloc(SuperForthInstance superForthInstance, int size, MachineHeapAllocation.GCTraceMode traceMode) => NewHeapAlloc(ref superForthInstance.VMInstance, size, traceMode);

            public static MachineRegister FromString(ref Machine machine, string s)
            {
                MachineRegister machineRegister = NewHeapAlloc(ref machine, s.Length, MachineHeapAllocation.GCTraceMode.None);
                MachineHeapAllocation machineHeapAllocation = machineRegister.HeapAllocation;
                for (int i = 0; i < s.Length; i++)
                    machineHeapAllocation[i] = MachineRegister.FromChar(s[i]);
                return machineRegister;
            }

            public static MachineRegister FromString(SuperForthInstance superForthInstance, string s) => FromString(ref superForthInstance.VMInstance, s);

            public MachineHeapAllocation HeapAllocation
            {
                get => Marshal.PtrToStructure<MachineHeapAllocation>(this.heapAllocationPtr);
            }

            [FieldOffset(0)]
            private IntPtr heapAllocationPtr;

            [FieldOffset(0)]
            public long LongInt;

            [FieldOffset(0)]
            public double FloatInt;

            [FieldOffset(0)]
            public char CharInt;

            [FieldOffset(0)]
            private int boolInt;

            public bool BoolFlag
            {
                get => boolInt == 1;
                set => boolInt = value ? 1 : 0;
            }

            public void GCKeepAlive(ref Machine machine) => machine_heap_supertrace(ref machine, this.heapAllocationPtr);
            public void GCRelease(ref Machine machine) => machine_heap_detrace(ref machine, this.heapAllocationPtr);

            public void GCKeepAlive(SuperForthInstance superForthInstance) => GCKeepAlive(ref superForthInstance.VMInstance);
            public void GCRelease(SuperForthInstance superForthInstance) => GCRelease(ref superForthInstance.VMInstance);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Machine
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate int ForeignFunction(ref Machine machine, ref MachineRegister Input, ref MachineRegister Output);

            [StructLayout(LayoutKind.Sequential)]
            struct FFITable
            {
                IntPtr FunctionTable;

                readonly ushort FuncionCount, AllocatedFunctions;

                [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
                public static extern int ffi_include_func(ref FFITable fFITable, ForeignFunction foreignFunction);
            }

            private readonly IntPtr Stack, Positions, HeapAllocs, HeapFrameBounds, HeapTraces, HeapTraceBounds, FreedHeapAllocs;

            public readonly ulong LastErrorInstructionPointer;
            public readonly Error LastError;

            private readonly ushort GlobalOffset, PositionCount, HeapFrame, FrameLimit, HeapCount, AllocedHeapAllocs, TraceCount, AllocedTraceAllocs, FreedHeapCount, FreedHeapAlloc;

            private FFITable ffi;
            private readonly IntPtr DynamicLibraryTable;

            private int HaltFlag;

            public bool IsPaused() => HaltFlag == 1;

            public void AddNewFFIFunction(ForeignFunction foreignFunction)
            {
                if(FFITable.ffi_include_func(ref this.ffi, foreignFunction) == 0)
                {
                    throw new SuperForthException(Error.Internal);
                }
            }

            public void Halt()
            {
                if (IsPaused())
                    throw new InvalidOperationException("Virtual machine has already been halted.");
                HaltFlag = 1;
            }

            public void Resume()
            {
                if (!IsPaused())
                    throw new InvalidOperationException("Virtual machine has not been halted.");
                HaltFlag = 0;
            }
        }

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Error get_last_err();

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void superforth_free_object(IntPtr superforthObj);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_ast(IntPtr superforthObj);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_machine(ref Machine machine);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr superforth_parse([MarshalAs(UnmanagedType.LPStr)] string filePath, int print_trace);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr superforth_compile(ref Machine machine, IntPtr ast);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int superforth_save_ins(IntPtr machineIns, IntPtr ast, ref Machine machine, [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr superforth_load_ins(ref Machine machine, [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int machine_execute(ref Machine machine, IntPtr instructions, IntPtr continueInstruction);

        [DllImport("superforthdll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void superforth_print_ins(IntPtr instructions);

        public static void Compile(string sourceFile, string outputFile)
        {
            if (!(sourceFile.EndsWith(".txt") || sourceFile.EndsWith(".sf")))
                throw new ArgumentException("Source file has invalid extension.");
            if (!(outputFile.EndsWith("bin") || !outputFile.Contains(".")))
                throw new ArgumentException("Output file has invalid extension.");

            IntPtr ast = superforth_parse(sourceFile, 1);
            if(ast == IntPtr.Zero)
                throw new SuperForthException(get_last_err());

            Machine machine = new Machine();
            IntPtr instructions = superforth_compile(ref machine, ast);

            if (instructions == IntPtr.Zero)
            {
                free_ast(ast);
                superforth_free_object(ast);
                throw new SuperForthException(get_last_err());
            }

            if(superforth_save_ins(instructions, ast, ref machine, outputFile) == 0)
            {
                free_machine(ref machine);
                superforth_free_object(instructions);
                free_ast(ast);
                superforth_free_object(ast);
                throw new SuperForthException(Error.Internal);
            }

            free_machine(ref machine);
            superforth_free_object(instructions);
            free_ast(ast);
            superforth_free_object(ast);
        }

        public bool IsPaused { get => VMInstance.IsPaused(); }

        IntPtr MachineInstructions;
        Machine VMInstance;

        private bool disposed;
        private volatile bool startedHalting;

        public SuperForthInstance(string filePath)
        {
            this.VMInstance = new Machine();
            this.disposed = false;
            this.startedHalting = false;

            if (filePath.EndsWith(".bin") || !filePath.Contains("."))
            {
                MachineInstructions = superforth_load_ins(ref this.VMInstance, filePath);
                if (MachineInstructions == IntPtr.Zero)
                {
                    this.disposed = true;
                    throw new SuperForthException(get_last_err());
                }
            }
            else if (filePath.EndsWith(".sf") || filePath.EndsWith(".txt"))
            {
                IntPtr ast = superforth_parse(filePath, 0);
                if (ast == IntPtr.Zero)
                {
                    this.disposed = true;
                    throw new SuperForthException(get_last_err());
                }

                this.MachineInstructions = superforth_compile(ref this.VMInstance, ast);
                free_ast(ast);
                superforth_free_object(ast);
                if (this.MachineInstructions == IntPtr.Zero)
                {
                    this.disposed = true;
                    throw new SuperForthException(get_last_err());
                }
            }
            else
            {
                this.disposed = true;
                throw new ArgumentException("Filepath \"" + filePath + "\" isn't a valid superforth source or binary.");
            }
        }

        ~SuperForthInstance()
        {
            Dispose();
        }

        public void AddForeignFunction(Machine.ForeignFunction foreignFunction) => this.VMInstance.AddNewFFIFunction(foreignFunction);

        public void Run()
        {
            if (this.disposed)
                throw new ObjectDisposedException("superforthinstance");
            if(machine_execute(ref this.VMInstance, this.MachineInstructions, this.MachineInstructions) == 0)
            {
                SuperForthException runtimeError = new SuperForthException(this.VMInstance.LastError, this.VMInstance.LastErrorInstructionPointer);
                this.Dispose();
                throw runtimeError;
            }
            if(!this.VMInstance.IsPaused())
                this.Dispose();
            this.startedHalting = false;
        }

        public void Pause()
        {
            if (this.disposed)
                throw new ObjectDisposedException("superforthinstance");
            if (startedHalting)
                return;
            this.startedHalting = true;
            this.VMInstance.Halt();
        }

        public void Resume()
        {
            ThreadResume();
            ResumeExecute();
        }

        public void ThreadResume()
        {
            if (this.disposed)
                throw new ObjectDisposedException("superforthinstance");
            if (!this.IsPaused)
                throw new InvalidOperationException();
            while(startedHalting) { }
            this.VMInstance.Resume();
        }
       
        public void ResumeExecute()
        {
            if (machine_execute(ref this.VMInstance, this.MachineInstructions, (IntPtr)this.VMInstance.LastErrorInstructionPointer) == 0)
            {
                SuperForthException runtimeError = new SuperForthException(this.VMInstance.LastError, this.VMInstance.LastErrorInstructionPointer);
                this.Dispose();
                throw runtimeError;
            }

            if (!this.VMInstance.IsPaused())
                this.Dispose();
            startedHalting = false;
        }

        public void PrintInstructions()
        {
            if (this.disposed)
                throw new ObjectDisposedException("superforthinstance");
            superforth_print_ins(this.MachineInstructions);
        }

        public void Dispose()
        {
            if (this.disposed)
                return;
            this.disposed = true;
            
            superforth_free_object(this.MachineInstructions);
            free_machine(ref this.VMInstance);
        }
    }
}
