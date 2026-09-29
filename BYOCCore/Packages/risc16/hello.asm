; Hello, world on the LCD, RISC-16 style
; print is a subroutine: BL puts the return address in LR and RET jumps back to it.
        CLT
        ADR_R1  msg         ; R1 points at the string
        BL      print
        HLT

; print: writes the zero terminated string at R1 to the LCD. Uses R0.
print:  LDR_R1              ; R0 = the character R1 points at
        CMPI    0
        BEQ     done        ; a 0 ends the string
        OUT
        INC_R1              ; move to the next character
        B       print
done:   RET

msg:    .WORD   "Hello from RISC-16!", 0

