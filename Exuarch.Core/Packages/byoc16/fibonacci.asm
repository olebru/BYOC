; Fibonacci on the LCD
; Prints each Fibonacci number in decimal until the next one does not fit in 16 bits.
; Variables in the MMU bank: 0 a, 1 b, 2 next, 3 value to print, 4 digit, 5 digit printed.
        DCL
        LAI   0
        STA   0           ; a = 0
        LAI   1
        STA   1           ; b = 1
next:   LDA   1           ; print b
        STA   3
        LAI   0
        STA   5           ; nothing printed yet
; digit for 10000s: count how often it can be subtracted
        LAI   '0'
        STA   4
count0: LDA   3
        LBI   10000
        SUB
        JC    digit0      ; borrow: value < place value
        STA   3
        LDA   4
        INA
        STA   4
        JMP   count0
digit0: LDA   4
        LBI   '0'
        CMP
        JNE   print0      ; not a zero: print it
        LDA   5
        LBI   1
        CMP
        JNE   skip0       ; leading zero: skip it
print0: LDA   4
        DWA
        LAI   1
        STA   5
skip0:
; digit for 1000s: count how often it can be subtracted
        LAI   '0'
        STA   4
count1: LDA   3
        LBI   1000
        SUB
        JC    digit1      ; borrow: value < place value
        STA   3
        LDA   4
        INA
        STA   4
        JMP   count1
digit1: LDA   4
        LBI   '0'
        CMP
        JNE   print1      ; not a zero: print it
        LDA   5
        LBI   1
        CMP
        JNE   skip1       ; leading zero: skip it
print1: LDA   4
        DWA
        LAI   1
        STA   5
skip1:
; digit for 100s: count how often it can be subtracted
        LAI   '0'
        STA   4
count2: LDA   3
        LBI   100
        SUB
        JC    digit2      ; borrow: value < place value
        STA   3
        LDA   4
        INA
        STA   4
        JMP   count2
digit2: LDA   4
        LBI   '0'
        CMP
        JNE   print2      ; not a zero: print it
        LDA   5
        LBI   1
        CMP
        JNE   skip2       ; leading zero: skip it
print2: LDA   4
        DWA
        LAI   1
        STA   5
skip2:
; digit for 10s: count how often it can be subtracted
        LAI   '0'
        STA   4
count3: LDA   3
        LBI   10
        SUB
        JC    digit3      ; borrow: value < place value
        STA   3
        LDA   4
        INA
        STA   4
        JMP   count3
digit3: LDA   4
        LBI   '0'
        CMP
        JNE   print3      ; not a zero: print it
        LDA   5
        LBI   1
        CMP
        JNE   skip3       ; leading zero: skip it
print3: LDA   4
        DWA
        LAI   1
        STA   5
skip3:
; units digit, then a space
        LDA   3
        LBI   '0'
        ADD
        DWA
        DWI   ' '
; next = a + b, stop when it does not fit
        LDA   0
        LDB   1
        ADD
        JC    done
        STA   2
        LDA   1
        STA   0           ; a = b
        LDA   2
        STA   1           ; b = next
        JMP   next
done:   HLT
