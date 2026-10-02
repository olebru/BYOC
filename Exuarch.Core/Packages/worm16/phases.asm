; Two phases
; A second loop rides along inside an egg until the first one is done. The outer HATCH carries 12 cells: the whole
; second phase, with its own HATCH and the HLT in its egg. When the first gate closes, the egg hatches, LDA_D clears N,
; and the second loop runs on its own.
        TAIL_D  end
        LDA_D   '5'
        GATE    '1'         ; the first phase, while A >= '1'
        OUT_P
        SUB_P   1
        HATCH   12          ; the second phase, unhatched until the first is done
        OUTC_D  ' '
        LDA_D   'E'
        GATE    'A'         ; the second phase, while A >= 'A'
        OUT_P
        SUB_P   1
        HATCH   1
        HLT
end:
