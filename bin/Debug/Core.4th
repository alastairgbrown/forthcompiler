Macro "Region" EndMacro
Macro "EndRegion" EndMacro

Region \ Assembly codes

    $10         Constant    .LDW 
    $11         Constant    .STW 
    $12         Constant    .PSH 
    $13         Constant    .POP 
    $14         Constant    .SWP 
    $15         Constant    .JNZ 
    $16         Constant    .JSR 
    $17         Constant    .RTO 
    $18         Constant    .TOR 
    $19         Constant    .ADD 
    $1a         Constant    .SUB 
    $1b         Constant    .AND 
    $1c         Constant    .XOR 
    $1d         Constant    .LSR 
    $1e         Constant    .ZEQ 
    [ $1f $00 ] 2Constant   .ADC 
    [ $1f $01 ] 2Constant   .MLT 
    [ $1f $02 ] 2Constant   .IOR 
    [ $1f $03 ] 2Constant   .LSP 
    [ $1f $04 ] 2Constant   .LRP 

EndRegion

Region \ General test cases

    TestCase ( General test cases ) EndTestCase
    TestCase Variable TestVariable 1 TestVariable ! TestVariable @  Produces 1                      EndTestCase
    TestCase 2 Constant TestConstant TestConstant                   Produces 2                      EndTestCase
    TestCase [ 2 20 + ] Constant TestConstant22 TestConstant22      Produces 22                     EndTestCase
    TestCase [ TestConstant22 10 * ]                                Produces 220                    EndTestCase
    TestCase 3 VALUE TestValue TestValue @                          Produces 3                      EndTestCase
    TestCase $0F $0B -                                              Produces 4                      EndTestCase
    TestCase %101                                                   Produces 5                      EndTestCase
    TestCase #6                                                     Produces 6                      EndTestCase
    TestCase [ 2 3 * 1 + ]                                          Produces 7                      EndTestCase
    TestCase [ 21 3 / 1 + ]                                         Produces 8                      EndTestCase
    TestCase : Def Dup + ;                                                                          EndTestCase
    TestCase 123 Def                                                Produces 246                    EndTestCase
    TestCase Include "IncludableFileForTestCases.4th"                                               EndTestCase
    TestCase IncludedConstant                                       Produces 24601                  EndTestCase
    TestCase [Char] 0 [Char] A [Char] a [Char] " " [Char] """"      Produces $30 $41 $61 $20 $22    EndTestCase
    TestCase C"A string" @                                          Produces 8                      EndTestCase
    TestCase C"A string" 1 + @                              [Char]  Produces "A"                    EndTestCase
    TestCase C"A string" 2 + @                              [Char]  Produces """ """                EndTestCase
    TestCase C"A string" 3 + @                              [Char]  Produces "s"                    EndTestCase
    TestCase C"A STRING" 3 + @                              [Char]  Produces "S"                    EndTestCase
    TestCase C"A string" 8 + @                              [Char]  Produces "g"                    EndTestCase
    TestCase Create TestComma 100 , 200 , 300 ,                                                     EndTestCase
    TestCase TestComma @                                            Produces 100                    EndTestCase
    TestCase TestComma 1 + @                                        Produces 200                    EndTestCase
    TestCase TestComma 2 + @                                        Produces 300                    EndTestCase
    
EndRegion

Region \ memory operations

    Macro @ ( Address -- DataAtAddress ) /Ldw EndMacro
    Macro ! ( Data Address -- ) /Stw /Pop /Pop EndMacro
    Macro +! Dup -Rot @ + Swap ! EndMacro
	
    TestCase ( memory test cases ) EndTestCase
    TestCase Variable TestIncrement EndTestCase
    TestCase 1 TestIncrement ! 1 TestIncrement +! TestIncrement @ Produces 2 EndTestCase
    TestCase 5 TestIncrement ! 2 TestIncrement +! TestIncrement @ Produces 7 EndTestCase
    
EndRegion

Region 

    Macro >R /tor /pop EndMacro
    Macro R> /psh /rto EndMacro
	Macro R@ /psh /lrp @ EndMacro

    Macro _R1_    /psh /lrp EndMacro
    Macro _R2_    /psh /lrp 1 - EndMacro
    Macro _R3_    /psh /lrp 2 - EndMacro
    Macro _R4_    /psh /lrp 3 - EndMacro
    Macro _R5_    /psh /lrp 4 - EndMacro
    Macro _Take1_ >R EndMacro
    Macro _Take2_ >R >R EndMacro
    Macro _Take3_ >R >R >R EndMacro
    Macro _Take4_ >R >R >R >R EndMacro
    Macro _Take5_ >R >R >R >R >R EndMacro
    Macro _Drop1_ /psh /rto /pop EndMacro
    Macro _Drop2_ /psh /rto /rto /pop EndMacro
    Macro _Drop3_ /psh /rto /rto /rto /pop EndMacro
    Macro _Drop4_ /psh /rto /rto /rto /rto /pop EndMacro
    Macro _Drop5_ /psh /rto /rto /rto /rto /rto /pop EndMacro
    
    TestCase 56 >R R@ R> Drop Produces 56 EndTestCase
    TestCase 34 >R _R1_ @ R> Drop Produces 34 EndTestCase
    TestCase 34 45 _Take2_ _R1_ @ _R2_ @ Produces 34 45 EndTestCase

    Macro LoopStackCode \ Prerequisite code for loops
        Variable _RS_Loop_ \ A pointer to the current loop variables for getting values for I And J
    EndMacro

    Macro LoadCode \ Prerequisite code for counted strings
        : Load \ 1..n n addr --
            _Take2_
            _R1_ @ 0 Do
                _R5_ @ I + !
            Loop
            _Drop2_
        ;
    EndMacro  

    Macro Count  ( CountedStringAddr -- AddressOf1stChar Count )
        Dup @ Swap 1 + Swap
    EndMacro
    TestCase C"A string" Count swap @ swap Produces 65 8 EndTestCase
        
EndRegion

Region \ Definition operators
    
    Macro ":" IsDefinition \ Start Standard defintion
        Struct Definition ";"
        Addr Definition.Skip /Jnz 
        Label {Label} 
        >R
    EndMacro

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
    
    TestCase ( Definition test cases ) EndTestCase
    TestCase :: SimpleDefinition Swap Dup + Swap ;;     EndTestCase
    TestCase 50 SimpleDefinition    Produces 100        EndTestCase
   
EndRegion

Region \ Math operators

    TestCase ( Math operators test cases ) EndTestCase

    Macro + ( x y -- x+y ) /Add EndMacro
    TestCase 1 1 + Produces 2 EndTestCase
    TestCase 1 -1 + Produces 0 EndTestCase
    TestCase 0 -1 + Produces -1 EndTestCase

    Macro - ( x y -- x-y ) /Sub EndMacro
    TestCase 1 1 - Produces 0 EndTestCase
    TestCase 0 1 - Produces -1 EndTestCase
    TestCase 4 2 - Produces 2 EndTestCase
        
    Macro PushC ( -- c ) 0 0 /Adc EndMacro
    TestCase -1 -1 + drop PushC Produces 1 EndTestCase
    
    Macro PopC ( c -- ) /Lsr /Pop EndMacro
    TestCase 1 PopC 0 0 /Adc Produces 1 EndTestCase
		
    Macro * ( a b -- a*b ) /Mlt EndMacro

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
        : / ( a b -- a/b )
            Dup 0= If Nip Else DivMod Drop Then
        ;
        : Mod ( a b -- a%b )
            Dup 0<> If DivMod Then Nip 
        ;
    EndMacro
    
    Prerequisite DivMod DivModCode

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
        
    Macro = ( a b -- a==b ) /Xor /Zeq EndMacro
    TestCase 1 1 = Produces -1 EndTestCase
    TestCase 1 0 = Produces 0 EndTestCase

    Macro <> ( a b -- a!=b ) /Xor /Zeq /Zeq EndMacro
    TestCase 1 1 <> Produces 0 EndTestCase
    TestCase 1 0 <> Produces -1 EndTestCase

    Macro 0= ( num -- num==0 ) /Zeq EndMacro
    TestCase 0 0= Produces -1 EndTestCase
    TestCase 1 0= Produces 0 EndTestCase

    Macro 0<> ( num -- num!=0 ) /Zeq /Zeq EndMacro
    TestCase 0 0<> Produces 0 EndTestCase
    TestCase 1 0<> Produces -1 EndTestCase

    Macro 0>= ( num -- num>=0 ) /Psh /Add /Psh /Xor /Psh /Adc /Zeq EndMacro
    TestCase -2 0>= Produces 0 EndTestCase
    TestCase -1 0>= Produces 0 EndTestCase
    TestCase 0 0>= Produces -1 EndTestCase
    TestCase 1 0>= Produces -1 EndTestCase
    TestCase 2 0>= Produces -1 EndTestCase

    Macro < ( a b -- a<b ) - 0>= 0= EndMacro
    TestCase 0 1 < Produces -1 EndTestCase
    TestCase 1 0 < Produces 0 EndTestCase
    TestCase 1 1 < Produces 0 EndTestCase

    Macro > ( a b -- a>b ) Swap - 0>= 0= EndMacro
    TestCase 0 1 > Produces 0 EndTestCase
    TestCase 1 0 > Produces -1 EndTestCase
    TestCase 1 1 > Produces 0 EndTestCase

    Macro <= ( a b -- a<=b ) Swap - 0>= EndMacro
    TestCase 0 1 <= Produces -1 EndTestCase
    TestCase 1 0 <= Produces 0 EndTestCase
    TestCase 1 1 <= Produces -1 EndTestCase

    Macro >= ( a b -- a>=b ) - 0>= EndMacro
    TestCase 0 1 >= Produces 0 EndTestCase
    TestCase 1 0 >= Produces -1 EndTestCase
    TestCase 1 1 >= Produces -1 EndTestCase

    Macro And ( a b -- a&b ) /And EndMacro
    TestCase %00011111 %11111000 And Produces %00011000 EndTestCase

    Macro Xor ( a b -- a^b ) /Xor EndMacro
    TestCase %00011111 %11111000 Xor Produces %11100111 EndTestCase

    Macro Or ( a b -- a|b ) /Ior EndMacro
    TestCase %00011111 %11111000 Or Produces %11111111 EndTestCase

    Macro Invert ( num -- ~num ) -1 Xor EndMacro
    TestCase -1 Invert Produces 0 EndTestCase
    TestCase  0 Invert Produces -1 EndTestCase

    Macro Negate ( num -- -num ) 0 Swap - EndMacro
    TestCase 0 Negate Produces 0 EndTestCase
    TestCase -1 Negate Produces 1 EndTestCase
    TestCase 1 Negate Produces -1 EndTestCase

    Macro Abs ( num -- |num| ) Dup 0>= 0= If Negate Then EndMacro
    TestCase -1 Abs Produces 1 EndTestCase
    TestCase 1 Abs Produces 1 EndTestCase

    Macro Min ( a b -- min[a,b] ) 2Dup > If Swap Then Drop EndMacro
    TestCase 0 1 Min Produces 0 EndTestCase
    TestCase 1 0 Min Produces 0 EndTestCase

    Macro Max ( a b -- max[a,b] ) 2Dup < If Swap Then Drop EndMacro
    TestCase 0 1 Max Produces 1 EndTestCase
    TestCase 1 0 Max Produces 1 EndTestCase
    
    Macro Within ( a b c -- a>=b&&a<c ) _Take3_ _R1_ @ dup _R2_ @ >= Swap _R3_ @ < And _Drop3_ EndMacro
    TestCase 0 1 3 Within Produces 0 EndTestCase
    TestCase 1 1 3 Within Produces -1 EndTestCase
    TestCase 2 1 3 Within Produces -1 EndTestCase
    TestCase 3 1 3 Within Produces 0 EndTestCase
    TestCase 4 1 3 Within Produces 0 EndTestCase

    Macro Within? ( a b c -- a>=b&&a<=c ) _Take3_ _R1_ @ dup _R2_ @ >= Swap _R3_ @ <= And _Drop3_ EndMacro
    TestCase 0 1 3 Within? Produces 0 EndTestCase
    TestCase 1 1 3 Within? Produces -1 EndTestCase
    TestCase 2 1 3 Within? Produces -1 EndTestCase
    TestCase 3 1 3 Within? Produces -1 EndTestCase
    TestCase 4 1 3 Within? Produces 0 EndTestCase

    Macro LShiftCode \ Prerequisite code for LShift
        : LShift \ x y -- x<<y
			Begin
				Dup 0<>
			While
				1 - Swap Dup + Swap
			Repeat
			Drop
		;
    EndMacro
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
        : RShift \ x y -- x>>y
			Begin
				Dup 0<>
			While
				1 - Swap /Lsr Swap
			Repeat
			Drop
        ;
    EndMacro
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

Region \ Stack operations
    
    TestCase ( Stack operations test cases ) EndTestCase
    
    Macro RetI ( -- ) /Swp /Swp /Jnz EndMacro
    
    Macro Dup ( a -- a a ) /Psh EndMacro
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

    Macro Nip ( a b -- b ) /Swp /Pop EndMacro
    TestCase 1 2 Nip Produces 2 EndTestCase

    Macro Tuck ( a b -- b a b ) Swap Over EndMacro
    TestCase 1 2 Tuck Produces 2 1 2 EndTestCase

    Macro Rot ( a b c -- b c a ) _Take3_ _R2_ @ _R3_ @ _R1_ @ _Drop3_ EndMacro
    TestCase 1 2 3 Rot Produces 2 3 1 EndTestCase

    Macro -Rot ( a b c -- c a b ) _Take3_ _R3_ @ _R1_ @ _R2_ @ _Drop3_ EndMacro
    TestCase 1 2 3 -Rot Produces 3 1 2 EndTestCase

    Macro Pick 0 Swap /Psh /Lsp Swap - 1 - @ Nip EndMacro
    TestCase 11 22 33 44 0 Pick Produces 11 22 33 44 44 EndTestCase
    TestCase 11 22 33 44 3 Pick Produces 11 22 33 44 11 EndTestCase

    Macro 2Dup ( a b -- a b a b ) _Take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _Drop2_ EndMacro
    TestCase 1 2 2Dup Produces 1 2 1 2 EndTestCase

    Macro 2Drop ( a b -- ) /Pop /Pop EndMacro
    TestCase 1 2 3 4 2Drop Produces 1 2 EndTestCase

    Macro 2Swap ( a b c d -- c d a b ) _Take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _Drop4_ EndMacro
    TestCase 1 2 3 4 2Swap Produces 3 4 1 2 EndTestCase

    Macro 2Over ( a b c d -- a b c d a b ) _Take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _Drop4_ EndMacro
    TestCase 1 2 3 4 2Over Produces 1 2 3 4 1 2 EndTestCase
    
EndRegion

Region \ Language Constructs

    TestCase ( Language Constructs test cases ) EndTestCase

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
        _R1_ _RS_Loop_ !
        Label Do.Start
        _R2_ @ _R1_ @ >= Addr Do.End And /Jnz
    EndMacro
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

Region \ I/O

    Macro IoBaseCode \ Prerequisite code for basic I/O
        
        $808                constant UART_BASE
        UART_BASE           constant .UART_INPUT_READY_ADDRESS
        3                   constant .UART_INPUT_READY_BIT
        [ UART_BASE 1 + ]   constant .UART_OUTPUT_ADDRESS
        [ UART_BASE 1 + ]   constant .UART_INPUT_ADDRESS
        
        :: uart_init ( -- ) 2 UART_BASE ! ;;
        :: emit ( char -- ) swap begin UART_BASE @ $20 and 0= until [ UART_BASE 1 + ] ! ;;
        :: key ( -- char ) begin UART_BASE @ $8 and 0<> until [ UART_BASE 1 + ] @ swap ;;
        :: key? ( -- t/f ) UART_BASE @ $8 and 0<> swap ;;
        uart_init
        
    EndMacro
    Prerequisite emit IoBaseCode
    Prerequisite key IoBaseCode
    Prerequisite key? IoBaseCode
        
    Macro IoDefinitionCode \ Prerequisite code for the . operator
        
        : . ( NumberToEmit -- )
            Dup 0 < If 
                ."-"
                Negate
            Then
            0 _Take2_
            Begin
                _R2_ @ 1 + _R2_ !
                _R1_ @ 10 DivMod
                Swap Dup _R1_ ! 
            0= Until
            _R2_ @ 0 Do
                [Char] "0" + emit
            Loop
            _Drop2_
        ;
        
        : Type ( AddressOf1stChar Count -- )
            0 Do
                Dup @ Emit
                1 +
            Loop
            Drop
        ;
        
    EndMacro
    Prerequisite . IoDefinitionCode
    Prerequisite Type IoDefinitionCode
    Macro CR 13 emit 10 emit EndMacro
    
    TestCase ( I/O  test cases ) EndTestCase
    TestCase ".""AB"""                                  ProducesCode   65 emit 66 emit             EndTestCase
    TestCase ".""AB"""                                  ProducesOutput 65 emit 66 emit             EndTestCase
    TestCase 0 .                                        ProducesOutput ".""0"""                    EndTestCase
    TestCase 123 .                                      ProducesOutput ".""123"""                  EndTestCase
    TestCase -123 .                                     ProducesOutput ".""-123"""                 EndTestCase
    TestCase C"A string" Count Type                     ProducesOutput ".""A string"""             EndTestCase
    TestCase CR                                         ProducesOutput $D emit $A emit             EndTestCase
    TestCase Key? .                                     ProducesOutput 0 .         WithInput ""    EndTestCase
    TestCase Key? .                                     ProducesOutput -1 .        WithInput 1 .   EndTestCase
    TestCase Begin Key? While Key Dup Emit Emit Repeat  ProducesOutput 112233 .    WithInput 123 . EndTestCase

EndRegion


Region \ Exception test cases

    TestCase ( Exception test cases ) EndTestCase
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

\ Region \ Optimization test cases

    \ TestCase ( Optimization test cases ) EndTestCase
    \ TestCase Macro y EndMacro Macro y Redefine 2 EndMacro y	ProducesCode 2 								         EndTestCase
	\ TestCase 0 begin again                              	ProducesCode 0 Label .a Addr .a /jnz EndTestCase
    \ TestCase Addr .y [ 1 1 + ] Label .y  WithOptimization   ProducesCode /psh /_0 /_5 2                                                                               EndTestCase
    \ TestCase RetI                                       	ProducesCode /Swp /Swp /Jnz                           ( Make sure RetI works                            ) EndTestCase
    \ TestCase 0 Org 6 Org 1 2 +                          	ProducesCode /_0 /_0 /_0 /_0 /_0 /_0 1 2 +            ( Make sure Org works                             ) EndTestCase
    \ TestCase 0 Org 6 Org 1 2 + WithCore Macro A 9 EndMacro Prerequisite "+" A 
															\ ProducesCode /_0 /_0 /_0 /_0 /_0 /_0 9 1 2 +          ( Make sure Org works with prerequisites          ) EndTestCase
    \ TestCase 1 7 8 -1                    WithOptimization   ProducesCode /Psh /_1 /Psh /_7 /Psh /_0 /_8 /Psh /_F  ( Make sure lits are properly compressed          ) EndTestCase
    \ TestCase [ 1 2 3 Rot ]               WithOptimization   ProducesCode 2 3 1                                    ( Make sure Rot is Excluded out                   ) EndTestCase
    \ TestCase [ 6 7 * ]                   WithOptimization   ProducesCode 42                                       ( Make sure MulCode is optimized out              ) EndTestCase
    \ TestCase 5 3 *                       WithOptimization   ProducesCode 5 Dup Dup + +                            ( Make sure 3 * is optimized                      ) EndTestCase
    \ TestCase 5 3 LShift                  WithOptimization   ProducesCode 5 Dup + Dup + Dup +                      ( Make sure 3 LShift is optimized                 ) EndTestCase
    \ TestCase $12 RawOpcode $00 RawOpcode                    ProducesCode 0                              EndTestCase
	
    
	\ \ TestCase 
		\ \ :: a 1 ;; :: b 2 ;; a b WithOptimization              					
	\ \ ProducesCode 
		\ \ addr .x /jnz label .a 1 /jnz label .b 2 /jnz label .x addr .a /jsr addr .b /jsr    \ Make sure jump Optimization works
	\ \ EndTestCase
	
    \ TestCase 1 addr .b /jnz 2 label .b 3      WithOptimization   ProducesCode 1 addr .b /jnz label .b 3            ( Make sure jump Optimization works               ) EndTestCase
    \ TestCase : ExitTest 11 Exit 22 ; ExitTest WithOptimization   ProducesCode : ExitTest 11 Exit ; ExitTest        ( Make sure unreachable code Optimization works   ) EndTestCase
    \ TestCase : TestOptimize2 _Take2_ _R1_ @ 2 * _R2_ @ 3 * + _Drop2_ ;                                        (                                                 ) EndTestCase
    \ TestCase : TestOptimize3 _Take3_ _R1_ @ 2 * _R2_ @ 3 * + _Drop3_ ;                                        (                                                 ) EndTestCase
    \ TestCase : TestOptimize4 _Take4_ _R1_ @ 2 * _R2_ @ 3 * + _Drop4_ ;                                        (                                                 ) EndTestCase
    \ TestCase 1 1 TestOptimize2  1 1 1 TestOptimize3 1 1 1 1 TestOptimize4   Produces 5 5 5                    (                                                 ) EndTestCase

\ EndRegion

\ Region \ MIF Generation test cases

    \ TestCase ( MIF Generation test cases ) EndTestCase
    \ TestCase /stw /stw /stw	/stw /stw /stw	ProducesMif "0000 : 2318C631;" EndTestCase
	\ TestCase 
		\ 16 Constant .Opcode_Word_Size
		\ 8 Constant .Opcode_Instruction_Size
		\ 2 Constant .Opcode_Instructions_Per_Word
		\ 2 Constant .Opcode_Sub_Word_Slot_Bits
		\ /stw /stw	ProducesMif "0000 : 1111;" EndTestCase
    \ TestCase 0	ProducesMif "0000 : 00000012;" EndTestCase
    \ TestCase 1	ProducesMif "0000 : 00000032;" EndTestCase
    
\ EndRegion
