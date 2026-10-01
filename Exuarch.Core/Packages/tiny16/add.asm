; 2 + 3
; Characters are numbers: '2' is 50, and 50 + 3 is 53, the character '5'.
        LOAD  '2'
        OUT
        LOAD  '+'
        OUT
        LOAD  '3'
        OUT
        LOAD  '='
        OUT
        LOAD  '2'
        ADD   3           ; A = '2' + 3 = '5'
        OUT
        HALT
