asm %1.asm > raw.raw
type raw.raw
makehex32 raw.raw
makemif32 raw.raw
copy raw.hex %2.hex
copy raw.mif %2.mif

