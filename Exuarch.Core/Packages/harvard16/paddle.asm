; Paddle: keep the ball in play
; The left and right arrows move the paddle, space starts a new game after a miss.
; In the Run view: switch on ⚡ Max, click the keypad's "Use the keyboard", then Run.
; Everything is drawn in 8 x 8 blocks by the block subroutine. When the paddle moves, only the block it leaves
; and the block it enters are redrawn, so a frame costs about 1500 ticks.
; Data memory: 0 ball x, 1 ball y, 2 x step, 3 y step, 4 paddle x, 5 score tens, 6 score ones, 7 keys,
; 10-12 block x, y and colour, 13 blocks left.
start:       CLS
             LDI      320
             STA      0           ; the ball starts in the middle, near the top
             LDI      40
             STA      1
             LDI      4
             STA      2           ; moving right
             STA      3           ; and down
             LDI      288
             STA      4           ; the paddle starts in the middle
             LDI      '0'
             STA      5
             STA      6
             CALL     score
; Draw the whole paddle once: eight white blocks.
             LDA      4
             STA      10
             LDI      440
             STA      11
             LDI      0xFFFF
             STA      12
             LDI      8
             STA      13
paddle:      CALL     block
             LDA      10
             ADDI     8
             STA      10
             LDA      13
             SUBI     1
             STA      13
             JNE      paddle

frame:       LDA      0           ; rub out the ball
             STA      10
             LDA      1
             STA      11
             LDI      0
             STA      12
             CALL     block
             LDA      0           ; move it
             LDB      2
             ADD
             STA      0
             LDA      1
             LDB      3
             ADD
             STA      1
             LDA      0           ; bounce off the left and right walls
             CMPI     0
             JEQ      flip_x
             CMPI     632
             JNE      walls_done
flip_x:      LDI      0
             LDB      2
             SUB
             STA      2           ; x step = -x step
walls_done:  LDA      1           ; bounce off the top
             CMPI     0
             JNE      top_done
             LDI      4
             STA      3
top_done:    LDA      1           ; at the paddle's height?
             CMPI     432
             JNE      paddle_done
             LDA      0           ; hit when ball x + 8 > paddle x ...
             ADDI     8
             LDB      4
             CMP
             JCS      paddle_done
             JEQ      paddle_done
             LDA      4           ; ... and ball x < paddle x + 64
             ADDI     64
             TAB
             LDA      0
             CMP
             JCC      paddle_done
             LDI      0
             LDB      3
             SUB
             STA      3           ; y step = -y step
             CALL     point
paddle_done: LDA      1           ; below the paddle: missed
             CMPI     472
             JEQ      game_over
             LDA      0           ; draw the ball
             STA      10
             LDA      1
             STA      11
             LDI      0xFFE0      ; yellow
             STA      12
             CALL     block

             IN                   ; read the keys once per frame
             STA      7
             ANDI     4
             JEQ      left_done
             LDA      4
             CMPI     0
             JEQ      left_done   ; already at the left wall
             ADDI     56          ; left: rub out the rightmost block ...
             STA      10
             LDI      440
             STA      11
             LDI      0
             STA      12
             CALL     block
             LDA      4           ; ... and add one on the left
             SUBI     8
             STA      4
             STA      10
             LDI      0xFFFF
             STA      12
             CALL     block
left_done:   LDA      7
             ANDI     8
             JEQ      right_done
             LDA      4
             CMPI     576
             JEQ      right_done  ; already at the right wall
             STA      10          ; right: rub out the leftmost block ...
             LDI      440
             STA      11
             LDI      0
             STA      12
             CALL     block
             LDA      4           ; ... and add one on the right
             ADDI     8
             STA      4
             ADDI     56
             STA      10
             LDI      0xFFFF
             STA      12
             CALL     block
right_done:  LDI      40          ; wait a little; raise this if the game is too fast
wait:        SUBI     1
             JNE      wait
             JMP      frame

game_over:   LBA      over_text
             CALL     print
again:       IN                   ; wait for space
             ANDI     16
             JEQ      again
             JMP      start

; point: one more point, in two decimal digits, then falls into score.
point:       LDA      6
             INA
             STA      6
             CMPI     ':'         ; past '9'
             JNE      score
             LDI      '0'
             STA      6
             LDA      5
             INA
             STA      5
; score: shows the score on the LCD.
score:       CLT
             LBA      score_text
             CALL     print
             LDA      5
             OUT
             LDA      6
             OUT
             RET

; block: draws an 8 x 8 block at (data 10, data 11) in the colour in data 12. The rows are unrolled and
; B holds the row, so a block is about 260 ticks. Changes B.
block:       LDB      11          ; B = the row being drawn
             LDA      10          ; row 1 of 8
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             LDA      10
             PX
             TBA
             PY
             LDA      12
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             PLOT
             INB
             RET

; print: prints the zero terminated string at B in program memory.
print:       LPM
             CMPI     0
             JEQ      print_done
             OUT
             INB
             JMP      print
print_done:  RET

score_text:  .STRING  "Arrows move the paddle", 10, "Score "
over_text:   .STRING  10, "Game over. Space plays again"


