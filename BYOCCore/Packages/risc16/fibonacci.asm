; Fibonacci on the LCD, RISC-16 style
; R1 and R2 hold the last two numbers. print_number is a subroutine that prints R0 in decimal.
              CLT
              MOVI_R1    0           ; a
              MOVI_R2    1           ; b
next:         MOV_R0_R2
              BL         print_number
              MOVI_R0    ' '
              OUT
              MOV_R0_R1
              ADD_R2                 ; R0 = a + b
              BCS        done        ; carry: it does not fit in 16 bits
              MOV_R1_R2
              MOV_R2_R0
              B          next
done:         HLT

; print_number: prints R0 in decimal without leading zeros, keeping R1 to R3.
; There is no divide: each digit counts how often its place value can be subtracted.
print_number: PUSH_R1
              PUSH_R2
              PUSH_R3
              MOV_R1_R0              ; R1 = what is left of the number
              ADR_R3     places      ; R3 walks the table of place values
skip:         LDR_R3                 ; R0 = the place value
              CMPI       1
              BEQ        digits      ; the units digit is always printed
              CMP_R1
              BCS        digits      ; place < number: the first digit
              BEQ        digits      ; place = number: also the first digit
              INC_R3                 ; a leading zero, try the next place
              B          skip
digits:       LDR_R3
              MOV_R2_R0              ; R2 = the place value
              PUSH_R3                ; free R3 to count the digit
              MOVI_R3    '0'
count:        MOV_R0_R1
              CMP_R2
              BCS        show        ; borrow: less than the place value is left
              SUB_R2
              MOV_R1_R0
              INC_R3
              B          count
show:         MOV_R0_R3
              OUT
              POP_R3
              MOV_R0_R2
              CMPI       1
              BEQ        return      ; that was the units digit
              INC_R3
              B          digits
return:       POP_R3
              POP_R2
              POP_R1
              RET

places:       .WORD      10000, 1000, 100, 10, 1

