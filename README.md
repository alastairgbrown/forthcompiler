# forthcompiler

## Test cases

Forth Code                                    | Expected result
----------------------------------------------|----------------
5 0 do I . loop                               | 0 1 2 3 4
5 1 do I . loop                               | 1 2 3 4
5 0 do I . 2 +loop                            | 0 2 4
5 0 do I i 3 = if leave then loop             | 0 1 2 3
5 begin dup 0<> while dup 1 - repeat          | 5 4 3 2 1 0
5 begin dup 1 - dup 0= until                  | 5 4 3 2 1 0
0 case 1 of 10 endof 2 of 20 20 endof endcase | 
1 case 1 of 10 endof 2 of 20 20 endof endcase | 10
2 case 1 of 10 endof 2 of 20 20 endof endcase | 20 20
