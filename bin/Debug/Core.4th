TESTCASE Variable "VARIABLE TestVariable 1 TestVariable ! TestVariable @" "1"
TESTCASE CONSTANT "2 CONSTANT TestConstant TestConstant" "2"
TESTCASE CONSTANT "[ 2 20 + ] CONSTANT TestConstant22 TestConstant22" "22"
TESTCASE VALUE "3 VALUE TestValue TestValue @" "3"
TESTCASE $ "$F $B -" "4"
TESTCASE % "%101" "5"
TESTCASE # "#6" "6"
TESTCASE [] "[ 2 3 * 1 + ]" "7"
TESTCASE defintion ": def dup + ; 123 def" "246"

MACROCLASS Stack
MACRO _R1_    _RS_ @ MACROEND
MACRO _R2_    _RS_ @ 1 - MACROEND
MACRO _R3_    _RS_ @ 2 - MACROEND
MACRO _R4_    _RS_ @ 3 - MACROEND
MACRO _Take1_ _RS_ @ 1 + _RS_ ! _R1_ ! MACROEND
MACRO _Take2_ _RS_ @ 2 + _RS_ ! _R2_ ! _R1_ ! MACROEND
MACRO _Take3_ _RS_ @ 3 + _RS_ ! _R3_ ! _R2_ ! _R1_ ! MACROEND
MACRO _Take4_ _RS_ @ 4 + _RS_ ! _R4_ ! _R3_ ! _R2_ ! _R1_ ! MACROEND
MACRO _Drop1_ _RS_ @ 1 - _RS_ ! MACROEND
MACRO _Drop2_ _RS_ @ 2 - _RS_ ! MACROEND
MACRO _Drop3_ _RS_ @ 3 - _RS_ ! MACROEND
MACRO _Drop4_ _RS_ @ 4 - _RS_ ! MACROEND

MACROCLASS Organisation
MACRO ReturnStackCode
	VARIABLE _RS_ 32 ALLOT _RS_ _RS_ ! 
    VARIABLE _LOOP_RS_
    MACROEND
\	PREREQUISITE : ReturnStackCode

MACRO MultiplicationCode
	: MulDefinition \ a b -- a*b
		0 _take3_ \ _R1_ is factor1, _R1_ is factor2, _R3_ is product
		begin _R1_ @ 0<> while
			_R1_ @ 1 and 0<> _R2_ @ and _R3_ @ + _R3_ ! \ add to the product
			_R2_ @ _R2_ @ + _R2_ ! \ LSL factor2
			0 _R1_ @ /lsr /pop _R1_ ! \ LSR factor1
		repeat
		_R3_ @ _drop3_ 
	;
    MACROEND

MACRO PickCode
	: PickDefinition \  xu .. x0 u -- xu .. x0 xu
		1 + dup _RS_ @ + _RS_ !  _Take1_ \ allocate xu+2 items on the return stack
		_R1_ @ 0 do \ suck the data stack into the return stack
			_RS_ @ 4 - I - !
		loop
		_R1_ @ 0 do \ restore the data stack
			_RS_ @ 3 - _R1_ @ - I + @
		loop
		_RS_ @ _R1_ @ - @ \ get the item we want
		_RS_ @ _R1_ @ - 1 - _RS_ ! \ restore the return stack
	;
    MACROEND
MACROCLASS Math

MACRO + /Add /Pop MACROEND
    TESTCASE + "1 1 +" "2"

MACRO - /Sub /Pop MACROEND
    TESTCASE - "1 1 -" "0"

MACRO * MulDefinition MACROEND
    PREREQUISITE * ReturnStackCode
	PREREQUISITE *  MultiplicationCode
    TESTCASE * "2 5 *" "10"
    TESTCASE * "10 100 *" "1000"

MACRO / NotImplementedException MACROEND

MACRO = /Sub /Swp /Zeq /Pop MACROEND
    TESTCASE = "1 1 =" "-1"
    TESTCASE = "1 0 =" "0"

MACRO <> /Sub /Swp /Zeq /Swp /Zeq /Pop MACROEND
    TESTCASE <> "1 1 <>" "0"
    TESTCASE <> "1 0 <>" "-1"

MACRO 0= /Psh /Zeq /Pop MACROEND
    TESTCASE 0= "0 0=" "-1"
    TESTCASE 0= "1 0=" "0"

MACRO 0<> /Psh /Zeq /Swp /Zeq /Pop MACROEND
    TESTCASE 0<> "0 0<>" "0"
    TESTCASE 0<> "1 0<>" "-1"

MACRO < - drop 0 dup /adc drop 0<> MACROEND
    TESTCASE < "0 1 <" "-1"
    TESTCASE < "1 0 <" "0"
    TESTCASE < "1 1 <" "0"

MACRO > swap < MACROEND
    TESTCASE > "0 1 >" "0"
    TESTCASE > "1 0 >" "-1"
    TESTCASE > "1 1 >" "0"

MACRO <= swap >= MACROEND
    TESTCASE <= "0 1 <=" "-1"
    TESTCASE <= "1 0 <=" "0"
    TESTCASE <= "1 1 <=" "-1"

MACRO >= - drop 0 dup /adc drop 0= MACROEND
    TESTCASE >= "0 1 >=" "0"
    TESTCASE >= "1 0 >=" "-1"
    TESTCASE >= "1 1 >=" "-1"

MACRO and /And /Pop MACROEND
    TESTCASE and "127 192 and" "64"

MACRO xor /Xor /Pop MACROEND
    TESTCASE xor "127 192 xor" "191"

MACRO or -1 xor swap -1 xor and -1 xor MACROEND
    TESTCASE or "127 192 or" "255"

MACRO invert -1 xor MACROEND
    TESTCASE invert "-1 invert" "0"
    TESTCASE invert "0 invert" "-1"

MACRO mod NotImplementedException MACROEND

MACRO negate 0 swap - MACROEND
    TESTCASE negate "0 NEGATE" "0"
    TESTCASE negate "-1 NEGATE" "1"
    TESTCASE negate "1 NEGATE" "-1"

MACRO abs Dup 0 < IF Negate Then MACROEND
    TESTCASE abs "-1 ABS" "1"
    TESTCASE abs "1 ABS" "1"

MACRO min 2dup > IF Swap Then drop MACROEND
    TESTCASE min "0 1 MIN" "0"
    TESTCASE min "1 0 MIN" "0"
    PREREQUISITE min ReturnStackCode

MACRO max 2Dup < IF Swap Then drop MACROEND
    TESTCASE max "0 1 MAX" "1"
    TESTCASE max "1 0 MAX" "1"
    PREREQUISITE max ReturnStackCode

MACRO LShift _take1_ begin _R1_ @ 0<> while dup + _R1_ @ 1 - _R1_ ! repeat _drop1_ MACROEND
    TESTCASE LShift "16 4 lshift" "256"
    PREREQUISITE LShift ReturnStackCode

MACRO RShift _take1_ begin _R1_ @ 0<> while dup /LSR drop _R1_ @ 1 - _R1_ ! repeat _drop1_ MACROEND
    TESTCASE LShift "16 4 rshift" "1"
    PREREQUISITE LShift ReturnStackCode
MACROCLASS Stack
MACRO dup /Psh MACROEND
    TESTCASE dup "1 DUP" "1 1"

MACRO ?DUP DUP DUP 0= IF DROP THEN MACROEND
    TESTCASE ?dup "1 ?DUP" "1 1"
    TESTCASE ?dup "0 ?DUP" "0"

MACRO DROP /Pop MACROEND
    TESTCASE drop "1 2 DROP" "1"

MACRO SWAP /Swp MACROEND
    TESTCASE Swap "0 1 SWAP" "1 0"

MACRO OVER _Take2_ _R1_ @ _R2_ @ _R1_ @ _drop2_ MACROEND
    TESTCASE Over "1 2 OVER" "1 2 1"
    PREREQUISITE Over ReturnStackCode

MACRO NIP /Swp /Pop MACROEND
    TESTCASE nip "1 2 NIP" "2"

MACRO TUCK swap over MACROEND
    TESTCASE tuck "1 2 TUCK" "2 1 2"

MACRO ROT _take3_ _R2_ @ _R3_ @ _R1_ @ _drop3_ MACROEND
    TESTCASE rot "1 2 3 ROT" "2 3 1"

MACRO -ROT _take3_ _R3_ @ _R1_ @ _R2_ @ _drop3_ MACROEND
    TESTCASE -rot "1 2 3 -ROT" "3 1 2"

MACRO PICK PickDefinition MACROEND
    TESTCASE pick "11 22 33 44 0 PICK" "11 22 33 44 44"
    TESTCASE pick "11 22 33 44 3 PICK" "11 22 33 44 11"
    PREREQUISITE pick ReturnStackCode
    PREREQUISITE pick PickCode

MACRO 2DUP _take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _drop2_ MACROEND
    TESTCASE 2dup "1 2 2DUP" "1 2 1 2"
    PREREQUISITE 2dup ReturnStackCode

MACRO 2DROP /Pop /Pop MACROEND
    TESTCASE 2DROP "1 2 3 4 2DROP" "1 2"

MACRO 2SWAP _take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_ MACROEND
    TESTCASE 2DROP "1 2 3 4 2SWAP" "3 4 1 2"
    PREREQUISITE 2DROP ReturnStackCode

MACRO 2OVER _take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_ MACROEND
    TESTCASE 2OVER "1 2 3 4 2OVER" "1 2 3 4 1 2"
    PREREQUISITE 2OVER ReturnStackCode

MACRO @ /Ldw MACROEND

MACRO ! /Stw /Pop /Pop MACROEND

MACRO +! dup -rot @ + swap ! MACROEND
    TESTCASE +! "Variable Test_+!" ""
    TESTCASE +! "1 Test_+! ! 1 Test_+! +! Test_+! @" "2"
    TESTCASE +! "5 Test_+! ! 2 Test_+! +! Test_+! @" "7"

MACROCLASS Structure
MACRO if 
    STRUCT if
    0= addr if.END and /jnz
    MACROEND
    TESTCASE if "1 if 88 then" "88"
    TESTCASE if "0 if 88 then" ""
    TESTCASE if "1 if 88 else 99 then" "88"
    TESTCASE if "0 if 88 else 99 then" "99"

MACRO Else 
    addr if.ENDELSE /jnz label if.END
    MACROEND

MACRO Then 
    LABEL if.ENDELSE LABEL if.END STRUCTEND if
    MACROEND

MACRO Exit 
    ADDR Definition.EXIT @ code /jnz
    MACROEND

MACRO do 
    STRUCT do
    _LOOP_RS_ @ _take3_ _RS_ @ _LOOP_RS_ !
    label do.START
    _R2_ @ _R1_ @ >= addr do.END and /jnz
    MACROEND
    PREREQUISITE do ReturnStackCode
    TESTCASE do "5 0 do I loop" "0 1 2 3 4"
    TESTCASE do "5 1 do I loop" "1 2 3 4"

MACRO Loop
    1 _R2_ @ + _R2_ ! addr do.START /jnz
    label do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
    STRUCTEND do
    MACROEND

MACRO +Loop 
    _R2_ @ + _R2_ ! addr do.START /jnz 
    label do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
    STRUCTEND do
    MACROEND
    TESTCASE +loop "5 0 do I 2 +loop" "0 2 4"

MACRO unloop
    _drop3_ 
    MACROEND

MACRO I 
    _LOOP_RS_ @ 1 - @
    MACROEND

MACRO J 
    _LOOP_RS_ @ 2 - @ 1 - @
    MACROEND
    TESTCASE do "2 0 do 12 10 do J I loop loop" "0 10 0 11 1 10 1 11"

MACRO Leave 
    addr do.END /jnz
    MACROEND

MACRO Begin 
    STRUCT Begin
    label begin
    MACROEND
    TESTCASE begin "5 begin dup 0<> while dup 1 - repeat" "5 4 3 2 1 0"
    TESTCASE begin "5 begin dup 1 - dup 0= until"         "5 4 3 2 1 0"

MACRO Again 
    addr begin /jnz 
	STRUCTEND begin
    MACROEND

MACRO Until 
    0= addr begin and /jnz 
	STRUCTEND begin
    MACROEND

MACRO While 
    STRUCT While
    0= addr while and /jnz
    MACROEND

MACRO Repeat 
    addr begin /jnz label while 
	STRUCTEND while 
	STRUCTEND begin
    MACROEND

MACRO Case 
    STRUCT Case
    _Take1_
    MACROEND
    PREREQUISITE Case ReturnStackCode
    TESTCASE case "0 case 1 of 10 endof 2 of 20 20 endof endcase" " "
    TESTCASE case "1 case 1 of 10 endof 2 of 20 20 endof endcase" "10"
    TESTCASE case "2 case 1 of 10 endof 2 of 20 20 endof endcase" "20 20"

MACRO Of 
    STRUCT Of
    _R1_ @ <> addr of.END and /jnz
    MACROEND

MACRO EndOf 
    addr case.END /jnz label of.END 
	STRUCTEND of
    MACROEND

MACRO EndCase 
    label case.END _drop1_ 
	STRUCTEND case
    MACROEND
