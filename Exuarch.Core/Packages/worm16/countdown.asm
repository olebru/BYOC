; Count down, then go
; There is no jump instruction. The head runs the program and eats it, and every instruction that should run again is
; copied to the tail, so the program comes round again as the next lap. A loop is just instructions that stay alive.
; TAIL_D and LDA_D die after they run; GATE opens every lap while A >= '0'; OUT_P and SUB_P live while the gate is
; open; HATCH carries the last three instructions along, unhatched, until the gate closes, and then lets them run.
        TAIL_D  end         ; the tail starts just after the program
        LDA_D   '9'         ; A = '9', once
        GATE    '0'         ; while A >= '0', open the lap
        OUT_P               ; print A
        SUB_P   1
        HATCH   5           ; the 5 cells below ride along until the gate closes
        OUTC    'G'
        OUTC    'O'
        HLT
end:
