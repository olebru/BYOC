; Sketch with the arrow keys, RISC-16 style
; The arrow keys move the pen and space changes its colour. In the Run view, click the keypad to use the keyboard.
; R1 x, R2 y, R3 colour; R0 is the working value. The program runs until you stop it.
           CLS
           MOVI_R1    320
           MOVI_R2    240
           MOVI_R3    0xFFE0      ; yellow
draw:      MOV_R0_R1
           PX
           MOV_R0_R2
           PY
           MOV_R0_R3
           PLOT
           IN                     ; R0 = the keys: 1 up, 2 down, 4 left, 8 right, 16 space
           PUSH_R0                ; keep them for each test
           ANDI       1
           BEQ        not_up
           MOV_R0_R2
           CMPI       0
           BEQ        not_up      ; already at the top
           DEC_R2
not_up:    POP_R0
           PUSH_R0
           ANDI       2
           BEQ        not_down
           MOV_R0_R2
           CMPI       479
           BEQ        not_down    ; already at the bottom
           INC_R2
not_down:  POP_R0
           PUSH_R0
           ANDI       4
           BEQ        not_left
           MOV_R0_R1
           CMPI       0
           BEQ        not_left
           DEC_R1
not_left:  POP_R0
           PUSH_R0
           ANDI       8
           BEQ        not_right
           MOV_R0_R1
           CMPI       639
           BEQ        not_right
           INC_R1
not_right: POP_R0
           ANDI       16
           BEQ        pause
           MOV_R0_R3
           ADDI       0x18E3      ; a little more red, green and blue
           MOV_R3_R0
pause:     MOVI_R0    60          ; wait a moment so the pen does not race at full speed
wait:      SUBI       1
           BNE        wait
           B          draw

