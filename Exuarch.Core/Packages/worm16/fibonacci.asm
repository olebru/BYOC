; Fibonacci
; A and B hold two numbers of the sequence in a row. Each lap prints A in decimal with OUT2, adds the two with ADDB,
; and moves them along with SWAP, so A is the next number and B the one after it. UPTO is a gate that is open while
; A <= 89, the last number with two digits; when it closes, HATCH lets HLT out.
        TAIL_D  end
        LDA_D   0           ; 0 and 1, the first two numbers
        LDB_D   1
        UPTO    89          ; open while A <= 89
        HATCH   1           ; HLT rides along until the gate closes
        HLT
        OUT2    10, '0'     ; print A in decimal
        OUTC    ' '
        ADDB                ; A = A + B
        SWAP                ; A = the next number to print, B = the one after it
end:
