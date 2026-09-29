; Hello, world with nothing but moves
; MOVE-16 has no ADD, JMP or PRINT instructions. Every line moves a value from a source port to a destination
; port, and the units act on what arrives: MEM_TO_LCD prints, MEM_TO_CMP compares, IMM_TO_JZ jumps if zero.
         IMM_TO_CLEAR  0           ; anything moved into CLEAR blanks the LCD
         IMM_TO_R1     message     ; R1 walks the string
loop:    R1_TO_MAR                 ; MEM now reads the character R1 points at
         IMM_TO_A      0           ; the function unit's A port
         MEM_TO_CMP                ; flags for 0 - the character
         IMM_TO_JZ     done        ; a 0 ends the string
         MEM_TO_LCD                ; print it
         R1_TO_A                   ; R1 + 1, the only way: through the ADD trigger
         IMM_TO_ADD    1
         RES_TO_R1
         IMM_TO_JMP    loop
done:    R0_TO_HALT                ; moving anything into HALT stops the clock

message: .WORD         "Nothing here but moves!", 0

