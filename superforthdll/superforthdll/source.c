#define WIN32_MEAN_AND_LEAN
#include <windows.h>

#include <stdlib.h>
#include "common.h"
#include "error.h"
#include "debug.h"
#include "ast.h"
#include "compiler.h"
#include "machine.h"
#include "file.h"
#include "stdlib.h"
#include "ffi.h"

static superforth_error_t last_err = SUPERFORTH_ERROR_NONE;

BOOL WINAPI DllMain(
    __in  HINSTANCE hinstDLL,
    __in  DWORD fdwReason,
    __in  LPVOID lpvReserved
) {
    switch (fdwReason) {
    case DLL_PROCESS_ATTACH:
        break;
    case DLL_PROCESS_DETACH:
        break;
    case DLL_THREAD_ATTACH:
        break;
    case DLL_THREAD_DETACH:
        break;
    }
    return TRUE;
}

uint16_t count_machine_ins(machine_ins_t* machine_ins) {
    machine_ins_t* start = machine_ins;
    while (!(machine_ins->op_code == MACHINE_OP_CODE_ABORT && machine_ins->a == SUPERFORTH_ERROR_NONE))
        machine_ins++;
    return (machine_ins - start) + 1;
}

DLLEXPORT superforth_error_t get_last_err() {
    return last_err;
}

DLLEXPORT void superforth_free_object(machine_ins_t* instructions) {
    free(instructions);
}

DLLEXPORT ast_t* superforth_parse(const char* source_file, int print_trace) {
    ast_parser_t parser;
    if (!init_ast_parser(&parser, source_file)) {
        last_err = parser.last_err;
        return NULL;
    }

    ast_t* ast = malloc(sizeof(ast_t));
    if (!ast) {
        last_err = SUPERFORTH_ERROR_MEMORY;
        free_ast_parser(&parser);
        return NULL;
    }

    if (!init_ast(ast, &parser)) {
        last_err = parser.last_err;
        if(print_trace)
            print_error_trace(parser.multi_scanner);
        free_ast_parser(&parser);
        return NULL;
    }
    free_ast_parser(&parser);

    return ast;
}

DLLEXPORT machine_ins_t* superforth_compile(machine_t* target_machine, ast_t* ast) {
    compiler_t compiler;
    if (!compile(&compiler, target_machine, ast)) {
        last_err = compiler.last_err;
        return NULL;
    }

    machine_ins_t* machine_ins = malloc(compiler.ins_builder.instruction_count * sizeof(machine_ins_t));
    if (!machine_ins) {
        last_err = SUPERFORTH_ERROR_MEMORY;
        free(compiler.ins_builder.instructions);
        return NULL;
    }

    compiler_ins_to_machine_ins(compiler.ins_builder.instructions, machine_ins, compiler.ins_builder.instruction_count);
    free(compiler.ins_builder.instructions);

    install_stdlib(target_machine);
    return machine_ins;
}

DLLEXPORT int superforth_save_ins(machine_ins_t* machine_ins, ast_t* ast, machine_t* targeted_machine, const char* file_path) {
    if (!file_save_compiled(file_path, ast, targeted_machine, machine_ins, count_machine_ins(machine_ins))) {
        return 0;
    }
    return 1;
}

DLLEXPORT machine_ins_t* superforth_load_ins(machine_t* target_machine, const char* file_path) {
    uint16_t instruction_count;
    machine_ins_t* instructions = file_load_ins(file_path, target_machine, &instruction_count);
    if (!instructions) {
        last_err = SUPERFORTH_ERROR_CANNOT_OPEN_FILE;
        return NULL; //failed to read instructions
    }

    //check if the program aborts correctly at the reported end 
    if (instructions[instruction_count - 1].op_code != MACHINE_OP_CODE_ABORT || instructions[instruction_count - 1].a != SUPERFORTH_ERROR_NONE) {
        last_err = SUPERFORTH_ERROR_INTERNAL;
        return NULL;
        //if it doesn't it could be a spoof to make count_machine_ins run forever
    }

    install_stdlib(target_machine);
    return instructions;
}

DLLEXPORT void superforth_print_ins(machine_ins_t* instructions) {
    print_instructions(instructions, count_machine_ins(instructions));
}