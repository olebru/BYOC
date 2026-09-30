; Stack and memory
; Pushes values on the stack in MMU bank 0 and adds two values stored after the code.
        LAI    15          ; A = 15
        PSA                ; push A
        PSA                ; and again
        LRA    letter      ; A = the cell at 'letter'
        LRB    one         ; B = the cell at 'one'
        PSA
loop:   ADD                ; A = A + B
        PSA                ; push the sum
        JMP    loop        ; forever: the stack grows down and wraps round bank 0
letter: .DATA  65          ; 'A'
one:    .DATA  1
