; Fibonacci on the LCD, with register operands
; The same program as RISC-16's, with eight registers to go round: print_number has R4 to R7 to itself, so it
; saves nothing on the stack.
              CLT
              MOVI   R1, 0       ; a
              MOVI   R2, 1       ; b
next:         MOV    R0, R2
              CALL   print_number
              MOVI   R0, ' '
              OUT    R0
              MOV    R0, R1
              ADD    R0, R2      ; R0 = a + b
              BCS    done        ; carry: it does not fit in 16 bits
              MOV    R1, R2
              MOV    R2, R0
              B      next
done:         HLT

; print_number: prints R0 in decimal without leading zeros. Uses R4 to R7.
; There is no divide: each digit counts how often its place value can be subtracted.
print_number: MOV    R4, R0      ; R4 = what is left of the number
              ADR    R5, places  ; R5 walks the table of place values
skip:         LDR    R6, R5      ; R6 = the place value
              CMPI   R6, 1
              BEQ    digits      ; the units digit is always printed
              CMP    R6, R4
              BCS    digits      ; place < number: the first digit
              BEQ    digits      ; place = number: also the first digit
              INC    R5          ; a leading zero, try the next place
              B      skip
digits:       LDR    R6, R5      ; R6 = the place value
              MOVI   R7, '0'     ; R7 counts the digit
count:        CMP    R4, R6
              BCS    show        ; borrow: less than the place value is left
              SUB    R4, R6
              INC    R7
              B      count
show:         OUT    R7
              CMPI   R6, 1
              BEQ    return      ; that was the units digit
              INC    R5
              B      digits
return:       RET

places:       .DATA  10000, 1000, 100, 10, 1
