; Colour gradient on the screen
; Red grows from left to right and green from top to bottom, with a little blue.
; Variables in the MMU bank: 0 the row's colour, 1 rows drawn, 2 rows since green last changed.
          GCL
          LAI   0x0010      ; blue at half, red and green at 0
          STA   0
          LAI   0
          STA   1
          STA   2
row:      LDA   0           ; A = colour at the left edge
level:    GPA               ; 20 pixels of this red level
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          GPA
          LBI   0x0800      ; one red level
          ADD
          JC    next_row    ; carry: the 32nd level wrapped, the row is full
          JMP   level
next_row: LDA   1
          INA
          STA   1           ; rows drawn
          LBI   480
          CMP
          JEQ   done
          LDA   2
          INA
          STA   2
          LBI   8           ; every 8 rows
          CMP
          JNE   row
          LAI   0
          STA   2
          LDA   0
          LBI   0x0020      ; one green level
          ADD
          STA   0
          JMP   row
done:     HLT
