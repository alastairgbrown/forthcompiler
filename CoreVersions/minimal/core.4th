( small opcode set minimal forth )

$10         Constant    .LDW 
$11         Constant    .STW 
$12         Constant    .PSH 
$13         Constant    .POP 
$14         Constant    .SWP 
$15         Constant    .JNZ 
$16         Constant    .JSR
$17         Constant    .ADC
$18         Constant    .AND 
$19         Constant    .XOR 
$1a         Constant    .LSR 
$1b         Constant    .ZEQ 

\ $1c         Constant    .ADD 
\ $1d         Constant    .SUB 
\ $1e         Constant    .IOR

\ [ $1f $00 ] Constant    .MLT 
\ [ $1f $01 ] Constant    .RTO
\ [ $1f $02 ] Constant    .TOR 
\ [ $1f $03 ] Constant    .LSP 
\ [ $1f $04 ] Constant    .LRP

macro @     ( a -- m[a] )  /ldw endmacro
macro dup   ( a -- a a )   /psh endmacro
macro drop  ( a -- )       /pop endmacro
macro swap  ( a b -- b a ) /swp endmacro
macro jump  ( n -- )       /jnz endmacro
macro call  ( n -- pc,s )  /jsr endmacro
macro +c    ( a b -- b+a+c ) /adc endmacro
macro and   ( a b -- a&b ) /and endmacro
macro xor   ( a b -- a^b ) /xor endmacro
macro 2/    ( a -- a>>1,c ) /lsr endmacro
macro 0=    ( a -- a=0 )   /zeq EndMacro

macro "::" IsDefinition Label {Label} EndMacro
macro ";;" jump EndMacro
