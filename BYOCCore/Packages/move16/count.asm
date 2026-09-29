; Count from 00 to 99, moving into CALL
; A subroutine call is a move into the CALL port: it jumps and leaves the return address in R3, and moving R3
; into JMP returns. R2 and R1 hold the tens and ones digits as characters.
        IMM_TO_CLEAR  0
        IMM_TO_R2     '0'
        IMM_TO_R1     '0'
next:   IMM_TO_CALL   show
        R1_TO_A                   ; ones + 1
        IMM_TO_ADD    1
        RES_TO_R1
        IMM_TO_A      ':'         ; the character after '9'
        R1_TO_CMP
        IMM_TO_JNZ    next
        IMM_TO_R1     '0'         ; ones wrapped: tens + 1
        R2_TO_A
        IMM_TO_ADD    1
        RES_TO_R2
        IMM_TO_A      ':'
        R2_TO_CMP
        IMM_TO_JNZ    next
        R0_TO_HALT

; show: prints the two digits and a space, then returns through R3.
show:   R2_TO_LCD
        R1_TO_LCD
        IMM_TO_LCD    ' '
        R3_TO_JMP

