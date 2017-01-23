( -------------------------------------------------------------------------- )
( lang.4th )
variable _RSP 32 Allot
:: _INIT_FORTH _RSP _RSP /stw /pop /pop ;;

( -------------------------------------------------------------------------- )
macro 1+ ( n -- n+1 ) 1 2/ +c endmacro
macro ! ( Data Address -- ) /stw drop drop endmacro
macro not ( a -- ~a ) 0= endmacro
macro 1- ( n -- n-1 ) 0 2/ not +c endmacro
macro rsp_push ( n -- ) _RSP dup @ 1+ swap /stw drop ! endmacro
macro rsp_pop ( -- n ) _RSP dup @ @ swap dup @ 1- swap ! endmacro
macro ":" isdefinition label {Label} rsp_push endmacro
macro ";" rsp_pop jump endmacro
:: >r ( n -- ) swap rsp_push ;;
:: r> ( -- n ) rsp_pop swap ;; 
: - ( x y -- x-y ) 1 2/ not xor +c 0 dup +c not 2/ drop ;
macro invc ( -- ) 0 dup +c not 2/ drop endmacro ( invert carry )
macro or ( a b -- a|b ) dup >r swap dup >r xor r> r> and + endmacro
macro + ( x y -- x+y,c ) 0 2/ drop +c endmacro
macro reti ( r -- ) swap swap jump endmacro
macro = ( a b -- a==b ) xor 0= endmacro
macro 0<> ( num -- num!=0 ) 0= not endmacro
macro <> ( a b -- a!=b ) xor 0<> endmacro
macro 2* ( n -- n<<1 ) 0 2/ drop dup +c endmacro
macro rol ( n -- n<<1|c ) dup +c endmacro
macro invert ( num -- ~num ) -1 xor endmacro
macro negate ( num -- -num ) 0 swap - endmacro
macro true ( -- 1=1 ) 0 not endmacro
macro false ( -- 1=0 ) 0 endmacro
macro "if" struct if then 0= addr if.end and jump endmacro
macro "else" addr if.endelse jump label if.end endmacro
macro "then" label if.endelse label if.end endstruct "if" endmacro
macro "exit" addr definition.exit jump endmacro
macro "begin" struct "begin" "again/until/repeat" label begin.start endmacro
macro "again" addr begin.start jump endstruct "begin" endmacro
macro "until" 0= addr begin.start and jump endstruct "begin" endmacro
macro "while" struct "while" "repeat" 0= addr while.end and jump endmacro
macro "repeat" addr begin.start jump label while.end endstruct "while" endstruct "begin" endmacro
macro _r1 ( -- r1 ) _RSP @ endmacro
macro _r2 ( -- r2 ) _RSP @ 1- endmacro
macro _r3 ( -- r3 ) _RSP @ 2 - endmacro
macro "do" ( end start -- ) struct "do" "loop"
   >r >r
   label do.start
   _RSP @ dup @ swap 1- @ <=
   if addr do.end jump then
endmacro
macro "loop" ( -- ) _RSP @ 1- dup @ 1+ swap ! addr do.start jump
   label do.end _RSP @ 2 - _RSP !
   endstruct "do" 
endmacro
macro "i" _RSP @ 1- @ endmacro
macro "j" _RSP @ 3 - @ endmacro

( signed inequalities )
macro 0>= ( n -- n>=0 ) 0 2/ drop dup +c dup xor dup +c 0= endmacro
macro > ( a b -- a>b ) swap - 0>= not endmacro
macro < (a b -- a<b ) - 0>= not endmacro
macro <= ( a b -- a<=b ) swap - 0>= endmacro
macro >= ( a b -- a>=b ) - 0>= endmacro

( stack functions )
: over ( a b -- a b a ) swap dup >r swap r> ;
macro rot ( a b c -- b c a ) >r swap r> swap endmacro
macro 2>r ( a b -- ) >r >r endmacro
macro 2r> ( -- a b ) r> r> endmacro
macro 2dup ( a b -- a b a b ) over over endmacro
macro 2drop ( a b -- ) drop drop endmacro
macro 2swap ( a b c d -- c d a b ) >r rot rot r> rot rot endmacro
macro 2over ( a b c d -- a b c d a b ) 2>r 2dup 2swap 2r> endmacro
macro 2+ ( a b c d -- ab+cd ) swap >r + r> swap >r +c r> endmacro
: lshift ( x y -- x<<y ) 
   begin
      dup 0<>
   while
      1- swap 2* swap
   repeat
   drop ;
: rshift ( x y -- x>>y )
   begin
      dup 0<>
   while
      1- Swap 2/ Swap
   repeat
   drop ;

