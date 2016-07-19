DEFLNG A-Z

ON ERROR GOTO FileOpenError

fil = FREEFILE
fil$ = COMMAND$
IF INSTR(fil$, ".") > 0 THEN
    fil$ = LEFT$(fil$, INSTR(fil$, ".") - 1)
END IF
IF INSTR(fil$, "\") > 0 THEN
    fil$ = MID$(fil$, INSTR(fil$, "\") + 1)
END IF
IF fil$ = "" THEN
    PRINT "no file specified."
    END
END IF
OPEN "i", fil, COMMAND$
fil2 = FREEFILE
OPEN "o", fil2, fil$ + ".mif"
x = 0

PRINT #fil2, "WIDTH=32;"
PRINT #fil2, "DEPTH=512;"
PRINT #fil2, "ADDRESS_RADIX=HEX;"
PRINT #fil2, "DATA_RADIX=HEX;"
PRINT #fil2,
PRINT #fil2, "CONTENT BEGIN"
flag = 0
y = 0
WHILE NOT EOF(fil)
    LINE INPUT #fil, l$
    PRINT l$
    lin$ = RIGHT$(l$, 8)
    PRINT #fil2, hex$(y);" : ";lin$;";"
    y=y+1
WEND
print #fil2, "END;"
CLOSE
PRINT fil$ + ".mif created."
END

FileOpenError:

IF ERR = 53 THEN
    PRINT "file not found."
    END
ELSE
    PRINT "an error occurred opening the file"
    END
END IF
