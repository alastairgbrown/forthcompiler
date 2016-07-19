TestCase Variable 1 VARIABLE TestVariable 1 TestVariable ! TestVariable @ EndTestCase
TestCase CONSTANT 2 2 CONSTANT TestConstant TestConstant EndTestCase
TestCase CONSTANT 22 [ 2 20 + ] CONSTANT TestConstant22 TestConstant22 EndTestCase
TestCase VALUE 3 3 VALUE TestValue TestValue @ EndTestCase
TestCase $ 4 $F $B - EndTestCase
TestCase % 5 %101 EndTestCase
TestCase # 6 #6 EndTestCase
TestCase [] "7" [ 2 3 * 1 + ] EndTestCase
TestCase definition "" : def dup + ; EndTestCase
TestCase definition "246" 123 def EndTestCase

Macro _R1_    _RS_ @ EndMacro
Macro _R2_    _RS_ @ 1 - EndMacro
Macro _R3_    _RS_ @ 2 - EndMacro
Macro _R4_    _RS_ @ 3 - EndMacro
Macro _Take1_ _RS_ @ 1 + _RS_ ! _R1_ ! EndMacro
Macro _Take2_ _RS_ @ 2 + _RS_ ! _R2_ ! _R1_ ! EndMacro
Macro _Take3_ _RS_ @ 3 + _RS_ ! _R3_ ! _R2_ ! _R1_ ! EndMacro
Macro _Take4_ _RS_ @ 4 + _RS_ ! _R4_ ! _R3_ ! _R2_ ! _R1_ ! EndMacro
Macro _Drop1_ _RS_ @ 1 - _RS_ ! EndMacro
Macro _Drop2_ _RS_ @ 2 - _RS_ ! EndMacro
Macro _Drop3_ _RS_ @ 3 - _RS_ ! EndMacro
Macro _Drop4_ _RS_ @ 4 - _RS_ ! EndMacro

Macro ReturnStackCode
	VARIABLE _RS_ 32 ALLOT _RS_ _RS_ ! 
    VARIABLE _LOOP_RS_
    EndMacro
	Prerequisite ":" ReturnStackCode
	
Macro MultiplicationCode
	: MulDefinition \ a b -- a*b
		0 _Take3_ \ _R1_ is factor1, _R1_ is factor2, _R3_ is product
		Begin _R1_ @ 0<> While
			_R1_ @ 1 and 0<> _R2_ @ and _R3_ @ + _R3_ ! \ add to the product
			_R2_ @ _R2_ @ + _R2_ ! \ LSL factor2
			_R1_ @ /lsr _R1_ ! \ LSR factor1
		repeat
		_R3_ @ _drop3_ 
	;
    EndMacro

Macro PickCode
	: PickDefinition \  xu .. x0 u -- xu .. x0 xu
		1 + dup _RS_ @ + _RS_ !  _Take1_ \ allocate xu+2 items on the return stack
		_R1_ @ 0 Do \ suck the data stack into the return stack
			_RS_ @ 4 - I - !
		Loop
		_R1_ @ 0 Do \ restore the data stack
			_RS_ @ 3 - _R1_ @ - I + @
		Loop
		_RS_ @ _R1_ @ - @ \ get the item we want
		_RS_ @ _R1_ @ - 1 - _RS_ ! \ restore the return stack
	;
    EndMacro

Macro + /Add EndMacro
    TestCase + "2" 1 1 + EndTestCase

Macro - /Sub EndMacro
    TestCase - "0" 1 1 - EndTestCase
    TestCase - "-1" 0 1 - EndTestCase
    TestCase - "2" 4 2 - EndTestCase

Macro * MulDefinition EndMacro
    Prerequisite * ReturnStackCode
	Prerequisite * MultiplicationCode
    TestCase * "10" 2 5 * EndTestCase
    TestCase * "1000" 10 100 * EndTestCase

Macro / NotImplementedException EndMacro
Macro . NotImplementedException EndMacro

Macro = /Sub /Zeq EndMacro
    TestCase = "-1" 1 1 = EndTestCase
    TestCase = "0" 1 0 = EndTestCase

Macro <> /Sub /Zeq /Zeq EndMacro
    TestCase <> "0" 1 1 <> EndTestCase
    TestCase <> "-1" 1 0 <> EndTestCase

Macro 0= /Zeq EndMacro
    TestCase 0= "-1" 0 0= EndTestCase
    TestCase 0= "0" 1 0= EndTestCase

Macro 0<> /Zeq /Zeq EndMacro
    TestCase 0<> "0" 0 0<> EndTestCase
    TestCase 0<> "-1" 1 0<> EndTestCase

Macro _IsNegative $80000000 - /psh /xor /psh /adc EndMacro
	TestCase _IsNegative 1 -2 _IsNegative EndTestCase
	TestCase _IsNegative 1 -1 _IsNegative EndTestCase
	TestCase _IsNegative 0 0 _IsNegative EndTestCase
	TestCase _IsNegative 0 1 _IsNegative EndTestCase
	TestCase _IsNegative 0 2 _IsNegative EndTestCase

Macro < - _IsNegative 0<> EndMacro
    TestCase < "-1" 0 1 < EndTestCase
    TestCase < "0" 1 0 < EndTestCase
    TestCase < "0" 1 1 < EndTestCase

Macro > swap < EndMacro
    TestCase > "0" 0 1 > EndTestCase
    TestCase > "-1" 1 0 > EndTestCase
    TestCase > "0" 1 1 > EndTestCase

Macro <= swap >= EndMacro
    TestCase <= "-1" 0 1 <= EndTestCase
    TestCase <= "0" 1 0 <= EndTestCase
    TestCase <= "-1" 1 1 <= EndTestCase

Macro >= - _IsNegative 0= EndMacro
    TestCase >= "0" 0 1 >= EndTestCase
    TestCase >= "-1" 1 0 >= EndTestCase
    TestCase >= "-1" 1 1 >= EndTestCase

Macro and /And EndMacro
    TestCase and "64" 127 192 and EndTestCase

Macro xor /Xor EndMacro
    TestCase xor "191" 127 192 xor EndTestCase

Macro or -1 xor swap -1 xor and -1 xor EndMacro
    TestCase or "255" 127 192 or EndTestCase

Macro invert -1 xor EndMacro
    TestCase invert "0" -1 invert EndTestCase
    TestCase invert "-1" 0 invert EndTestCase

Macro mod NotImplementedException EndMacro

Macro negate 0 swap - EndMacro
    TestCase negate "0" 0 NEGATE EndTestCase
    TestCase negate "1" -1 NEGATE EndTestCase
    TestCase negate "-1" 1 NEGATE EndTestCase

Macro abs Dup 0 < If Negate Then EndMacro
    TestCase abs "1" -1 ABS EndTestCase
    TestCase abs "1" 1 ABS EndTestCase

Macro min 2dup > If Swap Then drop EndMacro
    TestCase min "0" 0 1 MIN EndTestCase
    TestCase min "0" 1 0 MIN EndTestCase
    Prerequisite min ReturnStackCode

Macro max 2Dup < If Swap Then drop EndMacro
    TestCase max "1" 0 1 MAX EndTestCase
    TestCase max "1" 1 0 MAX EndTestCase
    Prerequisite max ReturnStackCode

Macro LShift _Take1_ Begin _R1_ @ 0<> While dup + _R1_ @ 1 - _R1_ ! repeat _drop1_ EndMacro
    TestCase LShift "256" 16 4 lshift EndTestCase
    Prerequisite LShift ReturnStackCode

Macro RShift _Take1_ Begin _R1_ @ 0<> While /LSR _R1_ @ 1 - _R1_ ! repeat _drop1_ EndMacro
    TestCase LShift "1" 16 4 rshift EndTestCase
    Prerequisite LShift ReturnStackCode

Macro dup /Psh EndMacro
    TestCase dup "1 1" 1 DUP EndTestCase

Macro ?DUP DUP DUP 0= If DROP THEN EndMacro
    TestCase ?dup "1 1" 1 ?DUP EndTestCase
    TestCase ?dup "0" 0 ?DUP EndTestCase

Macro DROP /Pop EndMacro
    TestCase drop "1" 1 2 DROP EndTestCase

Macro SWAP /Swp EndMacro
    TestCase Swap "1 0" 0 1 SWAP EndTestCase

Macro OVER _Take2_ _R1_ @ _R2_ @ _R1_ @ _drop2_ EndMacro
    TestCase Over "1 2 1" 1 2 OVER EndTestCase
    Prerequisite Over ReturnStackCode

Macro NIP /Swp /Pop EndMacro
    TestCase nip "2" 1 2 NIP EndTestCase

Macro TUCK swap over EndMacro
    TestCase tuck "2 1 2" 1 2 TUCK EndTestCase

Macro ROT _Take3_ _R2_ @ _R3_ @ _R1_ @ _drop3_ EndMacro
    TestCase rot "2 3 1" 1 2 3 ROT EndTestCase

Macro -ROT _Take3_ _R3_ @ _R1_ @ _R2_ @ _drop3_ EndMacro
    TestCase -rot "3 1 2" 1 2 3 -ROT EndTestCase

Macro PICK PickDefinition EndMacro
    TestCase pick "11 22 33 44 44" 11 22 33 44 0 PICK EndTestCase
    TestCase pick "11 22 33 44 11" 11 22 33 44 3 PICK EndTestCase
    Prerequisite pick ReturnStackCode
    Prerequisite pick PickCode

Macro 2DUP _Take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _drop2_ EndMacro
    TestCase 2dup "1 2 1 2" 1 2 2DUP EndTestCase
    Prerequisite 2dup ReturnStackCode

Macro 2DROP /Pop /Pop EndMacro
    TestCase 2DROP "1 2" 1 2 3 4 2DROP EndTestCase

Macro 2SWAP _Take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_ EndMacro
    TestCase 2DROP "3 4 1 2" 1 2 3 4 2SWAP EndTestCase
    Prerequisite 2DROP ReturnStackCode

Macro 2OVER _Take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_ EndMacro
    TestCase 2OVER "1 2 3 4 1 2" 1 2 3 4 2OVER EndTestCase
    Prerequisite 2OVER ReturnStackCode

Macro @ /Ldw EndMacro

Macro ! /Stw /Pop EndMacro

Macro +! dup -rot @ + swap ! EndMacro
    TestCase +! "" Variable Test_+! EndTestCase
    TestCase +! "2" 1 Test_+! ! 1 Test_+! +! Test_+! @ EndTestCase
    TestCase +! "7" 5 Test_+! ! 2 Test_+! +! Test_+! @ EndTestCase

Macro "If"
    Struct "If"
    0= ADDR If.END and /jnz
    EndMacro
    TestCase "If" "88" 1 If 88 then EndTestCase
    TestCase "If" "" 0 If 88 then EndTestCase
    TestCase "If" "88" 1 If 88 else 99 then EndTestCase
    TestCase "If" "99" 0 If 88 else 99 then EndTestCase

Macro "Else"
    ADDR If.ENDELSE /jnz LABEL If.END
    EndMacro

Macro "Then"
    LABEL If.ENDELSE LABEL If.END 
	EndStruct "If"
    EndMacro

Macro "Exit"
    ADDR Definition.EXIT @ code /jnz
    EndMacro

Macro "Do"
    Struct "Do"
    _LOOP_RS_ @ _Take3_ _RS_ @ _LOOP_RS_ !
    LABEL Do.START
    _R2_ @ _R1_ @ >= ADDR Do.END and /jnz
    EndMacro
    Prerequisite "Do" ReturnStackCode
    TestCase "Do" "0 1 2 3 4" 5 0 Do I Loop EndTestCase
    TestCase "Do" "1 2 3 4" 5 1 Do I Loop EndTestCase

Macro "Loop"
    1 _R2_ @ + _R2_ ! ADDR Do.START /jnz
    LABEL Do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
    EndStruct "Do"
    EndMacro

Macro "+Loop"
    _R2_ @ + _R2_ ! ADDR Do.START /jnz 
    LABEL Do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
    EndStruct "Do"
    EndMacro
    TestCase "+Loop" "0 2 4" 5 0 Do I 2 +Loop EndTestCase

Macro unloop
    _drop3_ 
    EndMacro

Macro I 
    _LOOP_RS_ @ 1 - @
    EndMacro

Macro J 
    _LOOP_RS_ @ 2 - @ 1 - @
    EndMacro
    TestCase "I+J" "0 10 0 11 1 10 1 11" 2 0 Do 12 10 Do J I Loop Loop EndTestCase

Macro Leave 
    ADDR Do.END /jnz
    EndMacro

Macro "Begin"
    Struct "Begin"
    LABEL Begin.Start
    EndMacro
    TestCase "Begin" "5 4 3 2 1 0" 5 Begin dup 0<> While dup 1 - repeat EndTestCase
    TestCase "Begin" "5 4 3 2 1 0" 5 Begin dup 1 - dup 0= until EndTestCase

Macro "Again"
    ADDR Begin.Start /jnz 
	EndStruct "Begin"
    EndMacro

Macro "Until"
    0= ADDR Begin.Start and /jnz 
	EndStruct "Begin"
    EndMacro

Macro "While"
    Struct "While"
    0= ADDR While.End and /jnz
    EndMacro

Macro "Repeat" 
    ADDR Begin.Start /jnz LABEL While.End 
	EndStruct "While"
	EndStruct "Begin"
    EndMacro

Macro "Case" 
    Struct "Case"
    _Take1_
    EndMacro
    Prerequisite "Case" ReturnStackCode
    TestCase "Case" " "  0 Case 1 Of 10 EndOf 2 Of 20 20 EndOf EndCase EndTestCase
    TestCase "Case" "10" 1 Case 1 Of 10 EndOf 2 Of 20 20 EndOf EndCase EndTestCase
    TestCase "Case" "20 20" 2 Case 1 Of 10 EndOf 2 Of 20 20 EndOf EndCase EndTestCase

Macro "Of" 
    Struct "Of"
    _R1_ @ <> ADDR Of.END and /jnz
    EndMacro

Macro "EndOf"
    ADDR Case.END /jnz LABEL Of.END 
	EndStruct "Of"
    EndMacro

Macro "EndCase"
    LABEL Case.END _drop1_ 
	EndStruct "Case"
    EndMacro

