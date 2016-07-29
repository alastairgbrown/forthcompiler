TestCase VARIABLE TestVariable 1 TestVariable ! TestVariable @	Produces 1 		EndTestCase
TestCase 2 CONSTANT TestConstant TestConstant 					Produces 2 		EndTestCase
TestCase [ 2 20 + ] CONSTANT TestConstant22 TestConstant22		Produces 22 	EndTestCase
TestCase 3 VALUE TestValue TestValue @							Produces 3 		EndTestCase
TestCase $F $B -												Produces 4 		EndTestCase
TestCase %101 													Produces 5 		EndTestCase
TestCase #6 													Produces 6 		EndTestCase
TestCase [ 2 3 * 1 + ] 											Produces 7 		EndTestCase
TestCase : def dup + ; 											Produces 		EndTestCase
TestCase 123 def 												Produces 246	EndTestCase

\ return stack operations
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
    EndMacro
	Prerequisite ":" ReturnStackCode

Macro LoopStackCode
    VARIABLE _LOOP_RS_
    EndMacro
	
Macro MultiplicationCode
	: MulDefinition \ a b -- a*b
		0 _Take3_ \ _R1_ is factor1, _R1_ is factor2, _R3_ is product
		Begin _R1_ @ 0<> While
			_R1_ @ 1 and 0<> _R2_ @ and _R3_ @ + _R3_ ! \ add to the product
			_R2_ @ dup + _R2_ ! \ LSL factor2
			_R1_ @ /lsr _R1_ ! \ LSR factor1
		repeat
		_R3_ @ _drop3_ 
	;
    EndMacro

Macro LShiftCode
	: LShiftDefinition \ x y -- z
		_Take1_
		Begin _R1_ @ 0<> While dup + _R1_ @ 1 - _R1_ ! repeat 
		_Drop1_
	;
	EndMacro
	
Macro RShiftCode
	: RShiftDefinition \ x y -- z
		_Take1_ 
		Begin _R1_ @ 0<> While /LSR _R1_ @ 1 - _R1_ ! repeat 
		_Drop1_
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
	
Optimization 0 * OptimizesTo dup xor EndOptimization
Optimization 1 * OptimizesTo EndOptimization
Optimization 2 * OptimizesTo dup + EndOptimization
Optimization 3 * OptimizesTo dup dup + + EndOptimization
Optimization 4 * OptimizesTo dup + dup + EndOptimization
Optimization 0 0 OptimizesTo /psh /psh /xor /psh EndOptimization
Optimization 0 OptimizesTo /psh /psh /xor EndOptimization
Optimization /zeq /zeq /zeq OptimizesTo /zeq EndOptimization
Optimization /zeq /zeq /zeq /zeq /zeq OptimizesTo /zeq EndOptimization

Macro + /Add EndMacro
    TestCase 1 1 + Produces 2 EndTestCase

Macro - /Sub EndMacro
    TestCase 1 1 - Produces 0 EndTestCase
    TestCase 0 1 - Produces -1 EndTestCase
    TestCase 4 2 - Produces 2 EndTestCase

Macro * MulDefinition EndMacro
    Prerequisite * ReturnStackCode
	Prerequisite * MultiplicationCode
    TestCase 2 5 * Produces 10 EndTestCase
    TestCase 10 100 * Produces 1000 EndTestCase
    TestCase 10 0 * Produces 0 EndTestCase
    TestCase 10 1 * Produces 10 EndTestCase
    TestCase 10 2 * Produces 20 EndTestCase
    TestCase 10 3 * Produces 30 EndTestCase
    TestCase 10 4 * Produces 40 EndTestCase

Macro / NotImplementedException EndMacro
Macro . NotImplementedException EndMacro

Macro = /Sub /Zeq EndMacro
    TestCase 1 1 = Produces -1 EndTestCase
    TestCase 1 0 = Produces 0 EndTestCase

Macro <> /Sub /Zeq /Zeq EndMacro
    TestCase 1 1 <> Produces 0 EndTestCase
    TestCase 1 0 <> Produces -1 EndTestCase

Macro 0= /Zeq EndMacro
    TestCase 0 0= Produces -1 EndTestCase
    TestCase 1 0= Produces 0 EndTestCase

Macro 0<> /Zeq /Zeq EndMacro
    TestCase 0 0<> Produces 0 EndTestCase
    TestCase 1 0<> Produces -1 EndTestCase

Macro _IsNegative /psh /add /psh /xor /psh /adc EndMacro
	TestCase -2 _IsNegative Produces 1 EndTestCase
	TestCase -1 _IsNegative Produces 1 EndTestCase
	TestCase 0 _IsNegative Produces 0 EndTestCase
	TestCase 1 _IsNegative Produces 0 EndTestCase
	TestCase 2 _IsNegative Produces 0 EndTestCase

Macro < - _IsNegative 0<> EndMacro
    TestCase 0 1 < Produces -1 EndTestCase
    TestCase 1 0 < Produces 0 EndTestCase
    TestCase 1 1 < Produces 0 EndTestCase

Macro > swap - _IsNegative 0<> EndMacro
    TestCase 0 1 > Produces 0 EndTestCase
    TestCase 1 0 > Produces -1 EndTestCase
    TestCase 1 1 > Produces 0 EndTestCase

Macro <= swap - _IsNegative 0= EndMacro
    TestCase 0 1 <= Produces -1 EndTestCase
    TestCase 1 0 <= Produces 0 EndTestCase
    TestCase 1 1 <= Produces -1 EndTestCase

Macro >= - _IsNegative 0= EndMacro
    TestCase 0 1 >= Produces 0 EndTestCase
    TestCase 1 0 >= Produces -1 EndTestCase
    TestCase 1 1 >= Produces -1 EndTestCase

Macro and /And EndMacro
    TestCase 127 192 and Produces 64 EndTestCase

Macro xor /Xor EndMacro
    TestCase 127 192 xor Produces 191 EndTestCase

Macro or -1 xor swap -1 xor and -1 xor EndMacro
    TestCase 127 192 or Produces 255 EndTestCase

Macro invert -1 xor EndMacro
    TestCase -1 invert Produces 0 EndTestCase
    TestCase  0 invert Produces -1 EndTestCase

Macro mod NotImplementedException EndMacro

Macro negate 0 swap - EndMacro
    TestCase 0 NEGATE Produces 0 EndTestCase
    TestCase -1 NEGATE Produces 1 EndTestCase
    TestCase 1 NEGATE Produces -1 EndTestCase

Macro abs Dup 0 < If Negate Then EndMacro
    TestCase -1 ABS Produces 1 EndTestCase
    TestCase 1 ABS Produces 1 EndTestCase

Macro min 2dup > If Swap Then drop EndMacro
    TestCase 0 1 MIN Produces 0 EndTestCase
    TestCase 1 0 MIN Produces 0 EndTestCase
    Prerequisite min ReturnStackCode

Macro max 2Dup < If Swap Then drop EndMacro
    TestCase 0 1 MAX Produces 1 EndTestCase
    TestCase 1 0 MAX Produces 1 EndTestCase
    Prerequisite max ReturnStackCode

Macro LShift LShiftDefinition EndMacro
    TestCase 16 4 lshift Produces 256 EndTestCase
    Prerequisite LShift ReturnStackCode
	Prerequisite LShift LShiftCode
	Optimization 0 lshift OptimizesTo EndOptimization
	Optimization 1 lshift OptimizesTo dup +  EndOptimization
	Optimization 2 lshift OptimizesTo dup + dup + EndOptimization
	Optimization 3 lshift OptimizesTo dup + dup + dup + EndOptimization
	Optimization 4 lshift OptimizesTo dup + dup + dup + dup + EndOptimization

Macro RShift RShiftDefinition EndMacro
    TestCase 16 4 rshift Produces 1 EndTestCase
    Prerequisite RShift ReturnStackCode
	Prerequisite RShift RShiftCode
	Optimization 0 RShift OptimizesTo EndOptimization
	Optimization 1 RShift OptimizesTo /lsr  EndOptimization
	Optimization 2 RShift OptimizesTo /lsr /lsr EndOptimization
	Optimization 3 RShift OptimizesTo /lsr /lsr /lsr EndOptimization
	Optimization 4 RShift OptimizesTo /lsr /lsr /lsr /lsr EndOptimization

Macro dup /Psh EndMacro
    TestCase 1 DUP Produces 1 1 EndTestCase

Macro ?DUP DUP DUP 0= If DROP THEN EndMacro
    TestCase 1 ?DUP Produces 1 1 EndTestCase
    TestCase 0 ?DUP Produces 0 EndTestCase

Macro DROP /Pop EndMacro
    TestCase 1 2 DROP Produces 1 EndTestCase

Macro SWAP /Swp EndMacro
    TestCase 0 1 SWAP Produces "1 0" EndTestCase

Macro OVER _Take2_ _R1_ @ _R2_ @ _R1_ @ _drop2_ EndMacro
    TestCase 1 2 OVER Produces 1 2 1 EndTestCase
    Prerequisite Over ReturnStackCode

Macro NIP /Swp /Pop EndMacro
    TestCase 1 2 NIP Produces 2 EndTestCase

Macro TUCK swap over EndMacro
    TestCase 1 2 TUCK Produces 2 1 2 EndTestCase

Macro ROT _Take3_ _R2_ @ _R3_ @ _R1_ @ _drop3_ EndMacro
    TestCase 1 2 3 ROT Produces 2 3 1 EndTestCase

Macro -ROT _Take3_ _R3_ @ _R1_ @ _R2_ @ _drop3_ EndMacro
    TestCase 1 2 3 -ROT Produces 3 1 2 EndTestCase

Macro PICK PickDefinition EndMacro
    TestCase 11 22 33 44 0 PICK Produces 11 22 33 44 44 EndTestCase
    TestCase 11 22 33 44 3 PICK Produces 11 22 33 44 11 EndTestCase
    Prerequisite pick ReturnStackCode
    Prerequisite pick LoopStackCode
    Prerequisite pick PickCode

Macro 2DUP _Take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _drop2_ EndMacro
    TestCase 1 2 2DUP Produces 1 2 1 2 EndTestCase
    Prerequisite 2dup ReturnStackCode

Macro 2DROP /Pop /Pop EndMacro
    TestCase 1 2 3 4 2DROP Produces 1 2 EndTestCase

Macro 2SWAP _Take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_ EndMacro
    TestCase 1 2 3 4 2SWAP Produces 3 4 1 2 EndTestCase
    Prerequisite 2DROP ReturnStackCode

Macro 2OVER _Take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_ EndMacro
    TestCase 1 2 3 4 2OVER Produces 1 2 3 4 1 2 EndTestCase
    Prerequisite 2OVER ReturnStackCode

Macro @ /Ldw EndMacro

Macro ! /Stw /Pop EndMacro

Macro +! dup -rot @ + swap ! EndMacro
    TestCase Variable Test_+! Produces  EndTestCase
    TestCase 1 Test_+! ! 1 Test_+! +! Test_+! @ Produces 2 EndTestCase
    TestCase 5 Test_+! ! 2 Test_+! +! Test_+! @ Produces 7 EndTestCase

Macro "If"
    Struct If Then
    0= ADDR If.END and /jnz
    EndMacro
    TestCase 1 If 88 then Produces 88 EndTestCase
    TestCase 0 If 88 then Produces  EndTestCase
    TestCase 1 If 88 else 99 then Produces 88 EndTestCase
    TestCase 0 If 88 else 99 then Produces 99 EndTestCase

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
    Struct Do Loop
    _LOOP_RS_ @ _Take3_ _RS_ @ _LOOP_RS_ !
    LABEL Do.START
    _R2_ @ _R1_ @ >= ADDR Do.END and /jnz
    EndMacro
    Prerequisite "Do" ReturnStackCode
    Prerequisite "Do" LoopStackCode
    TestCase 5 0 Do I Loop Produces 0 1 2 3 4 EndTestCase
    TestCase 5 1 Do I Loop Produces 1 2 3 4 EndTestCase

Macro "Loop"
    1 _R2_ @ + _R2_ ! 
	ADDR Do.START /jnz
    LABEL Do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
    EndStruct "Do"
    EndMacro

Macro "+Loop"
    _R2_ @ + _R2_ ! 
	ADDR Do.START /jnz 
    LABEL Do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
    EndStruct "Do"
    EndMacro
    TestCase 5 0 Do I 2 +Loop Produces 0 2 4 EndTestCase

Macro unloop
    _drop3_ 
    EndMacro

Macro I 
    _LOOP_RS_ @ 1 - @
    EndMacro

Macro J 
    _LOOP_RS_ @ 2 - @ 1 - @
    EndMacro
    TestCase 2 0 Do 12 10 Do J I Loop Loop Produces 0 10 0 11 1 10 1 11 EndTestCase

Macro Leave 
    ADDR Do.END /jnz
    EndMacro

Macro "Begin"
    Struct "Begin" "Again/Until/Repeat"
    LABEL Begin.Start
    EndMacro
    TestCase 5 Begin dup 0<> While dup 1 - repeat Produces 5 4 3 2 1 0 EndTestCase
    TestCase 5 Begin dup 1 - dup 0= until Produces 5 4 3 2 1 0 EndTestCase

Macro "Again"
    ADDR Begin.Start /jnz 
	EndStruct "Begin"
    EndMacro

Macro "Until"
    0= ADDR Begin.Start and /jnz 
	EndStruct "Begin"
    EndMacro

Macro "While"
    Struct "While" "Repeat"
    0= ADDR While.End and /jnz
    EndMacro

Macro "Repeat" 
    ADDR Begin.Start /jnz LABEL While.End 
	EndStruct "While"
	EndStruct "Begin"
    EndMacro

Macro "Case" 
    Struct Case EndCase
    _Take1_
    EndMacro
    Prerequisite "Case" ReturnStackCode
    TestCase 0 Case 1 Of 10 EndOf 2 Of 20 20 EndOf EndCase Produces  EndTestCase
    TestCase 1 Case 1 Of 10 EndOf 2 Of 20 20 EndOf EndCase Produces 10 EndTestCase
    TestCase 2 Case 1 Of 10 EndOf 2 Of 20 20 EndOf EndCase Produces 20 20 EndTestCase

Macro "Of" 
    Struct Of EndOf
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

\ These document and test the various exceptions

TestCase "" 							ProducesException "No code produced" EndTestCase
TestCase 1 1 * 							ProducesException "" EndTestCase	
TestCase "(" 							ProducesException "missing )" EndTestCase
TestCase ")" 							ProducesException ") is not defined" EndTestCase
TestCase "if again"						ProducesException "Missing Begin" EndTestCase
TestCase "if" 							ProducesException "Missing Then" EndTestCase
TestCase "if else" 						ProducesException "Missing Then" EndTestCase
TestCase "then" 						ProducesException "Missing If" EndTestCase
TestCase "else" 						ProducesException "Missing If" EndTestCase
TestCase "begin" 						ProducesException "Missing Again/Until/Repeat" EndTestCase
TestCase "begin while"					ProducesException "Missing Repeat" EndTestCase
TestCase addr LabelName 
		 label LabelName 				ProducesException "Missing LabelName" EndTestCase
TestCase addr Global.LabelName 
		 label Global.LabelName 		ProducesException "" EndTestCase
TestCase addr Global.LabelName			ProducesException "Missing Label Global.LabelName" EndTestCase
TestCase constant 						ProducesException "constant expects 1 arguments" EndTestCase
TestCase constant name 					ProducesException "missing code to evaluate" EndTestCase
TestCase [ 1 2 ] constant name 			ProducesException "constant expects 1 preceding value" EndTestCase
TestCase [ 1 2 ] allot 					ProducesException "allot expects 1 preceding value" EndTestCase
TestCase * WithCore prerequisite * y 	ProducesException "y is not defined as a Macro" EndTestCase
TestCase macro y endmacro : y ; 		ProducesException "y is already defined as a Macro" EndTestCase
TestCase : y ; macro y endmacro 		ProducesException "y is already defined as a Definition" EndTestCase
TestCase variable y macro y endmacro	ProducesException "y is already defined as a Variable" EndTestCase
TestCase NotImplementedException 		ProducesException "The method or operation is not implemented." EndTestCase
TestCase / 								ProducesException "The method or operation is not implemented." EndTestCase
TestCase y 								ProducesException "y is not defined" EndTestCase
TestCase ] 								ProducesException "Missing [" EndTestCase
TestCase [ 								ProducesException "Missing ]" EndTestCase