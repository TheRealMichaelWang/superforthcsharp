#pragma once

#ifndef DEBUG_H
#define DEBUG_H

#include "common.h"
#include "machine.h"
#include "scanner.h"
#include "error.h"

void print_instructions(machine_ins_t* ins, uint16_t ins_count);
DLLEXPORT const char* get_err_msg(superforth_error_t superforth_error);

void print_error_trace(multi_scanner_t multi_scanner);

#endif // !DEBUG_h
