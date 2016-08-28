Macro "Region" EndMacro
Macro "EndRegion" EndMacro

Region \ General test cases

    TestCase Variable TestVariable 1 TestVariable ! TestVariable @  Produces 1      EndTestCase
    TestCase 2 Constant TestConstant TestConstant                   Produces 2      EndTestCase
    TestCase [ 2 20 + ] Constant TestConstant22 TestConstant22      Produces 22     EndTestCase
    TestCase [ TestConstant22 10 * ]                                Produces 220    EndTestCase
    TestCase 3 VALUE TestValue TestValue @                          Produces 3      EndTestCase
    TestCase $0F $0B -                                              Produces 4      EndTestCase
    TestCase %101                                                   Produces 5      EndTestCase
    TestCase #6                                                     Produces 6      EndTestCase
    TestCase [ 2 3 * 1 + ]                                          Produces 7      EndTestCase
    TestCase [ 21 3 / 1 + ]                                         Produces 8      EndTestCase
    TestCase : Def Dup + ;                                                          EndTestCase
    TestCase 123 Def                                                Produces 246    EndTestCase
    TestCase Include "IncludableFileForTestCases.4th"                               EndTestCase
    TestCase IncludedConstant                                       Produces 24601  EndTestCase
    
EndRegion

Region \ memory operations

    Macro @ /Ldw EndMacro
    Macro ! /Stw /Pop /Pop EndMacro
    Macro +! Dup -Rot @ + Swap ! EndMacro
	
    TestCase Variable TestIncrement EndTestCase
    TestCase 1 TestIncrement ! 1 TestIncrement +! TestIncrement @ Produces 2 EndTestCase
    TestCase 5 TestIncrement ! 2 TestIncrement +! TestIncrement @ Produces 7 EndTestCase
    
EndRegion

Region \ Return stack operations
    Macro ReturnStackCode \ Prerequisite code for return stack operations
        Variable _RS_ \ A pointer to the top of the return stack
        32 Allot _RS_ _RS_ ! 
    EndMacro

    Macro LoopStackCode \ Prerequisite code for loops
        Variable _RS_Loop_ \ A pointer to the current loop variables for getting values for I And J
    EndMacro

	Macro R@ _RS_ @ @ EndMacro
	Macro >R _RS_ @ 1 + _RS_ /Stw /Pop /Stw /Pop /Pop  EndMacro
	Macro R> R@ _RS_ @ 1 - _RS_ ! EndMacro
	Prerequisite R@ ReturnStackCode
	Prerequisite >R ReturnStackCode
	Prerequisite R> ReturnStackCode

    Macro _R1_    _RS_ @ EndMacro
    Macro _R2_    _RS_ @ 1 - EndMacro
    Macro _R3_    _RS_ @ 2 - EndMacro
    Macro _R4_    _RS_ @ 3 - EndMacro
    Macro _R5_    _RS_ @ 4 - EndMacro
    Macro _R6_    _RS_ @ 5 - EndMacro
    Macro _Take1_ _RS_ @ 1 + _RS_ ! _R1_ ! EndMacro
    Macro _Take2_ _RS_ @ 2 + _RS_ ! _R2_ ! _R1_ ! EndMacro
    Macro _Take3_ _RS_ @ 3 + _RS_ ! _R3_ ! _R2_ ! _R1_ ! EndMacro
    Macro _Take4_ _RS_ @ 4 + _RS_ ! _R4_ ! _R3_ ! _R2_ ! _R1_ ! EndMacro
    Macro _Take5_ _RS_ @ 5 + _RS_ ! _R5_ ! _R4_ ! _R3_ ! _R2_ ! _R1_ ! EndMacro
    Macro _Drop1_ _RS_ @ 1 - _RS_ ! EndMacro
    Macro _Drop2_ _RS_ @ 2 - _RS_ ! EndMacro
    Macro _Drop3_ _RS_ @ 3 - _RS_ ! EndMacro
    Macro _Drop4_ _RS_ @ 4 - _RS_ ! EndMacro
    Macro _Drop5_ _RS_ @ 5 - _RS_ ! EndMacro
    Macro _Drop6_ _RS_ @ 6 - _RS_ ! EndMacro
    
    Optimization _Take1_ _R1_ @ OptimizesTo dup _Take1_ IsLastPass EndOptimization
    \ Optimization _Drop1_ _R1_ @ _Drop1_ OptimizesTo _R2_ @ _Drop2_ IsLastPass EndOptimization
    Optimization _Drop2_ _R1_ @ _Drop1_ OptimizesTo _R3_ @ _Drop3_ IsLastPass EndOptimization
    Optimization _Drop3_ _R1_ @ _Drop1_ OptimizesTo _R4_ @ _Drop4_ IsLastPass EndOptimization
    Optimization _Drop4_ _R1_ @ _Drop1_ OptimizesTo _R5_ @ _Drop5_ IsLastPass EndOptimization
    Optimization _Drop5_ _R1_ @ _Drop1_ OptimizesTo _R6_ @ _Drop6_ IsLastPass EndOptimization
    Optimization _R1_ @ Drop OptimizesTo  EndOptimization
    
EndRegion

Region \ Definition operators
    
    Macro ":" IsDefinition \ Start Standard defintion
        Struct Definition ";"
        Addr Definition.Skip /Jnz 
        Label {Label} 
        >R
    EndMacro
    Prerequisite ":" ReturnStackCode

    Macro ";" \ End Standard defintion
        Label Definition.Exit 
        R> /Jnz 
        Label Definition.Skip
        EndStruct Definition
    EndMacro

    Macro "::" IsDefinition \ Start Simplified defintion
        Struct Definition ";;"
        Addr Definition.Skip /Jnz 
        Label {Label} 
    EndMacro

    Macro ";;" \ End Simplified defintion
        Label Definition.Exit /Jnz 
        Label Definition.Skip
        EndStruct Definition
    EndMacro
    
    TestCase :: SimpleDefinition Swap Dup + Swap ;;     EndTestCase
    TestCase 50 SimpleDefinition    Produces 100        EndTestCase
   
EndRegion

Region \ Math operators

    Macro + ( x y -- x+y ) /Add EndMacro
    TestCase 1 1 + Produces 2 EndTestCase

    Macro - ( x y -- x-y ) /Sub EndMacro
    TestCase 1 1 - Produces 0 EndTestCase
    TestCase 0 1 - Produces -1 EndTestCase
    TestCase 4 2 - Produces 2 EndTestCase
        
    Macro * /Mlt EndMacro

    Optimization 0 * OptimizesTo Drop 0 EndOptimization
    Optimization 1 * OptimizesTo EndOptimization
    Optimization 2 * OptimizesTo Dup + EndOptimization
    Optimization 3 * OptimizesTo Dup Dup + + EndOptimization
    Optimization 4 * OptimizesTo Dup + Dup + EndOptimization
    TestCase 2 5 * Produces 10 EndTestCase
    TestCase 10 100 * Produces 1000 EndTestCase
    TestCase 10 0 * Produces 0 EndTestCase
    TestCase 10 1 * Produces 10 EndTestCase
    TestCase 10 2 * Produces 20 EndTestCase
    TestCase 10 3 * Produces 30 EndTestCase
    TestCase 10 4 * Produces 40 EndTestCase

    Macro DivModCode \ Prerequisite code for / and Mod
        : DivMod \ a b -- a/b a%b
            Dup 1 0 _Take5_ \ _R1_ is a, _R2_ is b, _R5_ _R1_ is result
            Begin _R3_ @ _R1_ @ < While
                _R3_ @ 1 LShift _R3_ !
                _R4_ @ 1 LShift _R4_ !
            Repeat
            Begin _R1_ @ _R2_ @ >= While
                _R3_ @ _R1_ @ <= If
                    _R5_ @ _R4_ @ + _R5_ !
                    _R1_ @ _R3_ @ - _R1_ !
                Then
                _R3_ @ 1 RShift _R3_ !
                _R4_ @ 1 RShift _R4_ !
            Repeat
            _R5_ @ _R1_ @
            _Drop5_ 
        ;
        : /
            Dup 0= If Nip Else DivMod Drop Then
        ;
        : Mod
            Dup 0<> If DivMod Then Nip 
        ;
    EndMacro
    
    Prerequisite / ReturnStackCode
    Prerequisite / RShiftCode
    Prerequisite / LShiftCode
    Prerequisite / DivModCode
    Optimization 2 / OptimizesTo /Lsr EndOptimization
    Optimization 4 / OptimizesTo /Lsr /Lsr EndOptimization
    Optimization 8 / OptimizesTo /Lsr /Lsr /Lsr EndOptimization
    Optimization 16 / OptimizesTo /Lsr /Lsr /Lsr /Lsr EndOptimization
    TestCase 15 0 / Produces 0 EndTestCase
    TestCase 15 3 / Produces 5 EndTestCase
    TestCase 100 3 / Produces 33 EndTestCase
    TestCase 100 55 / Produces 1 EndTestCase
    TestCase 10 2 / Produces 5 EndTestCase
    TestCase 100 4 / Produces 25 EndTestCase
    TestCase 1000 8 / Produces 125 EndTestCase
    TestCase 10000 16 / Produces 625 EndTestCase

    Prerequisite Mod ReturnStackCode
    Prerequisite Mod RShiftCode
    Prerequisite Mod LShiftCode
    Prerequisite Mod DivModCode
    Optimization 2 Mod OptimizesTo 1 /And EndOptimization
    Optimization 4 Mod OptimizesTo 3 /And EndOptimization
    Optimization 8 Mod OptimizesTo 7 /And EndOptimization
    Optimization 16 Mod OptimizesTo 15 /And EndOptimization
    TestCase 15 0 Mod Produces 0 EndTestCase
    TestCase 15 3 Mod Produces 0 EndTestCase
    TestCase 100 3 Mod Produces 1 EndTestCase
    TestCase 100 55 Mod Produces 45 EndTestCase
    TestCase $55 2 Mod Produces 1 EndTestCase
    TestCase $55 4 Mod Produces 1 EndTestCase
    TestCase $55 8 Mod Produces 5 EndTestCase
    TestCase $55 16 Mod Produces 5 EndTestCase
        
    Macro = /Xor /Zeq EndMacro
    TestCase 1 1 = Produces -1 EndTestCase
    TestCase 1 0 = Produces 0 EndTestCase

    Macro <> /Xor /Zeq /Zeq EndMacro
    TestCase 1 1 <> Produces 0 EndTestCase
    TestCase 1 0 <> Produces -1 EndTestCase

    Macro 0= /Zeq EndMacro
    TestCase 0 0= Produces -1 EndTestCase
    TestCase 1 0= Produces 0 EndTestCase

    Macro 0<> /Zeq /Zeq EndMacro
    TestCase 0 0<> Produces 0 EndTestCase
    TestCase 1 0<> Produces -1 EndTestCase

    Macro 0>= /Psh /Add /Psh /Xor /Psh /Adc /Zeq EndMacro
    TestCase -2 0>= Produces 0 EndTestCase
    TestCase -1 0>= Produces 0 EndTestCase
    TestCase 0 0>= Produces -1 EndTestCase
    TestCase 1 0>= Produces -1 EndTestCase
    TestCase 2 0>= Produces -1 EndTestCase

    Macro < - 0>= 0= EndMacro
    TestCase 0 1 < Produces -1 EndTestCase
    TestCase 1 0 < Produces 0 EndTestCase
    TestCase 1 1 < Produces 0 EndTestCase

    Macro > Swap - 0>= 0= EndMacro
    TestCase 0 1 > Produces 0 EndTestCase
    TestCase 1 0 > Produces -1 EndTestCase
    TestCase 1 1 > Produces 0 EndTestCase

    Macro <= Swap - 0>= EndMacro
    TestCase 0 1 <= Produces -1 EndTestCase
    TestCase 1 0 <= Produces 0 EndTestCase
    TestCase 1 1 <= Produces -1 EndTestCase

    Macro >= - 0>= EndMacro
    TestCase 0 1 >= Produces 0 EndTestCase
    TestCase 1 0 >= Produces -1 EndTestCase
    TestCase 1 1 >= Produces -1 EndTestCase

    Macro And /And EndMacro
    TestCase %00011111 %11111000 And Produces %00011000 EndTestCase

    Macro Xor /Xor EndMacro
    TestCase %00011111 %11111000 Xor Produces %11100111 EndTestCase

    Macro Or /Ior EndMacro
    TestCase %00011111 %11111000 Or Produces %11111111 EndTestCase

    Macro Invert -1 Xor EndMacro
    TestCase -1 Invert Produces 0 EndTestCase
    TestCase  0 Invert Produces -1 EndTestCase

    Macro Negate 0 Swap - EndMacro
    TestCase 0 Negate Produces 0 EndTestCase
    TestCase -1 Negate Produces 1 EndTestCase
    TestCase 1 Negate Produces -1 EndTestCase

    Macro Abs Dup 0>= 0= If Negate Then EndMacro
    TestCase -1 Abs Produces 1 EndTestCase
    TestCase 1 Abs Produces 1 EndTestCase

    Macro Min 2Dup > If Swap Then Drop EndMacro
    Prerequisite Min ReturnStackCode
    TestCase 0 1 Min Produces 0 EndTestCase
    TestCase 1 0 Min Produces 0 EndTestCase

    Macro Max 2Dup < If Swap Then Drop EndMacro
    Prerequisite Max ReturnStackCode
    TestCase 0 1 Max Produces 1 EndTestCase
    TestCase 1 0 Max Produces 1 EndTestCase
    
    Macro Within _Take3_ _R1_ @ dup _R2_ @ >= Swap _R3_ @ < And _Drop3_ EndMacro
    TestCase 0 1 3 Within Produces 0 EndTestCase
    TestCase 1 1 3 Within Produces -1 EndTestCase
    TestCase 2 1 3 Within Produces -1 EndTestCase
    TestCase 3 1 3 Within Produces 0 EndTestCase
    TestCase 4 1 3 Within Produces 0 EndTestCase

    Macro Within? _Take3_ _R1_ @ dup _R2_ @ >= Swap _R3_ @ <= And _Drop3_ EndMacro
    TestCase 0 1 3 Within? Produces 0 EndTestCase
    TestCase 1 1 3 Within? Produces -1 EndTestCase
    TestCase 2 1 3 Within? Produces -1 EndTestCase
    TestCase 3 1 3 Within? Produces -1 EndTestCase
    TestCase 4 1 3 Within? Produces 0 EndTestCase

    Macro LShiftCode \ Prerequisite code for LShift
        : LShift \ x y -- z
			Begin
				Dup 0<>
			While
				1 - Swap Dup + Swap
			Repeat
			Drop
		;
    EndMacro
    Prerequisite LShift ReturnStackCode
    Prerequisite LShift LShiftCode
    Optimization 0 LShift OptimizesTo EndOptimization
    Optimization 1 LShift OptimizesTo Dup +  EndOptimization
    Optimization 2 LShift OptimizesTo Dup + Dup + EndOptimization
    Optimization 3 LShift OptimizesTo Dup + Dup + Dup + EndOptimization
    Optimization 4 LShift OptimizesTo Dup + Dup + Dup + Dup + EndOptimization
    TestCase 16 0 LShift Produces 16 EndTestCase
    TestCase 16 1 LShift Produces 32 EndTestCase
    TestCase 16 2 LShift Produces 64 EndTestCase
    TestCase 16 3 LShift Produces 128 EndTestCase
    TestCase 16 4 LShift Produces 256 EndTestCase
    TestCase 16 12 LShift Produces 65536 EndTestCase

    Macro RShiftCode \ Prerequisite code for RShift
        : RShift \ x y -- z
			Begin
				Dup 0<>
			While
				1 - Swap /Lsr Swap
			Repeat
			Drop
        ;
    EndMacro
    Prerequisite RShift ReturnStackCode
    Prerequisite RShift RShiftCode
    Optimization 0 RShift OptimizesTo EndOptimization
    Optimization 1 RShift OptimizesTo /Lsr  EndOptimization
    Optimization 2 RShift OptimizesTo /Lsr /Lsr EndOptimization
    Optimization 3 RShift OptimizesTo /Lsr /Lsr /Lsr EndOptimization
    Optimization 4 RShift OptimizesTo /Lsr /Lsr /Lsr /Lsr EndOptimization
    TestCase 16 0 RShift Produces 16 EndTestCase
    TestCase 16 1 RShift Produces 8 EndTestCase
    TestCase 16 2 RShift Produces 4 EndTestCase
    TestCase 16 3 RShift Produces 2 EndTestCase
    TestCase 16 4 RShift Produces 1 EndTestCase
    TestCase $4000 12 RShift Produces $4 EndTestCase

    Optimization /Zeq /Zeq /Zeq OptimizesTo /Zeq IsLastPass EndOptimization

EndRegion

Region \ stack operations

    Macro PushC 0 0 /Adc EndMacro
    TestCase -1 -1 + drop PushC Produces 1 EndTestCase
    
    Macro PopC /Lsr /Pop EndMacro
    TestCase 1 PopC 0 0 /Adc Produces 1 EndTestCase
    
    Macro RetI /Swp /Swp /Jnz EndMacro
    
    Macro Dup /Psh EndMacro
    TestCase 1 Dup Produces 1 1 EndTestCase

    Macro ?Dup Dup Dup 0= If Drop Then EndMacro
    TestCase 1 ?Dup Produces 1 1 EndTestCase
    TestCase 0 ?Dup Produces 0 EndTestCase

    Macro Drop ( a -- - ) /Pop EndMacro
    TestCase 1 2 Drop Produces 1 EndTestCase

    Macro Swap ( a b -- b a ) /Swp EndMacro
    TestCase 0 1 Swap Produces 1 0 EndTestCase

    Macro Over ( a b -- a b a ) _Take2_ _R1_ @ _R2_ @ _R1_ @ _Drop2_ EndMacro
    TestCase 1 2 Over Produces 1 2 1 EndTestCase
    Prerequisite Over ReturnStackCode

    Macro Nip ( a b -- b ) /Swp /Pop EndMacro
    TestCase 1 2 Nip Produces 2 EndTestCase

    Macro Tuck ( a b -- b a b ) Swap Over EndMacro
    TestCase 1 2 Tuck Produces 2 1 2 EndTestCase

    Macro Rot ( a b c -- b c a ) _Take3_ _R2_ @ _R3_ @ _R1_ @ _Drop3_ EndMacro
    TestCase 1 2 3 Rot Produces 2 3 1 EndTestCase
    Prerequisite Rot ReturnStackCode

    Macro -Rot ( a b c -- c a b ) _Take3_ _R3_ @ _R1_ @ _R2_ @ _Drop3_ EndMacro
    TestCase 1 2 3 -Rot Produces 3 1 2 EndTestCase
    Prerequisite -Rot ReturnStackCode

    Macro PickCode \ Prerequisite code for Pick
        : Pick \  xu .. x0 u -- xu .. x0 xu
            1 + Dup _RS_ @ + _RS_ !  _Take1_ \ allocate xu+2 items on the return stack
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
    Prerequisite Pick ReturnStackCode
    Prerequisite Pick LoopStackCode
    Prerequisite Pick PickCode
    TestCase 11 22 33 44 0 Pick Produces 11 22 33 44 44 EndTestCase
    TestCase 11 22 33 44 3 Pick Produces 11 22 33 44 11 EndTestCase

    Macro 2Dup _Take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _Drop2_ EndMacro
    TestCase 1 2 2Dup Produces 1 2 1 2 EndTestCase
    Prerequisite 2Dup ReturnStackCode

    Macro 2Drop /Pop /Pop EndMacro
    TestCase 1 2 3 4 2Drop Produces 1 2 EndTestCase

    Macro 2Swap _Take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _Drop4_ EndMacro
    TestCase 1 2 3 4 2Swap Produces 3 4 1 2 EndTestCase
    Prerequisite 2Drop ReturnStackCode

    Macro 2Over _Take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _Drop4_ EndMacro
    TestCase 1 2 3 4 2Over Produces 1 2 3 4 1 2 EndTestCase
    Prerequisite 2Over ReturnStackCode
    
EndRegion

Region \ Language Constructs

    Macro "If"
        Struct If Then
        0= Addr If.End And /Jnz
    EndMacro
    TestCase 1 If 88 Then Produces 88 EndTestCase
    TestCase 0 If 88 Then Produces EndTestCase
    TestCase 1 If 88 Else 99 Then Produces 88 EndTestCase
    TestCase 0 If 88 Else 99 Then Produces 99 EndTestCase

    Macro "Else"
        Addr If.EndElse /Jnz 
        Label If.End
    EndMacro

    Macro "Then"
        Label If.EndElse 
        Label If.End 
        EndStruct "If"
    EndMacro

    Macro "Exit"
        Addr Definition.Exit /Jnz
    EndMacro
    TestCase : ExitTest 11 Exit 22 ; EndTestCase
    TestCase ExitTest Produces 11 EndTestCase

    Macro "Do"
        Struct Do Loop
        _RS_Loop_ @ _Take3_ 
        _RS_ @ _RS_Loop_ !
        Label Do.Start
        _R2_ @ _R1_ @ >= Addr Do.End And /Jnz
    EndMacro
    Prerequisite "Do" ReturnStackCode
    Prerequisite "Do" LoopStackCode
    TestCase 5 0 Do I Loop Produces 0 1 2 3 4 EndTestCase
    TestCase 5 1 Do I Loop Produces 1 2 3 4 EndTestCase

    Macro "Loop"
        1 _R2_ @ + _R2_ ! 
        Addr Do.Start /Jnz
        Label Do.End _R3_ @ _RS_Loop_ ! 
        _Drop3_ 
        EndStruct "Do"
    EndMacro

    Macro "+Loop"
        _R2_ @ + _R2_ ! 
        Addr Do.Start /Jnz 
        Label Do.End _R3_ @ _RS_Loop_ ! 
        _Drop3_ 
        EndStruct "Do"
    EndMacro
    TestCase 5 0 Do I 2 +Loop Produces 0 2 4 EndTestCase

    Macro Unloop
        _Drop3_ 
    EndMacro
    TestCase : UnloopTest 5 0 Do I I 3 = If Unloop Exit Then Loop ;         EndTestCase
    TestCase UnloopTest Produces 0 1 2 3                                    EndTestCase

    Macro I 
        _RS_Loop_ @ 1 - @
    EndMacro

    Macro J 
        _RS_Loop_ @ 2 - @ 1 - @
    EndMacro
    TestCase 12 10 Do 22 20 Do J I Loop Loop Produces 10 20 10 21 11 20 11 21 EndTestCase

    Macro Leave 
        Addr Do.End /Jnz
    EndMacro
    TestCase 5 0 Do I I 3 = If Leave Then Loop Produces 0 1 2 3             EndTestCase

    Macro "Begin"
        Struct "Begin" "Again/Until/Repeat"
        Label Begin.Start
    EndMacro
    TestCase 5 Begin Dup 0<> While Dup 1 - Repeat   Produces 5 4 3 2 1 0    EndTestCase
    TestCase 5 Begin Dup 1 - Dup 0= Until           Produces 5 4 3 2 1 0    EndTestCase

    Macro "Again"
        Addr Begin.Start /Jnz 
        EndStruct "Begin"
    EndMacro
    TestCase : AgainTest 5 Begin Dup 1 - Dup 0= If Exit Then Again ;        EndTestCase
    TestCase AgainTest                              Produces 5 4 3 2 1 0    EndTestCase

    Macro "Until"
        0= Addr Begin.Start And /Jnz 
        EndStruct "Begin"
    EndMacro

    Macro "While"
        Struct "While" "Repeat"
        0= Addr While.End And /Jnz
    EndMacro

    Macro "Repeat" 
        Addr Begin.Start /Jnz Label While.End 
        EndStruct "While"
        EndStruct "Begin"
    EndMacro

    Macro "Case" 
        Struct "Case" "EndCase"
        _Take1_
        _R1_ @
    EndMacro
    Prerequisite "Case" ReturnStackCode

    Macro "Of" 
        Struct "Of" "EndOf"
        <> Addr Of.End And /Jnz
    EndMacro

    Macro "Of?" 
        Struct "Of" "EndOf"
        Nip 0= Addr Of.End And /Jnz
    EndMacro
    
    Macro "EndOf"
        Addr Case.End /Jnz Label Of.End 
        EndStruct "Of"
        _R1_ @ 
    EndMacro

    Macro "EndCase"
        drop
        Label Case.End _Drop1_ 
        EndStruct "Case"
    EndMacro

    TestCase 0 Case 1 Of 10 EndOf 2 Of 20 20 EndOf Dup 3 4 Within? Of? 34 EndOf Dup EndCase Produces 0      EndTestCase
    TestCase 1 Case 1 Of 10 EndOf 2 Of 20 20 EndOf Dup 3 4 Within? Of? 34 EndOf Dup EndCase Produces 10     EndTestCase
    TestCase 2 Case 1 Of 10 EndOf 2 Of 20 20 EndOf Dup 3 4 Within? Of? 34 EndOf Dup EndCase Produces 20 20  EndTestCase
    TestCase 3 Case 1 Of 10 EndOf 2 Of 20 20 EndOf Dup 3 4 Within? Of? 34 EndOf Dup EndCase Produces 34     EndTestCase
    TestCase 4 Case 1 Of 10 EndOf 2 Of 20 20 EndOf Dup 3 4 Within? Of? 34 EndOf Dup EndCase Produces 34     EndTestCase
    TestCase 5 Case 1 Of 10 EndOf 2 Of 20 20 EndOf Dup 3 4 Within? Of? 34 EndOf Dup EndCase Produces 5      EndTestCase
    TestCase 6 Case                                                                 EndCase Produces        EndTestCase
    
EndRegion

Region \ Exception test cases

    TestCase ""                                  ProducesException "No code produced"                         EndTestCase
    TestCase 1		                             ProducesException ""                                         EndTestCase
    TestCase "("                                 ProducesException "missing )"                                EndTestCase
    TestCase ")"                                 ProducesException ") is not defined"                         EndTestCase
    TestCase "If Again"                          ProducesException "Missing Begin"                            EndTestCase
    TestCase "If"                                ProducesException "Missing Then"                             EndTestCase
    TestCase "If Else"                           ProducesException "Missing Then"                             EndTestCase
    TestCase "Then"                              ProducesException "Missing If"                               EndTestCase
    TestCase "Else"                              ProducesException "Missing If"                               EndTestCase
    TestCase "Begin"                             ProducesException "Missing Again/Until/Repeat"               EndTestCase
    TestCase "Begin While"                       ProducesException "Missing Repeat"                           EndTestCase
    TestCase "Exit"                              ProducesException "Missing Definition"                       EndTestCase
    TestCase Addr Name    Label Name             ProducesException "Missing Name"                             EndTestCase
    TestCase Addr .Name   Label .Name            ProducesException ""                                         EndTestCase
    TestCase Addr .Name                          ProducesException "Missing Label .Name"                      EndTestCase
    TestCase Constant                            ProducesException "Constant expects 1 arguments"             EndTestCase
    TestCase Constant Name                       ProducesException "missing code to evaluate"                 EndTestCase
    TestCase [ 1 2 ] Constant Name               ProducesException "Constant expects 1 preceding value"       EndTestCase
    TestCase [ 1 2 ] Allot                       ProducesException "Allot expects 1 preceding value"          EndTestCase
    TestCase yy WithCore prerequisite yy y       ProducesException "y is not defined as a Macro"              EndTestCase
    TestCase Macro y EndMacro : y ;              ProducesException "y is already defined as a Macro"          EndTestCase
    TestCase Macro y EndMacro Macro y EndMacro   ProducesException "y is already defined as a Macro"          EndTestCase
    TestCase : y ; Macro y EndMacro              ProducesException "y is already defined as a Definition"     EndTestCase
    TestCase Variable y Macro y EndMacro         ProducesException "y is already defined as a Variable"       EndTestCase
    TestCase y                                   ProducesException "y is not defined"                         EndTestCase
    TestCase ]                                   ProducesException "Missing ["                                EndTestCase
    TestCase [                                   ProducesException "Missing ]"                                EndTestCase
    TestCase [                                   ProducesException "Missing ]"                                EndTestCase
    TestCase 1 Org 0 Org                         ProducesException "Org value decreasing from 1 to 0"         EndTestCase
    TestCase Macro y EndMacro Undefine y : y ;   ProducesException ""          								  EndTestCase

EndRegion

Region \ Optimization test cases

    TestCase Macro y EndMacro Macro y Redefine 2 EndMacro y	ProducesCode 2 								         EndTestCase
	TestCase 0 begin again                              	ProducesCode 0 Label .a Addr .a /jnz EndTestCase
    TestCase Addr .y [ 1 1 + ] Label .y                 	ProducesCode /psh /_0 /_5 2                                                                               EndTestCase
    TestCase RetI                                       	ProducesCode /Swp /Swp /Jnz                           ( Make sure RetI works                            ) EndTestCase
    TestCase 0 Org 6 Org 1 2 +                          	ProducesCode /_0 /_0 /_0 /_0 /_0 /_0 1 2 +            ( Make sure Org works                             ) EndTestCase
    TestCase 0 Org 6 Org 1 2 + WithCore Macro A 9 EndMacro Prerequisite "+" A 
															ProducesCode /_0 /_0 /_0 /_0 /_0 /_0 9 1 2 +          ( Make sure Org works with prerequisites          ) EndTestCase
    TestCase 1 7 8 -1                                   	ProducesCode /Psh /_1 /Psh /_7 /Psh /_0 /_8 /Psh /_F  ( Make sure lits are properly compressed          ) EndTestCase
    TestCase [ 1 2 3 Rot ]                              	ProducesCode 2 3 1                                    ( Make sure ReturnStackCode is optimized out      ) EndTestCase
    TestCase [ 6 7 * ]                                  	ProducesCode 42                                       ( Make sure MulCode is optimized out              ) EndTestCase
    TestCase 5 3 *                                      	ProducesCode 5 Dup Dup + +                            ( Make sure 3 * is optimized                      ) EndTestCase
    TestCase 5 3 LShift                                 	ProducesCode 5 Dup + Dup + Dup +                      ( Make sure 3 LShift is optimized                 ) EndTestCase
    
	\ TestCase 
		\ : a ; : b ;                					
	\ ProducesCode 
		\ addr .x /jnz >R R> /jnz >R R> /jnz label .x 1    \ Make sure jump Optimization works
	\ EndTestCase
	
    TestCase 1 addr .b /jnz 2 label .b 3                	ProducesCode 1 addr .b /jnz label .b 3                ( Make sure jump Optimization works               ) EndTestCase
    TestCase : ExitTest 11 Exit 22 ; ExitTest           	ProducesCode : ExitTest 11 Exit ; ExitTest            ( Make sure unreachable code Optimization works   ) EndTestCase
    TestCase : TestOptimize2 _Take2_ _R1_ @ 2 * _R2_ @ 3 * + _Drop2_ ;                                        (                                                 ) EndTestCase
    TestCase : TestOptimize3 _Take3_ _R1_ @ 2 * _R2_ @ 3 * + _Drop3_ ;                                        (                                                 ) EndTestCase
    TestCase : TestOptimize4 _Take4_ _R1_ @ 2 * _R2_ @ 3 * + _Drop4_ ;                                        (                                                 ) EndTestCase
    TestCase 1 1 TestOptimize2  1 1 1 TestOptimize3 1 1 1 1 TestOptimize4   Produces 5 5 5                    (                                                 ) EndTestCase

EndRegion

Region \ MIF test cases

    TestCase 0 ProducesMif "0000 : 00000012;" EndTestCase
    TestCase 1 ProducesMif "0000 : 00000032;" EndTestCase
    
EndRegion
