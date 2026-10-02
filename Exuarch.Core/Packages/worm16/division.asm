; Cell division
; OUTC_2 divides: it prints a star, and two copies of it go to the tail. So the next lap has two stars, then four, then
; eight, and the worm grows a little every lap. GATE stops it after four laps, and HATCH lets HLT out.
        TAIL_D  end
        LDA_D   4           ; four laps
        GATE    1           ; open while A >= 1
        HATCH   1           ; HLT rides along until the gate closes
        HLT
        OUTC_2  '*'         ; one star, two copies of it in the next lap
        OUTC    ' '
        SUB     1
end:
