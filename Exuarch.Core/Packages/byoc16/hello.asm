; Hello, world on the LCD
; B indexes the string, A holds each character.
        DCL                ; clear the display
        LBI    0           ; B = 0
loop:   LNA    msg         ; A = msg[B]
        DWA                ; print A
        INB                ; next character
        LAI    13          ; length of the string
        CMP                ; Z is set when B = 13
        JNE    loop
        DWI    '\n'        ; new line
        DWI    'Æ'
        DWI    'Ø'
        DWI    'Å'
        DWI    ' '
        DWI    'æ'
        DWI    'ø'
        DWI    'å'
        HLT
msg:    .DATA  "HELLO, WORLD!"
