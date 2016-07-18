# forthcompiler

# The Current Op Code Set
Hex Value | Mnemonic | Description            | Dataflow
----------|----------|------------------------|----------
0         | NOP      | No Operation           | no
1         | LDW      | Load Word              | top = mem(top);
2         | STW      | Store Word             | mem(top) = next; pop;
3         | PSH      | Push Top               | push;
4         | POP      | Pop Top                | pop;
5         | SWP      | Swap Top, Next         | tmp = top; top = next; next = tmp;
6         | JNZ      | Jump if Next Not ZERO  | if (next!=0) {pc = top;} pop;
7         | JSR      | Jump Subroutine        | tmp = top; top = pc; pc = tmp;
8         | ADD      | Add                    | next = next + top; cf = cout; pop;
9         | ADC      | Add with Carry         | next = next + top + cf; cf = cout; pop;
A         | SUB      | Subtract Top from Next | next = next + (~top) + 1; cf = cout; pop;
B         | AND      | And                    | next = next & top; pop;
C         | XOR      | Xor                    | next = next ^ top; pop;
D         | LSR      | Logic Shift Right      | cf = (top & 0x1); top = (top >> 1);
E         | ZEQ      | Equal Zero Test        | if (top==0){top = 0xfffff...ff;} else {top = 0x0;}
F         | LIT      | Push Literal           | push; top = mem(pc);pc = pc + 1;

Stack Function  | Dataflow
----------------|--------------------------------------------|
push defined as | mem(sp+1) = next; next = top; sp = sp + 1; |
pop defined as  | top = next; next = mem(sp); sp = sp - 1;   |

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

```forth
: mul ( a b -- a*b )
   a ! b ! 0 product !
   begin a @ 0<> while
     a @ 1 and 0<> if b @ product +! then
     b @ b +!
     a @ 1 rshift a !
   repeat
   product @
   ;

```
