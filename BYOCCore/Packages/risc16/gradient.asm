; Colour gradient on the screen, RISC-16 style
; Red grows from left to right and green from top to bottom. The flags from ADDI, CMPI and ANDI drive the loops.
; R1 row colour, R2 rows drawn, R3 pixel colour; R0 is the working value.
        CLS
        MOVI_R1    0x0010      ; blue at half, red and green at 0
        MOVI_R2    0
row:    MOV_R3_R1              ; colour at the left edge
level:  MOV_R0_R3
        PLOT                   ; 20 pixels of this red level, unrolled
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        PLOT
        ADDI       0x0800      ; next red level; the 32nd carries out
        MOV_R3_R0              ; MOV leaves the flags alone
        BCC        level
        MOV_R0_R2
        ADDI       1
        MOV_R2_R0
        CMPI       480
        BEQ        done
        ANDI       7           ; Z when the row count is a multiple of 8
        BNE        row
        MOV_R0_R1
        ADDI       0x0020      ; one green level
        MOV_R1_R0
        B          row
done:   HLT

