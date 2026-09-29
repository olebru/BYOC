; XOR texture: every pixel is x XOR y
; 128 x 128 values drawn at twice the size, 256 x 256 in the middle of the screen. Each value costs a dozen
; moves, so it builds up slowly: watch it at ⚡ Max. R0 is x, R1 is y, R2 is the screen row.
        IMM_TO_CLS    0
        IMM_TO_R1     0
        IMM_TO_R2     112
row:    IMM_TO_X      192
        R2_TO_Y
        IMM_TO_R0     0
pixel:  R0_TO_A
        R1_TO_XOR                 ; RES = x XOR y
        RES_TO_A
        IMM_TO_SHL    9           ; into the red and green bits
        RES_TO_PIXEL              ; each value is two pixels wide
        RES_TO_PIXEL
        R0_TO_A                   ; x + 1
        IMM_TO_ADD    1
        RES_TO_R0
        IMM_TO_A      128
        R0_TO_CMP
        IMM_TO_JNZ    pixel
        R2_TO_A                   ; next screen row
        IMM_TO_ADD    1
        RES_TO_R2
        RES_TO_A
        IMM_TO_AND    1           ; each value is two rows high: only move on to the next y after an odd row
        IMM_TO_JNZ    row
        R1_TO_A                   ; y + 1
        IMM_TO_ADD    1
        RES_TO_R1
        IMM_TO_A      128
        R1_TO_CMP
        IMM_TO_JNZ    row
        R0_TO_HALT

