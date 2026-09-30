; Hello, world on the LCD, with register operands
; LDR, OUT and INC each work with any register: the register is an operand, R0 to R7, not part of the mnemonic.
        CLT
        ADR      R1, msg     ; R1 points at the string
        CALL     print
        HLT

; print: writes the zero terminated string at R1 to the LCD. Uses R0.
print:  LDR      R0, R1      ; R0 = the character R1 points at
        CMPI     R0, 0
        BEQ      done        ; a 0 ends the string
        OUT      R0
        INC      R1          ; move to the next character
        B        print
done:   RET

msg:    .STRING  "Hello from RF-16!"
