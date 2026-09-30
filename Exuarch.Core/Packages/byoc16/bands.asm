; Colour bands on the screen
; Draws 8 bands of 4 rows. The cursor moves right after each plot and wraps
; to the next row, so a band is just a count of pixels. B counts them.
              GCL
              GYI    224         ; first row
              GXI    0
; red
              LBI    0
red_band:     LRA    red         ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    red_band
; orange
              LBI    0
orange_band:  LRA    orange      ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    orange_band
; yellow
              LBI    0
yellow_band:  LRA    yellow      ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    yellow_band
; green
              LBI    0
green_band:   LRA    green       ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    green_band
; cyan
              LBI    0
cyan_band:    LRA    cyan        ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    cyan_band
; blue
              LBI    0
blue_band:    LRA    blue        ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    blue_band
; magenta
              LBI    0
magenta_band: LRA    magenta     ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    magenta_band
; white
              LBI    0
white_band:   LRA    white       ; A = the colour
              GPA                ; plot it
              INB
              LAI    2560        ; pixels in a band
              CMP
              JNE    white_band
              HLT
; RGB565 colours
red:          .DATA  0xF800
orange:       .DATA  0xFD20
yellow:       .DATA  0xFFE0
green:        .DATA  0x07E0
cyan:         .DATA  0x07FF
blue:         .DATA  0x001F
magenta:      .DATA  0xF81F
white:        .DATA  0xFFFF
