; Mandelbrot, 160 x 120
; Every point c = cx + i cy of the picture runs z = z * z + c from z = c, up to 32 steps, until z is more than 2 from
; 0. A point that never gets that far is in the Mandelbrot set and is black; the others are coloured by how soon they
; escaped. Numbers are 8.8 fixed point, 256 is 1.0, and MUL multiplies two of them in one instruction.
; The picture spans -2.5 to 1.25 across and -1.40625 to 1.40625 down, 160 points by 120, each drawn as a block of 4 by 4 pixels.
; One row of points is worked out into line first, then drawn: the screen's cursor moves right with every PLOT and
; wraps at the end of a row of pixels, so the whole screen fills in order without PX or PY.
         CLS
         MOVI   R0, 0
         ADR    R7, row
         STR    R0, R7
nextrow: ADR    R7, row
         LDR    R0, R7
         MOVI   R1, 1536     ; 256 x the step between points, 6 in 8.8
         MUL    R3, R0, R1   ; row x step
         SUBI   R3, R3, 360  ; cy = -1.40625 + row x step
         ADR    R7, cy
         STR    R3, R7
         MOVI   R0, 0
         ADR    R7, col
         STR    R0, R7
nextcol: ADR    R7, col
         LDR    R0, R7
         MOVI   R1, 1536
         MUL    R2, R0, R1
         SUBI   R2, R2, 640  ; cx = -2.5 + col x step
         ADR    R7, cy
         LDR    R3, R7
         MOV    R0, R2       ; z = c: R0 is x and R1 is y
         MOV    R1, R3
         MOVI   R6, 32       ; steps left
iter:    MUL    R4, R0, R0   ; x * x
         MUL    R5, R1, R1   ; y * y
         ADD    R7, R4, R5
         CMPI   R7, 1025
         BPL    escaped      ; x * x + y * y > 4: more than 2 from 0
         MUL    R7, R0, R1
         ADD    R1, R7, R7
         ADD    R1, R1, R3   ; y = 2 x y + cy
         SUB    R0, R4, R5
         ADD    R0, R0, R2   ; x = x * x - y * y + cx
         SUBI   R6, R6, 1
         BNE    iter
         MOVI   R7, 0        ; still close after 32 steps: in the set, black
         B      store
escaped: ADR    R7, palette
         ADD    R7, R7, R6
         LDR    R7, R7       ; the colour for how many steps were left
store:   ADR    R4, col
         LDR    R0, R4
         ADR    R5, line
         ADD    R5, R5, R0
         STR    R7, R5       ; line[col] = the colour
         ADDI   R0, R0, 1
         STR    R0, R4
         CMPI   R0, 160
         BNE    nextcol
         MOVI   R5, 4        ; draw the row: 4 lines of pixels
lines:   ADR    R4, line
         MOVI   R3, 160
points:  LDR    R7, R4
         PLOT   R7
         PLOT   R7
         PLOT   R7
         PLOT   R7
         ADDI   R4, R4, 1
         SUBI   R3, R3, 1
         BNE    points
         SUBI   R5, R5, 1
         BNE    lines
         ADR    R7, row
         LDR    R0, R7
         ADDI   R0, R0, 1
         STR    R0, R7
         CMPI   R0, 120
         BNE    nextrow
         HLT

row:     .DATA  0
col:     .DATA  0
cy:      .DATA  0
palette: .DATA  0, 0x58A0, 0x6920, 0x81C0, 0x9A60, 0xAB00, 0xC3A0, 0xDC40, 0xECC0, 0xFD61, 0xFDC5, 0xFE09, 0xF66D, 0xF6D1, 0xF735, 0xF779, 0xEFDD, 0xE7BF, 0xC71E, 0xAE7D, 0x95FD, 0x755C, 0x5CBB, 0x441A, 0x2399, 0x1B18, 0x1AB6, 0x1254, 0x11D3, 0x0971, 0x090F, 0x00AE, 0x002C
line:
